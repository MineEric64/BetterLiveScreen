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

using MessagePack;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Open.P2P;
using Open.P2P.EventArgs;
using Open.P2P.IO;
using Open.P2P.Listeners;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Rooms;

namespace BetterLiveScreen.Clients
{
    public class ClientOne
    {
        public const int PORT_NUMBER = 4089;
        public const int MAXIMUM_BUFFER_SIZE = 65507;

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public List<IPEndPoint> IPEPs { get; private set; } = new List<IPEndPoint>();
        public CommunicationManager Client { get; set; }
        public Queue<ReceiveInfo> ReceivedQueue { get; private set; } = new Queue<ReceiveInfo>();

        public bool IsReady => IPEPs.Count > 0;
        public bool IsConnected { get; private set; } = false;

        public ClientOne()
        {
            //for serialize optimization
            //for details, see at https://github.com/neuecc/MessagePack-CSharp/issues/1350
            _ = MessagePackSerializer.Serialize(BitmapInfo.Empty);

            Client = new CommunicationManager();
            Client.PeerConnected += OnPeerConnected;
        }

        private void OnPeerConnected(object sender, PeerEventArgs e)
        {
            MessageBox.Show($"Hi {e.Peer.EndPoint}");
            Task.Run(async () =>
            {
                using (var reader = new MessagePackStreamReader(e.Peer.Stream))
                {
                    CancellationToken token = new CancellationToken();

                    while (true)
                    {
                        ReadOnlySequence<byte>? buffer = await reader.ReadAsync(token);

                        if (buffer != null)
                        {
                            var info = MessagePackSerializer.Deserialize<ReceiveInfo>(buffer.Value);
                            await OnReceived(e, info);
                        }
                        Thread.Sleep(10);
                    }
                }
            });
        }

        private async Task OnReceived(PeerEventArgs e, ReceiveInfo receivedInfo)
        {
            if (RoomManager.IsAdmin) //For Admin
            {
                switch (receivedInfo.SendType)
                {
                    #region Room
                    case SendTypes.RoomInfoRequested:
                        string json = RoomManager.CurrentRoom.ToJsonString();
                        byte[] buffer = Encode(json);

                        var info = new ReceiveInfo(SendTypes.RoomInfoRequested, ResponseCodes.OK, buffer, BufferTypes.JsonString);
                        await SendBufferAsync(info);

                        break;

                    case SendTypes.RoomConnectRequested:


                        break;

                    case SendTypes.RoomDeleted:


                        break;
                    #endregion
                }
            }
            else //For User
            {
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


                        break;
                    #endregion
                }
            }
        }

        public async Task SendBufferAsync(ReceiveInfo info)
        {
            if (!IsReady) throw new ArgumentException("IP Endpoints are not available");

            byte[] buffer = MessagePackSerializer.Serialize(info);
            await Client.SendAsync(buffer, 0, buffer.Length, IPEPs);
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
                var info = ReceivedQueue.Peek();

                if (info.SendType == sentInfo.SendType)
                {
                    _ = ReceivedQueue.Dequeue();
                    sw.Stop();

                    return info;
                }
                await Task.Delay(10);

                if (sw.Elapsed >= timeOut) break;
            }
            sw.Stop();

            return ReceiveInfo.Timeout;
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

        public async Task ConnectAsync()
        {
            if (!IsReady) throw new ArgumentException("IP Endpoints are not available");

            await Client.ConnectAsync(IPEPs[0]);
            IsConnected = true;
        }

        public async Task CloseAsync()
        {
            if (RoomManager.IsConnected) await RoomManager.DisconnectAsync();

            IPEPs.Clear();
            IsConnected = false;
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

        /// <summary>
        /// the user that is not a admin has to use this method.
        /// </summary>
        public async Task<bool> AddAdminAsync(string adminAddress)
        {
            var ipep = await GetIPEPAsync(adminAddress);

            if (ipep.Address == IPAddress.None) return false;
            if (IPEPs.Count > 0 && IPEPs[0].Address.ToString() == ipep.Address.ToString()) return true; //already added, so doesn't have to add again
            IPEPs.Clear();
            IPEPs.Add(ipep);

            return true;
        }
    }
}
