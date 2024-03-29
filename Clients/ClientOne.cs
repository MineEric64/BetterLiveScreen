﻿using System;
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

using log4net;

using LiteNetLib;
using LiteNetLib.Utils;

using MessagePack;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NAudio;
using NAudio.Wave;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

using My = BetterLiveScreen.MainWindow;

namespace BetterLiveScreen.Clients
{
    public class ClientOne
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public const int PORT_NUMBER = 4089;
        public const int MAXIMUM_BUFFER_SIZE = 65000; //1200;
        public const string DEFAULT_KEY = "blss_default_key";
        public const DeliveryMethod DELIVERY_METHOD = DeliveryMethod.ReliableUnordered;

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        /// <summary>
        /// (User, (SendType, buffer))
        /// </summary>
        private Dictionary<string, Dictionary<SendTypes, byte[]>> _bufferMap = new Dictionary<string, Dictionary<SendTypes, byte[]>>();
        private EventBasedNetListener _listener;
        public NetManager Client { get; set; }
        public Queue<ReceiveInfo> ReceivedQueue { get; private set; } = new Queue<ReceiveInfo>();

        public Dictionary<IPEndPoint, UserInfo> UserMap { get; private set; } = new Dictionary<IPEndPoint, UserInfo>();
        public Dictionary<string, List<string>> WatchMap { get; private set; } = new Dictionary<string, List<string>>();

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

        public event EventHandler<string> StreamStarted;
        public event EventHandler<string> StreamEnded;

