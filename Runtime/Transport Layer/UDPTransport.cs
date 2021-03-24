using kcp2k;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using UnityEngine;

namespace VaporNetworking
{
    public enum UDPChannels : int
    {
        Unreliable = 0,
        Reliable = 1
    }

    public class UDPTransport
    {
        public enum TransportEvent { Connected, Data, Disconnected }

        public enum Source { Default = 0, Client = 1, Server = 2 }

        // scheme used by this transport
        public const string Scheme = "kcp";

        // Transport Configuration
        public static ushort Port = 7777;
        // NoDelay is recommended to reduce latency. This also scales better without buffers getting full.
        public static bool NoDelay = true;
        // KCP internal update interval. 100ms is KCP default, but a lower interval is recommended to minimize latency and to scale to more networked entities.
        public static uint Interval = 10;
        // Advanced
        // KCP fastresend parameter. Faster resend for the cost of higher bandwidth. 0 in normal mode, 2 in turbo mode.
        public static int FastResend = 2;
        // KCP congestion window. Enabled in normal mode, disabled in turbo mode. Disable this for high scale games if connections get chocked regularly.
        public static bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        // KCP window size can be modified to support higher loads.
        public static uint SendWindowSize = 4096; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.
        // KCP window size can be modified to support higher loads.
        public static uint ReceiveWindowSize = 4096; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.

        private static KcpClient client;
        private static KcpServer server;

        private static bool IsServer;
        private static bool IsClient;

        private static bool IsSimulated;
        private static ConcurrentQueue<SimulatedMessage> simulatedServerQueue = new ConcurrentQueue<SimulatedMessage>();
        private static ConcurrentQueue<SimulatedMessage> simulatedClientQueue = new ConcurrentQueue<SimulatedMessage>();

        public static void Init(bool isServer = false, bool isClient = false, bool isSimulated = false)
        {
            IsServer = isServer ? true : IsServer;
            IsClient = isClient ? true : IsClient;
            IsSimulated = isSimulated ? true : IsSimulated;

            if (isSimulated)
            {
                if (NetLogFilter.logInfo) { Debug.Log("Transport Layer Initialized: Simulation"); }
                return;
            }

            if (IsServer && server == null)
            {
                // server
                server = new KcpServer(
                    (connectionId) => OnServerConnected.Invoke(connectionId),
                    (connectionId, message) => OnServerDataReceived.Invoke(connectionId, message, (int)UDPChannels.Reliable),
                    (connectionId) => OnServerDisconnected.Invoke(connectionId),
                    NoDelay,
                    Interval,
                    FastResend,
                    CongestionWindow,
                    SendWindowSize,
                    ReceiveWindowSize
                );
                if (NetLogFilter.logInfo) { Debug.Log("Transport Layer Initialized: Server"); }
            }

            if (IsClient && client == null)
            {
                // client
                client = new KcpClient(
                    () => OnClientConnected.Invoke(),
                    (message) => OnClientDataReceived.Invoke(message, (int)UDPChannels.Reliable),
                    () => OnClientDisconnected.Invoke()
                );
                if (NetLogFilter.logInfo) { Debug.Log("Transport Layer Initialized: Client"); }
            }
        }

        public static void Process()
        {
            if (IsSimulated) { return; }

            if (IsServer)
            {
                server.Tick();
            }

            if (IsClient)
            {
                client.Tick();
            }
        }

        public static void Shutdown()
        {
            if (NetLogFilter.logInfo) { Debug.Log("Transport Layer Shutdown"); }
            client?.Disconnect();
            server?.Stop();
        }

        public static bool Send(int connectionID, ArraySegment<byte> segment, Source source = Source.Default, int channelId = 1)
        {
            if (source == Source.Default) { return false; }

            if (source == Source.Server && IsServer)
            {
                switch (channelId)
                {
                    case (int)UDPChannels.Unreliable:
                        server.Send(connectionID, segment, KcpChannel.Unreliable);
                        break;
                    default:
                        server.Send(connectionID, segment, KcpChannel.Reliable);
                        break;
                }
                return true;
            }

            if (source == Source.Client && IsClient)
            {
                switch (channelId)
                {
                    case (int)UDPChannels.Unreliable:
                        client.Send(segment, KcpChannel.Unreliable);
                        break;
                    default:
                        client.Send(segment, KcpChannel.Reliable);
                        break;
                }
                return true;
            }

            return false;
        }

