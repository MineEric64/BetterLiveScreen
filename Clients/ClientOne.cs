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

using My = BetterLiveScreen.MainWindow;

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

        /// <summary>
        /// (User, (SendType, (buffer, read)))
        /// </summary>
        private Dictionary<string, Dictionary<SendTypes, (byte[], int)>> _bufferMap = new Dictionary<string, Dictionary<SendTypes, (byte[], int)>>();
        private EventBasedNetListener _listener;
        public NetManager Client { get; set; }
        public Queue<ReceiveInfo> ReceivedQueue { get; private set; } = new Queue<ReceiveInfo>();
        public Dictionary<IPEndPoint, UserInfo> UserMap { get; private set; } = new Dictionary<IPEndPoint, UserInfo>();

        public bool IsStarted { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;

        //if this user joined it, it will be invoked.
        public event EventHandler Connected;
        public event EventHandler<bool> Disconnected;

        //if other user joined, it will be invoked.
        public event EventHandler<UserInfo> UserConnected;
        public event EventHandler<string> UserDisconnected; //if string is empty, this event is invoked to host only.

        //if the user joined, it will be invoked to host only.
        public event EventHandler HostConnected;

        public event EventHandler<(string, BitmapInfo)> StreamStarted;
        public event EventHandler<string> StreamEnded;

        public event EventHandler<string> WatchStarted;
        public event EventHandler<string> WatchEnded;

        public event EventHandler<(byte[], string)> VideoBufferReceived;
        public event EventHandler<(byte[], string)> AudioBufferReceived;

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
                var info = new ReceiveInfo(SendTypes.PeerConnected, ResponseCodes.OK);

                SendBuffer(info, peer);
                HostConnected?.Invoke(null, null);
            }
            else
            {
                Connected?.Invoke(null, null);
            }
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (RoomManager.CurrentRoom == null || !RoomManager.IsHost)
            {
                bool roomDeleted = false;

                if (!disconnectInfo.AdditionalData.EndOfData) //this means room is deleted
                {
                    ReadOnlyMemory<byte> buffer = new ReadOnlyMemory<byte>(disconnectInfo.AdditionalData.RawData, disconnectInfo.AdditionalData.Position, disconnectInfo.AdditionalData.AvailableBytes);
                    string text = Decode(buffer.ToArray());

                    roomDeleted = text == "RoomDeleted";
                }

                if (roomDeleted) RoomManager.Disconnect(true);
                Disconnected?.Invoke(null, roomDeleted);
                IsConnected = false;

                return;
            }
            if (RoomManager.IsHost)
            {
                string userName = string.Empty;

                if (UserMap.TryGetValue(peer.EndPoint, out var user))
                {
                    userName = user.ToString();
                    UserMap.Remove(peer.EndPoint);
                }
                UserDisconnected?.Invoke(null, userName);
            }
        }

        private void OnConnectionRequested(ConnectionRequest request)
        {
            if (!RoomManager.IsHost)
            {
                Debug.WriteLine("[Warning] This user is not host. reject the connection.");
                request.Reject();

                return;
            }

            if (Client.ConnectedPeersCount < RoomManager.MAX_USER_COUNT /* max connections */)
            {
                if (request.AcceptIfKey(DEFAULT_KEY) != null)
                    Debug.WriteLine("[Info] The connection is accepted by host.");
                else
                    Debug.WriteLine("[Warning] Can't be accepted, the key doesn't equals to default key.");
            }
            else
            {
                request.Reject();
                Debug.WriteLine("[Warning] The number of connected users is exceed to max. reject the connection.");
            }
        }

        private void OnNetworkReceived(NetPeer peer, NetDataReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            ReadOnlyMemory<byte> receivedBuffer = new ReadOnlyMemory<byte>(reader.RawData, reader.Position, reader.AvailableBytes);
            var receivedInfo = MessagePackSerializer.Deserialize<ReceiveInfo>(receivedBuffer);

            byte[] buffer;
            ReceiveInfo info;

            string jsonRaw;
            JObject json;

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

                            foreach (var user in My.Users)
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

                            UserMap.Add(peer.EndPoint, new UserInfo(userName, userAvatarUrl));
                            UserConnected?.Invoke(null, UserMap[peer.EndPoint]);
                        }
                        else
                        {
                            info = receivedInfo.GetFailed(ResponseCodes.AccessDenied);
                            SendBuffer(info, peer);
                        }

                        break;
                    #endregion
                    #region User
                    case SendTypes.UserDisconnected:
                        SendBufferToAllExcept(receivedInfo, peer);
                        break;
                    #endregion
                    #region Streaming
                    case SendTypes.StreamStarted:
                        SendBufferToAllExcept(receivedInfo, peer);
                        break;

                    case SendTypes.StreamEnded:
                        SendBufferToAllExcept(receivedInfo, peer);
                        break;

                    #region Watch
                    case SendTypes.WatchStarted:

                        break;

                    case SendTypes.WatchEnded:

                        break;
                    #endregion
                    #region Video
                    case SendTypes.Video:
                        SendBufferToAllExcept(receivedInfo, peer); //test (need to add watch feature)
                        break;
                    #endregion
                    #region Audio
                    case SendTypes.Audio:
                        SendBufferToAllExcept(receivedInfo, peer); //test (need to add watch feature)
                        break;
                    #endregion
                    #endregion
                }
            }
            else //For User
            {
                string userName = string.Empty;
                byte checksum = 0;

                switch (receivedInfo.SendType)
                {
                    #region Peer
                    case SendTypes.PeerConnected:
                        ReceivedQueue.Enqueue(receivedInfo);
                        break;
                    #endregion
                    #region Room
                    case SendTypes.RoomInfoRequested:
                        ReceivedQueue.Enqueue(receivedInfo);
                        break;

                    case SendTypes.RoomConnectRequested:
                        ReceivedQueue.Enqueue(receivedInfo);
                        break;
                    #endregion
                    #region User
                    case SendTypes.UserConnected:
                        jsonRaw = Decode(receivedInfo.Buffer);
                        json = JObject.Parse(jsonRaw);
                        userName = json["user"].ToString();
                        string userAvatarUrl = json["user_avatar_url"]?.ToString() ?? string.Empty;

                        if (string.IsNullOrEmpty(userName))
                        {
                            Debug.WriteLine("[Error] Can't get user name from packet receiving event.");
                            break;
                        }

                        UserConnected?.Invoke(null, new UserInfo(userName, userAvatarUrl));
                        break;

                    case SendTypes.UserDisconnected:
                        userName = Decode(receivedInfo.Buffer);
                        UserDisconnected?.Invoke(null, userName);

                        break;
                    #endregion
                    #region Streaming
                    case SendTypes.StreamStarted:
                        var videoInfo = MessagePackSerializer.Deserialize<BitmapInfo>(receivedInfo.ExtraBuffer);
                        userName = Decode(receivedInfo.Buffer);

                        StreamStarted?.Invoke(null, (userName, videoInfo));
                        break;

                    case SendTypes.StreamEnded:
                        userName = Decode(receivedInfo.Buffer);
                        StreamEnded?.Invoke(null, userName);

                        break;
                    #region Watch
                    case SendTypes.WatchStarted:

                        break;

                    case SendTypes.WatchEnded:

                        break;
                    #endregion
                    #region Video
                    case SendTypes.Video:
                        json = JObject.Parse(Decode(receivedInfo.ExtraBuffer));
                        userName = json["user"]?.ToString() ?? string.Empty;
                        checksum = json["checksum"]?.ToObject<byte>() ?? 0;

                        if (receivedInfo.Step == 0)
                        {
                            int bufferLength = json["buffer_length"]?.ToObject<int>() ?? 0;
                            byte[] videoBuffer = new byte[bufferLength];

                            if (_bufferMap.TryGetValue(userName, out var bufferMap2))
                            {
                                if (bufferMap2.TryGetValue(SendTypes.Video, out _))
                                {
                                    bufferMap2[SendTypes.Video] = (videoBuffer, 0);
                                }
                                else
                                {
                                    bufferMap2.Add(SendTypes.Video, (videoBuffer, 0));
                                }
                            }
                            else
                            {
                                _bufferMap.Add(userName, new Dictionary<SendTypes, (byte[], int)>());
                                _bufferMap[userName].Add(SendTypes.Video, (videoBuffer, 0));
                            }
                        }
                        var bufferInfo = _bufferMap[userName][SendTypes.Video];

                        Buffer.BlockCopy(receivedInfo.Buffer, 0, bufferInfo.Item1, bufferInfo.Item2, receivedInfo.Buffer.Length);
                        _bufferMap[userName][SendTypes.Video] = (bufferInfo.Item1, bufferInfo.Item2 + receivedInfo.Buffer.Length);

                        if (receivedInfo.Step == receivedInfo.MaxStep)
                        {
                            byte bufferChecksum = Checksum.ComputeAddition(bufferInfo.Item1);

                            if (bufferChecksum != checksum)
                            {
                                Debug.WriteLine("[Warning] Checksum doesn't equals to original buffer's value. skip this buffer");
                                break;
                            }

                            VideoBufferReceived?.Invoke(null, (bufferInfo.Item1, userName));
                        }

                        break;
                    #endregion
                    #region Audio
                    case SendTypes.Audio:
                        json = JObject.Parse(Decode(receivedInfo.ExtraBuffer));
                        userName = json["user"]?.ToString() ?? string.Empty;
                        checksum = json["checksum"]?.ToObject<byte>() ?? 0;

                        if (receivedInfo.Step == 0)
                        {
                            int bufferLength = json["buffer_length"]?.ToObject<int>() ?? 0;
                            byte[] videoBuffer = new byte[bufferLength];

                            if (_bufferMap.TryGetValue(userName, out var bufferMap2))
                            {
                                if (bufferMap2.TryGetValue(SendTypes.Audio, out _))
                                {
                                    bufferMap2[SendTypes.Audio] = (videoBuffer, 0);
                                }
                                else
                                {
                                    bufferMap2.Add(SendTypes.Audio, (videoBuffer, 0));
                                }
                            }
                            else
                            {
                                _bufferMap.Add(userName, new Dictionary<SendTypes, (byte[], int)>());
                                _bufferMap[userName].Add(SendTypes.Audio, (videoBuffer, 0));
                            }
                        }

                        if (receivedInfo.Step > 0) userName = Decode(receivedInfo.ExtraBuffer);
                        var bufferInfo2 = _bufferMap[userName][SendTypes.Audio];

                        Buffer.BlockCopy(receivedInfo.Buffer, 0, bufferInfo2.Item1, bufferInfo2.Item2, receivedInfo.Buffer.Length);
                        bufferInfo2.Item2 += receivedInfo.Buffer.Length;

                        if (receivedInfo.Step == receivedInfo.MaxStep)
                        {
                            AudioBufferReceived?.Invoke(null, (bufferInfo2.Item1, userName));
                        }

                        break;
                        #endregion
                    #endregion
                }
            }
        }

        public void Start()
        {
            Client.Start(PORT_NUMBER);
            IsStarted = true;

            Task.Run(() =>
            {
                while (IsStarted)
                {
                    Client.PollEvents();
                    Thread.Sleep(15);
                }
            });
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

        public void Disconnect()
        {
            if (!IsConnected) return;

            if (!RoomManager.IsHost)
            {
                Client.DisconnectAll();
            }
            else
            {
                byte[] buffer = Encode("RoomDeleted");

                Client.DisconnectAll(buffer, 0, buffer.Length);

                ReceivedQueue.Clear();
                UserMap.Clear();
                IsConnected = false;
            }
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
        /// <param name="sentType">Info</param>
        /// <param name="timeOut_">if this param is null, set to default value (10 seconds)</param>
        /// <returns></returns>
        public async Task<ReceiveInfo> ReceiveBufferAsync(SendTypes sentType, TimeSpan? timeOut_ = null)
        {
            return await ReceiveBufferAsync(new ReceiveInfo(sentType), timeOut_);
        }

        /// <summary>
        /// Receive the buffer that is sent by user. This method must not be used when the buffer has to process with low latency.
        /// </summary>
        /// <param name="sentInfo">Info</param>
        /// <param name="timeOut_">if this param is null, set to default value (10 seconds)</param>
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

        public static IEnumerable<ReceiveInfo> DivideInfo(SendTypes sendType, byte[] buffer)
        {
            int read = 0;
            int step = 0;
            int maxStep = (int)Math.Ceiling((double)buffer.Length / 65507) - 1;

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