        //(buffer, userName, timestamp)
        public event EventHandler<(byte[], string, long)> VideoBufferReceived;
        public event EventHandler<(byte[], string, long)> AudioBufferReceived;

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
            Client.ChannelsCount = 3;
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
                    userName = user.FullName;
                    UserMap.Remove(peer.EndPoint);
                }
                UserDisconnected?.Invoke(null, userName);
            }
        }

        private void OnConnectionRequested(ConnectionRequest request)
        {
            if (!RoomManager.IsHost)
            {
                log.Warn("This user is not host. reject the connection.");
                request.Reject();

                return;
            }

            if (Client.ConnectedPeersCount < RoomManager.MAX_USER_COUNT /* max connections */)
            {
                if (request.AcceptIfKey(DEFAULT_KEY) != null)
                    log.Info("The connection is accepted by host.");
                else
                    log.Warn("Can't be accepted, the key doesn't equals to default key.");
            }
            else
            {
                request.Reject();
                log.Warn("The number of connected users is exceed to max. reject the connection.");
            }
        }

        private void OnNetworkReceived(NetPeer peer, NetDataReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            ReadOnlyMemory<byte> receivedBuffer = new ReadOnlyMemory<byte>(reader.RawData, reader.Position, reader.AvailableBytes);
            var receivedInfo = MessagePackSerializer.Deserialize<ReceiveInfo>(receivedBuffer);

            if (RoomManager.IsHost) //For Host
            {
                OnReceived4Host(peer, channel, receivedInfo);
                OnReceived4User(receivedInfo);
            }
            else //For User
            {
                OnReceived4User(receivedInfo);
            }
        }

        public void OnReceived4Host(NetPeer peer, byte channel, ReceiveInfo receivedInfo, string userName = "")
        {
            byte[] buffer;
            ReceiveInfo info;

            string jsonRaw;
            JObject json;

            if (string.IsNullOrEmpty(userName) && peer != null && UserMap.TryGetValue(peer.EndPoint, out var user_)) userName = user_.ToString();

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
                    userName = json["user"]?.ToString();
                    string userAvatarUrl = json["user_avatar_url"]?.ToString() ?? string.Empty;

                    if (!RoomManager.CurrentRoom.PasswordRequired || password == RoomManager.Password)
                    {
                        if (RoomManager.CurrentRoom.CurrentUserCount >= RoomManager.MAX_USER_COUNT)
                        {
                            info = receivedInfo.GetFailed(ResponseCodes.TooManyUsers);
                            SendBuffer(info, peer);

                            break;
                        }
                        if (userName == null || My.Users.Where(x => x.Equals(userName)).Any()) //duplicated user name
                        {
                            info = receivedInfo.GetFailed(ResponseCodes.Failed);
                            SendBuffer(info, peer);

                            break;
                        }

                        //Successful
                        //Send User Infos to joined user
                        var jsonUsers = new JArray();

                        foreach (var user in My.Users)
                        {
                            jsonUsers.Add(new JObject()
                                {
                                    { "full_name", user.FullName },
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

                        //Send Joined user info to all users except joined user
                        json = new JObject
                            {
                                { "user", userName },
                                { "user_avatar_url", userAvatarUrl }
                            };
                        buffer = Encode(json.ToString());
                        info = new ReceiveInfo(SendTypes.UserConnected, buffer, BufferTypes.JsonString);

                        SendBufferToAllExcept(info, peer);

                        //Add User Info (Host Only)
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
                    if (string.IsNullOrEmpty(userName)) return;

                    var streamInfo = MessagePackSerializer.Deserialize<BitmapInfo>(receivedInfo.ExtraBuffer);

                    if (Rescreen.VideoStreams.TryGetValue(userName, out var videoStream)) videoStream.Info = streamInfo;
                    else Rescreen.VideoStreams.Add(userName, new VideoLike(streamInfo));

                    SendBufferToAllExcept(receivedInfo, peer);
                    break;

                case SendTypes.StreamEnded:
                    SendBufferToAllExcept(receivedInfo, peer);
                    break;

                case SendTypes.StreamInfoRequested:
                    string userName2 = Decode(receivedInfo.Buffer);

                    if (Rescreen.VideoStreams.TryGetValue(userName2, out var video))
                    {
                        byte[] buffer2 = MessagePackSerializer.Serialize(video.Info);
                        var info2 = new ReceiveInfo(SendTypes.StreamInfoRequested, ResponseCodes.OK, receivedInfo.Buffer, BufferTypes.String, buffer2);

                        //if the host required this information, we have to send the info directly
                        if (peer == null) ReceivedQueue.Enqueue(info2);
                        else SendBuffer(info2, peer, channel);
                    }
                    else
                    {
                        SendBuffer(receivedInfo.GetFailed(ResponseCodes.Failed), peer);
                    }

                    break;

                #region Watch
                case SendTypes.WatchStarted:
                    if (string.IsNullOrEmpty(userName)) userName = UserMap[peer.EndPoint].ToString();
                    string userName12 = Decode(receivedInfo.Buffer); //the user that the sent user wants to watch

                    if (!WatchMap.TryGetValue(userName12, out var watches)) {
                        WatchMap.Add(userName12, new List<string>());
                        watches = WatchMap[userName12];
                    }
                    if (!watches.Contains(userName))
                    {
                        watches.Add(userName);
                        log.Info($"{userName} started to watch {userName12}'s Stream");
                    }
                    else
                    {
                        log.Warn($"{userName} is already watching {userName12}'s Stream");
                    }
                    break;

                case SendTypes.WatchEnded:
                    if (string.IsNullOrEmpty(userName)) userName = UserMap[peer.EndPoint].ToString();
                    string userName22 = Decode(receivedInfo.Buffer); //the user that the sent user wants to watch

                    if (WatchMap.TryGetValue(userName22, out var watches2))
                    {
                        watches2.Remove(userName);
                        log.Info($"{userName} stopped to watch {userName22}'s Stream");
                    }
                    break;
                #endregion
                #region Video & Audio
                case SendTypes.Video:
                case SendTypes.Audio:
                    //Others : Channel 0
                    //Video : Channel 1
                    //Audio : Channel 2
                    byte channelToSend = (byte)(receivedInfo.SendType == SendTypes.Video ? 1 : 2);

                    if (string.IsNullOrEmpty(userName)) return;
                    if (WatchMap.TryGetValue(userName, out var watches3))
                    {
                        foreach (var user in watches3)
                        {
                            var ep = UserMap.GetKeyByValue(My.GetUserByName(user));
                            NetPeer peer2 = null;
                            List<NetPeer> peers;

                            peers = Client.ToList();
                            foreach (var connected in peers)
                            {
                                if (connected?.EndPoint == ep)
                                {
                                    peer2 = connected;
                                }
                            }
                            SendBuffer(receivedInfo, peer2, channelToSend);
                        }
                    }
                    break;
                #endregion
                #endregion
            }
        }

        public void OnReceived4User(ReceiveInfo receivedInfo)
        {
            string userName = string.Empty;
            string jsonRaw;
            JObject json;

            byte checksum = 0;
            long timestamp = 0;
            int bufferLength = 0;

            switch (receivedInfo.SendType)
            {
                #region Peer
                case SendTypes.PeerConnected:
                    ReceivedQueue.Enqueue(receivedInfo);
                    break;
                #endregion
                #region Room
                case SendTypes.RoomInfoRequested:
                    if (RoomManager.IsHost) return; //the host must not receive this info, because it processed already on OnReceived4Host().
                    ReceivedQueue.Enqueue(receivedInfo);
                    break;

                case SendTypes.RoomConnectRequested:
                    if (RoomManager.IsHost) return; //the host must not receive this info, because it processed already on OnReceived4Host().
                    ReceivedQueue.Enqueue(receivedInfo);
                    break;
                #endregion
                #region User
                case SendTypes.UserConnected:
                    jsonRaw = Decode(receivedInfo.Buffer);
                    json = JObject.Parse(jsonRaw);
                    userName = json["user"]?.ToString();
                    string userAvatarUrl = json["user_avatar_url"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrEmpty(userName))
                    {
                        log.Error("Can't get user name from packet receiving event.");
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
                    userName = Decode(receivedInfo.Buffer);

                    StreamStarted?.Invoke(null, userName);
                    break;

                case SendTypes.StreamEnded:
                    userName = Decode(receivedInfo.Buffer);

                    StreamEnded?.Invoke(null, userName);
                    break;

                case SendTypes.StreamInfoRequested:
                    if (RoomManager.IsHost) return; //the host must not receive this info, because it processed already on OnReceived4Host().
                    ReceivedQueue.Enqueue(receivedInfo);
                    break;

                #region Video
                case SendTypes.Video:
                    json = JObject.Parse(Decode(receivedInfo.ExtraBuffer));
                    userName = json["user"]?.ToString();
                    checksum = json["checksum"]?.ToObject<byte>() ?? 0;
                    bufferLength = json["buffer_length"]?.ToObject<int>() ?? 0;
                    timestamp = json["timestamp"]?.ToObject<long>() ?? Timestamp.Now;

                    if (_bufferMap.TryGetValue(userName, out var bufferMap2))
                    {
                        if (bufferMap2.TryGetValue(SendTypes.Video, out var prevBuffer))
                        {
                            if (prevBuffer == null || prevBuffer.Length != bufferLength)
                            {
                                byte[] videoBuffer = new byte[bufferLength];
                                bufferMap2[SendTypes.Video] = videoBuffer;
                            }
                        }
                        else
                        {
                            byte[] videoBuffer = new byte[bufferLength];
                            bufferMap2.Add(SendTypes.Video, videoBuffer);
                        }
                    }
                    else
                    {
                        byte[] videoBuffer = new byte[bufferLength];
                        _bufferMap.Add(userName, new Dictionary<SendTypes, byte[]>());
                        _bufferMap[userName].Add(SendTypes.Video, videoBuffer);
                    }

                    byte[] bufferInfo = _bufferMap[userName][SendTypes.Video];
                    int offset = receivedInfo.Step * MAXIMUM_BUFFER_SIZE;

                    Buffer.BlockCopy(receivedInfo.Buffer, 0, bufferInfo, offset, receivedInfo.Buffer.Length);

                    if (receivedInfo.Step == receivedInfo.MaxStep)
                    {
                        byte bufferChecksum = Checksum.ComputeAddition(bufferInfo);

                        if (bufferChecksum != checksum)
                        {
                            log.Warn("Checksum doesn't equals to original buffer's value. skipped this buffer.");
                            break;
                        }

                        VideoBufferReceived?.Invoke(null, (bufferInfo, userName, timestamp));
                        _bufferMap[userName][SendTypes.Video] = null;
                    }

                    break;
                #endregion
                #region Audio
                case SendTypes.Audio:
                    json = JObject.Parse(Decode(receivedInfo.ExtraBuffer));
                    userName = json["user"]?.ToString();
                    checksum = json["checksum"]?.ToObject<byte>() ?? 0;
                    bufferLength = json["buffer_length"]?.ToObject<int>() ?? 0;
                    int audioSampleRate = json["audio_sample_rate"]?.ToObject<int>() ?? 44100;
                    int audioChannel = json["audio_channel"]?.ToObject<int>() ?? 2;
                    timestamp = json["timestamp"]?.ToObject<long>() ?? Timestamp.Now;

                    if (_bufferMap.TryGetValue(userName, out var bufferMap3))
                    {
                        if (bufferMap3.TryGetValue(SendTypes.Audio, out var prevBuffer))
                        {
                            if (prevBuffer == null || prevBuffer.Length != bufferLength)
                            {
                                byte[] videoBuffer = new byte[bufferLength];
                                bufferMap3[SendTypes.Audio] = videoBuffer;
                            }
                        }
                        else
                        {
                            byte[] videoBuffer = new byte[bufferLength];
                            bufferMap3.Add(SendTypes.Audio, videoBuffer);
                        }
                    }
                    else
                    {
                        byte[] videoBuffer = new byte[bufferLength];
                        _bufferMap.Add(userName, new Dictionary<SendTypes, byte[]>());
                        _bufferMap[userName].Add(SendTypes.Audio, videoBuffer);
                    }

                    var bufferInfo2 = _bufferMap[userName][SendTypes.Audio];
                    int offset2 = receivedInfo.Step * MAXIMUM_BUFFER_SIZE;

                    Buffer.BlockCopy(receivedInfo.Buffer, 0, bufferInfo2, offset2, receivedInfo.Buffer.Length);

                    if (receivedInfo.Step == receivedInfo.MaxStep)
                    {
                        byte bufferChecksum = Checksum.ComputeAddition(bufferInfo2);

                        if (bufferChecksum != checksum)
                        {
                            log.Warn("Checksum doesn't equals to original buffer's value. skipped this buffer.");
                            break;
                        }

                        Rescreen.VideoStreams[userName].ChangeFormat(WaveFormat.CreateIeeeFloatWaveFormat(audioSampleRate, audioChannel));
                        AudioBufferReceived?.Invoke(null, (bufferInfo2, userName, timestamp));
                        _bufferMap[userName][SendTypes.Audio] = null;
                    }

                    break;
                    #endregion
                    #endregion
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

        public void SendBuffer(ReceiveInfo info, NetPeer peer, byte channel = 0)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            peer?.Send(buffer, channel, DELIVERY_METHOD);
        }

        public void SendBufferToHost(ReceiveInfo info)
        {
            SendBuffer(info, Client.FirstPeer);
        }

        public void SendBufferToAll(ReceiveInfo info)
        {
            
            byte[] buffer = MessagePackSerializer.Serialize(info);

            foreach (NetPeer peer in Client.ConnectedPeerList)
            {
                if (!UserMap.ContainsKey(peer.EndPoint)) continue;
                peer.Send(buffer, DELIVERY_METHOD);
            }
        }

        public void SendBufferToAllExcept(ReceiveInfo info, NetPeer exceptPeer)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);

            foreach (NetPeer peer in Client.ConnectedPeerList)
            {
                if (exceptPeer != null && (!UserMap.ContainsKey(peer.EndPoint) || exceptPeer.EndPoint == peer.EndPoint)) continue;
                peer.Send(buffer, DELIVERY_METHOD);
            }
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
        /// <param name="timeOut_">if this param is null, set to default value (5 seconds)</param>
        /// <returns></returns>
        public async Task<ReceiveInfo> ReceiveBufferAsync(ReceiveInfo sentInfo, TimeSpan? timeOut_ = null)
        {
            TimeSpan timeout = timeOut_ ?? TimeSpan.FromSeconds(5);
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

                if (sw.Elapsed >= timeout) break;
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
            catch (Exception ex)
            {
                log.Error("Can't get IP End Point.", ex);
            }
            return new IPEndPoint(address, PORT_NUMBER);
        }

        public static IEnumerable<ReceiveInfo> DivideInfo(SendTypes sendType, byte[] buffer)
        {
            int step = 0;
            int maxStep = (int)Math.Ceiling((double)buffer.Length / MAXIMUM_BUFFER_SIZE) - 1;

            var infos = new List<ReceiveInfo>();

            while (step <= maxStep)
            {
                int bytesRead = GetBufferSize(buffer.Length, step, maxStep);
                int offset = step * MAXIMUM_BUFFER_SIZE;

                byte[] buffer2 = new byte[bytesRead];

                Buffer.BlockCopy(buffer, offset, buffer2, 0, bytesRead);
                infos.Add(new ReceiveInfo(sendType, step++, maxStep, buffer2, BufferTypes.ByteArray));
            }

            return infos;
        }

        public static int GetBufferSize(int length, int step, int maxStep)
        {
            return (step != maxStep) ? MAXIMUM_BUFFER_SIZE : length - (maxStep * MAXIMUM_BUFFER_SIZE);
        }

        public static int GetBufferSize(int length, ReceiveInfo info)
        {
            return GetBufferSize(length, info.Step, info.MaxStep);
        }
    }
}