        public static bool SendSimulated(int connectionID, ArraySegment<byte> slice, Source source)
        {
            byte[] data = new byte[slice.Count];
            Array.Copy(slice.Array, slice.Offset, data, 0, slice.Count);

            if (source == Source.Server)
            {
                simulatedClientQueue.Enqueue(new SimulatedMessage(connectionID, SimulatedEventType.Data, data));
            }
            if (source == Source.Client)
            {
                simulatedServerQueue.Enqueue(new SimulatedMessage(connectionID, SimulatedEventType.Data, data));
            }
            return true;
        }
        public static bool ReceiveSimulatedMessage(Source source, out int connectionID, out TransportEvent transportEvent, out ArraySegment<byte> data)
        {
            if (source == Source.Server)
            {
                if (simulatedServerQueue.TryDequeue(out SimulatedMessage message))
                {
                    // convert Telepathy EventType to TransportEvent
                    transportEvent = (TransportEvent)message.eventType;

                    // assign rest of the values and return true
                    connectionID = message.connectionID;
                    if (message.data != null)
                    {
                        data = new ArraySegment<byte>(message.data);
                    }
                    else
                    {
                        data = default;
                    }
                    return true;
                }
            }

            if (source == Source.Client)
            {
                if (simulatedClientQueue.TryDequeue(out SimulatedMessage message))
                {
                    // convert Telepathy EventType to TransportEvent
                    transportEvent = (TransportEvent)message.eventType;

                    // assign rest of the values and return true
                    connectionID = -1;
                    if (message.data != null)
                    {
                        data = new ArraySegment<byte>(message.data);
                    }
                    else
                    {
                        data = default;
                    }
                    return true;
                }
            }

            connectionID = -1;
            transportEvent = TransportEvent.Data;
            data = default;
            return false;
        }

        #region - Client - 
        /// <summary>
        /// Notify subscribers when when this client establish a successful connection to the server
        /// <para>callback()</para>
        /// </summary>
        public static Action OnClientConnected = () => Debug.LogWarning("OnClientConnected called with no handler");

        /// <summary>
        /// Notify subscribers when this client receive data from the server
        /// <para>callback(ArraySegment&lt;byte&gt; data, int channel)</para>
        /// </summary>
        public static Action<ArraySegment<byte>, int> OnClientDataReceived = (data, channel) => Debug.LogWarning("OnClientDataReceived called with no handler");

        /// <summary>
        /// Notify subscribers when this client disconnects from the server
        /// <para>callback()</para>
        /// </summary>
        public static Action OnClientDisconnected = () => Debug.LogWarning("OnClientDisconnected called with no handler");

        public static bool Connected => client.connected;

        public static void Connect(string address, int port) => client.Connect(address, Port, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize);
        public static void SimulatedConnect(int connectionID) => simulatedServerQueue.Enqueue(new SimulatedMessage(connectionID, SimulatedEventType.Connected, default));
        public static void Disconnect() => client?.Disconnect();
        #endregion

        #region - Server -
        /// <summary>
        /// Notify subscribers when a client connects to this server
        /// <para>callback(int connId)</para>
        /// </summary>
        public static Action<int> OnServerConnected = (connId) => Debug.LogWarning("OnServerConnected called with no handler");

        /// <summary>
        /// Notify subscribers when this server receives data from the client
        /// <para>callback(int connId, ArraySegment&lt;byte&gt; data, int channel)</para>
        /// </summary>
        public static Action<int, ArraySegment<byte>, int> OnServerDataReceived = (connId, data, channel) => Debug.LogWarning("OnServerDataReceived called with no handler");

        /// <summary>
        /// Notify subscribers when a client disconnects from this server
        /// <para>callback(int connId)</para>
        /// </summary>
        public static Action<int> OnServerDisconnected = (connId) => Debug.LogWarning("OnServerDisconnected called with no handler");

