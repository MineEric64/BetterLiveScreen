using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using LiteNetLib;
using LiteNetLib.Utils;

using MessagePack;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

namespace BetterLiveScreen.Clients
{
    public class ClientOne
    {
        public const int PORT_NUMBER = 4089;
        public const int MAXIMUM_BUFFER_SIZE = 65507;
        public const string DEFAULT_KEY = "blss_default_key";

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        private EventBasedNetListener _listener;
        public NetManager Client { get; set; }
        public Queue<ReceiveInfo> ReceivedQueue { get; private set; } = new Queue<ReceiveInfo>();

        public bool IsStarted { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;

        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public event EventHandler HostConnected;
        public event EventHandler<string> HostDisconnected;

        public ClientOne()
        {
            //for serialize optimization
            //for details, see at https://github.com/neuecc/MessagePack-CSharp/issues/1350
            _ = MessagePackSerializer.Serialize(BitmapInfo.Empty);

            _listener = new EventBasedNetListener();
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceived;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.ConnectionRequestEvent += OnConnectionRequested;

            Client = new NetManager(_listener);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            IsConnected = true;

            if (RoomManager.IsHost)
            {
                HostConnected?.Invoke(null, null);
            }
            else
            {
                Connected?.Invoke(null, null);
            }
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (RoomManager.IsHost)
            {
                HostDisconnected?.Invoke(null, string.Empty);
            }
            else
            {
                IsConnected = false;
                Disconnected?.Invoke(null, null);
            }
        }

        private void OnConnectionRequested(ConnectionRequest request)
        {
            if (!RoomManager.IsHost)
            {
                request.Reject();
                return;
            }

            if (Client.ConnectedPeersCount < RoomManager.MAX_USER_COUNT /* max connections */)
            {
                request.AcceptIfKey(DEFAULT_KEY);
            }
            else
            {
                request.Reject();
            }
        }

        private void OnNetworkReceived(NetPeer peer, NetDataReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            ReadOnlyMemory<byte> receivedBuffer = new ReadOnlyMemory<byte>(reader.RawData, reader.Position, reader.RawDataSize - reader.Position);
            var receivedInfo = MessagePackSerializer.Deserialize<ReceiveInfo>(receivedBuffer);

            byte[] buffer;
            ReceiveInfo info;

            string jsonRaw;
            JObject json;

            var main = (MainWindow)Application.Current.MainWindow;

            if (RoomManager.IsHost) //For Host
            {
                switch (receivedInfo.SendType)
                {
                    #region Room
                    case SendTypes.RoomInfoRequested:
                        jsonRaw = JsonConvert.SerializeObject(RoomManager.CurrentRoom);
                        buffer = Encode(jsonRaw);

                        info = new ReceiveInfo(SendTypes.RoomInfoRequested, ResponseCodes.OK, buffer, BufferTypes.JsonString);
                        SendBuffer(info, peer);

                        break;

                    case SendTypes.RoomConnectRequested:
                        json = JObject.Parse(Decode(receivedInfo.Buffer));
                        string password = json["password"]?.ToString() ?? string.Empty;
                        string userName = json["user"]?.ToString();
                        string userAvatarUrl = json["user_avatar_url"]?.ToString() ?? string.Empty;

                        if (!RoomManager.CurrentRoom.PasswordRequired || password == RoomManager.Password)
                        {
                            if (RoomManager.CurrentRoom.CurrentUserCount >= RoomManager.MAX_USER_COUNT)
                            {
                                info = receivedInfo.GetFailed(ResponseCodes.TooManyUsers);
                                SendBuffer(info, peer);

                                break;
                            }
                            if (userName == null)
                            {
                                info = receivedInfo.GetFailed(ResponseCodes.Failed);
                                SendBuffer(info, peer);

                                break;
                            }
                            var jsonUsers = new JArray();

                            foreach (var user in MainWindow.Users)
                            {
                                jsonUsers.Add(new JObject()
                                {
                                    { "full_name", user.ToString() },
                                    { "avatar_url", user.AvatarURL },
                                    { "is_lived", user.IsLived }
                                });
                            }
                            json = new JObject
                            {
                                { "users", jsonUsers },
                                { "room_id", RoomManager.CurrentRoomId.ToString() }
                            };

                            buffer = Encode(json.ToString());
                            info = new ReceiveInfo(SendTypes.RoomConnectRequested, ResponseCodes.OK, buffer, BufferTypes.JsonString);
                            SendBuffer(info, peer);

                            json = new JObject
                            {
                                { "user", userName },
                                { "user_avatar_url", userAvatarUrl }
                            };
                            buffer = Encode(json.ToString());

                            info = new ReceiveInfo(SendTypes.UserConnected, buffer, BufferTypes.JsonString);
                            SendBufferToAllExcept(info, peer);
                        }
                        else
                        {
                            info = receivedInfo.GetFailed(ResponseCodes.AccessDenied);
                            SendBuffer(info, peer);
                        }

                        break;

                    case SendTypes.UserDisconnected:
                        SendBufferToAllExcept(receivedInfo, peer);
                        break;

                        #endregion
                }
            }
            else //For User
            {
                string userName;

                switch (receivedInfo.SendType)
                {
                    #region Room
                    case SendTypes.RoomInfoRequested:
                        ReceivedQueue.Enqueue(receivedInfo);
                        break;

                    case SendTypes.RoomConnectRequested:
                        ReceivedQueue.Enqueue(receivedInfo);
                        break;

                    case SendTypes.RoomDeleted:
                        RoomManager.IsConnected = false;
                        Close();
                        Rescreen.Stop();
                        main.InitializeUI();

                        Disconnected?.Invoke(null, null);

                        break;

                    case SendTypes.UserConnected:
                        jsonRaw = Decode(receivedInfo.Buffer);
                        json = JObject.Parse(jsonRaw);
                        userName = json["user"].ToString();
                        string userAvatarUrl = json["user"].ToString() ?? string.Empty;

                        if (string.IsNullOrEmpty(userName))
                        {
                            Debug.WriteLine("[Error] Can't get user name from packet receiving event.");
                            break;
                        }

                        MainWindow.Users.Add(new UserInfo(userName, userAvatarUrl));
                        main.UpdateUserUI();

                        break;

                    case SendTypes.UserDisconnected:
                        jsonRaw = Decode(receivedInfo.Buffer);
                        json = JObject.Parse(jsonRaw);
                        userName = json["user"].ToString();

                        MainWindow.Users.Remove(MainWindow.Users.Where(x => x.Equals(userName)).First());
                        main.UpdateUserUI();

                        break;
                        #endregion
                }
            }
        }

        public void Start()
        {
            Client.Start(PORT_NUMBER);

            Task.Run(() =>
            {
                while (true)
                {
                    Client.PollEvents();
                    Thread.Sleep(15);
                }
            });
            IsStarted = true;
        }

        public void Stop()
        {
            if (!IsConnected) return;

            Client.Stop();
            IsConnected = false;
            IsStarted = false;
        }

        public void Connect(IPEndPoint ipep)
        {
            Client.Connect(ipep, DEFAULT_KEY);
        }

        public void Close()
        {
            if (RoomManager.IsConnected) RoomManager.Disconnect();

            Client.DisconnectAll();
            Stop();

            IsConnected = false;
        }

        public void SendBuffer(ReceiveInfo info, NetPeer peer)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            peer?.Send(buffer, DeliveryMethod.ReliableUnordered);
        }