        public Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = Port;
            return builder.Uri;
        }
        public static bool Active => server.IsActive();
        public static void StartServer(string address, int port) => server.Start((ushort)port);
        public static void StopServer() => server.Stop();
        public static string GetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public static bool DisconnectPeer(int connectionId) { server.Disconnect(connectionId); return true; }
        #endregion

        #region - Helper -
        // max message size
        public int GetMaxPacketSize(int channelId = 1)
        {
            // switch to kcp channel.
            // unreliable or reliable.
            // default to reliable just to be sure.
            switch (channelId)
            {
                case (int)UDPChannels.Unreliable:
                    return KcpConnection.UnreliableMaxMessageSize;
                default:
                    return KcpConnection.ReliableMaxMessageSize;
            }
        }

        // Server Statistics
        public static int GetAverageMaxSendRate() => server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (int)conn.MaxSendRate) / server.connections.Count
                : 0;
        public static int GetAverageMaxReceiveRate() => server.connections.Count > 0
                ? server.connections.Values.Sum(conn => (int)conn.MaxReceiveRate) / server.connections.Count
                : 0;
        private static int GetTotalSendQueue() => server.connections.Values.Sum(conn => conn.SendQueueCount);
        private static int GetTotalReceiveQueue() => server.connections.Values.Sum(conn => conn.ReceiveQueueCount);
        private static int GetTotalSendBuffer() => server.connections.Values.Sum(conn => conn.SendBufferCount);
        private static int GetTotalReceiveBuffer() => server.connections.Values.Sum(conn => conn.ReceiveBufferCount);

        // PrettyBytes function from DOTSNET
        // pretty prints bytes as KB/MB/GB/etc.
        // long to support > 2GB
        // divides by floats to return "2.5MB" etc.
        public static string PrettyBytes(long bytes)
        {
            // bytes
            if (bytes < 1024)
                return $"{bytes} B";
            // kilobytes
            else if (bytes < 1024L * 1024L)
                return $"{(bytes / 1024f):F2} KB";
            // megabytes
            else if (bytes < 1024 * 1024L * 1024L)
                return $"{(bytes / (1024f * 1024f)):F2} MB";
            // gigabytes
            return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
        }

        public static void OnLogStatistics(bool logToConsole, out string serverLog, out string clientLog)
        {
            serverLog = "";
            clientLog = "";

            if (IsServer && Active)
            {
                string log = "SERVER @ time: " + ServerTime.Time + "\n";
                log += $"  connections: {server.connections.Count}\n";
                log += $"  MaxSendRate (avg): {PrettyBytes(GetAverageMaxSendRate())}/s\n";
                log += $"  MaxRecvRate (avg): {PrettyBytes(GetAverageMaxReceiveRate())}/s\n";
                log += $"  SendQueue: {GetTotalSendQueue()}\n";
                log += $"  ReceiveQueue: {GetTotalReceiveQueue()}\n";
                log += $"  SendBuffer: {GetTotalSendBuffer()}\n";
                log += $"  ReceiveBuffer: {GetTotalReceiveBuffer()}\n\n";
                serverLog = log;
                if (logToConsole)
                {
                    Debug.Log(log);
                }
            }

            if (IsClient && Connected)
            {
                string log = "CLIENT @ time: " + ServerTime.Time + "\n";
                log += $"  MaxSendRate: {PrettyBytes(client.connection.MaxSendRate)}/s\n";
                log += $"  MaxRecvRate: {PrettyBytes(client.connection.MaxReceiveRate)}/s\n";
                log += $"  SendQueue: {client.connection.SendQueueCount}\n";
                log += $"  ReceiveQueue: {client.connection.ReceiveQueueCount}\n";
                log += $"  SendBuffer: {client.connection.SendBufferCount}\n";
                log += $"  ReceiveBuffer: {client.connection.ReceiveBufferCount}\n";
                log += $"  Ping: {ServerTime.Rtt*0.5f*1000:N0} ms\n\n";
                clientLog = log;
                if (logToConsole)
                {
                    Debug.Log(log);
                }
            }
        }
        #endregion
    }
}