        public void SendBufferToHost(ReceiveInfo info)
        {
            SendBuffer(info, Client.FirstPeer);
        }

        public void SendBufferToAll(ReceiveInfo info)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            Client.SendToAll(buffer, DeliveryMethod.ReliableUnordered);
        }

        public void SendBufferToAllExcept(ReceiveInfo info, NetPeer exceptPeer)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            Client.SendToAll(buffer, DeliveryMethod.ReliableUnordered, exceptPeer);
        }

        /// <summary>
        /// Receive the buffer that is sent by user. This method must not be used when the buffer has to process with low latency.
        /// </summary>
        /// <param name="sentInfo">Info</param>
        /// <param name="timeOut">if this param is null, set to default value (10 seconds)</param>
        /// <returns></returns>
        public async Task<ReceiveInfo> ReceiveBufferAsync(ReceiveInfo sentInfo, TimeSpan? timeOut_ = null)
        {
            TimeSpan timeOut = timeOut_ ?? TimeSpan.FromSeconds(10);
            Stopwatch sw = Stopwatch.StartNew();

            while (true)
            {
                if (ReceivedQueue.Count > 0)
                {
                    var info = ReceivedQueue.Peek();

                    if (info.SendType == sentInfo.SendType)
                    {
                        _ = ReceivedQueue.Dequeue();
                        sw.Stop();

                        return info;
                    }
                }
                await Task.Delay(10);

                if (sw.Elapsed >= timeOut) break;
            }
            sw.Stop();

            return sentInfo.GetFailed(ResponseCodes.Timeout);
        }

        public static byte[] SerializeLz4<T>(T info)
        {
            return MessagePackSerializer.Serialize(info, LZ4_OPTIONS);
        }

        public static T DeserializeLz4<T>(byte[] buffer)
        {
            return MessagePackSerializer.Deserialize<T>(buffer, LZ4_OPTIONS);
        }

        public static byte[] Encode(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        public static string Decode(byte[] buffer)
        {
            return Encoding.UTF8.GetString(buffer);
        }

        public static async Task<IPEndPoint> GetIPEPAsync(string ip)
        {
            IPAddress address = IPAddress.None;

            try
            {
                if (NetworkManager.IsAvailable)
                {
                    if (!IPAddress.TryParse(ip, out address))
                    {
                        IPAddress[] hosts = await Dns.GetHostAddressesAsync(ip);

                        if (hosts.Length > 0)
                        {
                            address = hosts[0];
                        }
                    }
                    else
                    {
                        address = IPAddress.Parse(ip);
                    }
                }
            }
            catch
            {
                Debug.WriteLine("[Error] Can't get IP End Point.");
            }
            return new IPEndPoint(address, PORT_NUMBER);
        }

        public static IEnumerable<ReceiveInfo> GetDividedInfo(SendTypes sendType, byte[] buffer)
        {
            int read = 0;
            int step = 0;
            int maxStep = (int)Math.Ceiling((double)buffer.Length / 65507);

            var infos = new List<ReceiveInfo>();

            while (read < buffer.Length)
            {
                int bytesRead = read + 65507 < buffer.Length ? 65507 : buffer.Length - read;
                byte[] buffer2 = new byte[bytesRead];

                Buffer.BlockCopy(buffer, read, buffer2, 0, bytesRead);
                infos.Add(new ReceiveInfo(sendType, step++, maxStep, buffer2, BufferTypes.ByteArray));

                read += bytesRead;
            }

            return infos;
        }
    }
}
