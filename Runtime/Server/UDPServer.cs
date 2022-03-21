using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading;

namespace VaporNetworking
{

    public class UDPServer : MonoBehaviour, IServerMessages
    {
        private const string TAG = "<color=cyan><b>[Server]</b></color>";

        public static UDPServer instance;

        public bool isRunning;
        public bool isSimulated;

        private bool isInitialized;
        private bool isSetup;

        #region Inspector
        [Header("Logging"), Tooltip("Log level for network debugging")]
        public NetLogFilter.LogLevel logLevel;
        [Tooltip("Spews all debug logs that come from update methods. Warning: could be a lot of messages")]
        public bool debugSpew;
        [Tooltip("True if you want to recieve diagnostics on the messages being sent and recieved.")]
        public bool messageDiagnostics;

        public int maxServerPlayers = 2000;
        public string address = "127.0.0.1";
        public int port = 7777;

        [Tooltip("Server Target Framerate")]
        public int serverUpdateRate = 30;
        #endregion

        #region Connections
        public Dictionary<int, Peer> connectedPeers; // All peers connected to the server through the connectionID.

        //Network IDs for Objects
        private long counter = 0;
        public ulong NextNetworkID()
        {
            long id = Interlocked.Increment(ref counter);

            if (id == long.MaxValue)
            {
                throw new Exception("connection ID Limit Reached: " + id);
            }

            if (NetLogFilter.logDebug && NetLogFilter.spew) { Debug.LogFormat("Generated ID: {0}", id); }
            return (ulong)id;
        }
        #endregion

        #region Modules
        private Dictionary<Type, ServerModule> modules; // Modules added to the network manager
        private HashSet<Type> initializedModules; // set of initialized modules on the network manager
        #endregion

        #region Messaging
        private Dictionary<short, PacketHandler> handlers; // key value pair to handle messages.
        #endregion

        #region Event Handling
        public event Action Started;

        public event PeerActionHandler PeerConnected;
        public event PeerActionHandler PeerDisconnected;
        #endregion

        #region - Unity Methods and Initialization -
        protected void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            NetLogFilter.CurrentLogLevel = (int)logLevel;
            NetLogFilter.spew = debugSpew;
            NetLogFilter.messageDiagnostics = messageDiagnostics;
            Application.targetFrameRate = serverUpdateRate;
            QualitySettings.vSyncCount = 0;

            connectedPeers = new Dictionary<int, Peer>();
            this.modules = new Dictionary<Type, ServerModule>();
            initializedModules = new HashSet<Type>();
            handlers = new Dictionary<short, PacketHandler>();

            isInitialized = false;

            var modules = GetComponentsInChildren<ServerModule>();
            foreach (var mod in modules)
            {
                AddModule(mod);
            }

            DontDestroyOnLoad(gameObject);
        }

        protected void Start()
        {
            InitializeModules();
            SetupServer();

            StartCoroutine(WaitStartFrame());
        }

        private void SetupServer()
        {
            if (isSetup) { return; }
            UDPTransport.OnServerConnected = HandleConnect;
            UDPTransport.OnServerDataReceived = HandleData;
            UDPTransport.OnServerDisconnected = HandleDisconnect;


            RegisterHandler(OpCode.Ping, OnHandlePing);
            isSetup = true;
        }

        private IEnumerator WaitStartFrame()
        {
            yield return null;

            UDPTransport.Init(true, false, isSimulated);
            if (!isSimulated)
            {
                UDPTransport.StartServer(address, port);
            }
            isInitialized = true;

            Started?.Invoke();
            isRunning = true;
            Debug.Log("Server Started");
        }

        public void Shutdown()
        {
            UDPTransport.Shutdown();
            isRunning = false;
        }

        protected void OnDisable()
        {
            if (!isInitialized) { return; }
            if (instance != this) { return; }

            instance = null;

            isInitialized = false;

            handlers.Clear();

            UDPTransport.Shutdown();

            isRunning = false;
            Debug.Log("Server Shutdown");
        }

        protected void LateUpdate()
        {
            if (!isInitialized || !isSimulated) { return; }

            while (UDPTransport.ReceiveSimulatedMessage(UDPTransport.Source.Server, out int connectionId, out UDPTransport.TransportEvent transportEvent, out ArraySegment<byte> data))
            {
                switch (transportEvent)
                {
                    case UDPTransport.TransportEvent.Connected:
                        HandleConnect(connectionId);
                        break;
                    case UDPTransport.TransportEvent.Data:
                        HandleData(connectionId, data, 1);
                        break;
                    case UDPTransport.TransportEvent.Disconnected:
                        HandleDisconnect(connectionId);
                        break;
                }
            }
        }

        internal static void NetworkEarlyUpdate()
        {
            UDPTransport.ServerEarlyUpdate();
        }

        internal static void NetworkLateUpdate()
        {
            UDPTransport.ServerLateUpdate();
        }
        #endregion

        #region - Module Methods -
        /// <summary>
        ///     Adds a network module to the manager.
        /// </summary>
        /// <param name="module"></param>
        protected void AddModule(ServerModule module)
        {
            if (modules.ContainsKey(module.GetType()))
            {
                if (NetLogFilter.logWarn) { Debug.Log(string.Format("{0} Module has already been added. {1} || ({2})", TAG, module, Time.time)); }
            }
            modules.Add(module.GetType(), module);
        }

        /// <summary>
        ///     Adds a network module to the manager and initializes all modules.
        /// </summary>
        /// <param name="module"></param>
        public void AddModuleAndInitialize(ServerModule module)
        {
            AddModule(module);
            InitializeModules();
        }

        /// <summary>
        ///     Checks if the maanger has the module.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool HasModule(ServerModule module)
        {
            return modules.ContainsKey(module.GetType());
        }

        /// <summary>
        ///     Initializes all uninitialized modules
        /// </summary>
        /// <returns></returns>
        public bool InitializeModules()
        {
            while (true)
            {
                var changed = false;
                foreach (var mod in modules)
                {
                    // Module is already initialized
                    if (initializedModules.Contains(mod.Key)) { continue; }

                    mod.Value.Initialize(this);
                    initializedModules.Add(mod.Key);
                    changed = true;
                }

                // If nothing else can be initialized
                if (!changed)
                {
                    return !GetUninitializedModules().Any();
                }
            }
        }

        /// <summary>
        ///     Gets the module of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : ServerModule
        {
            modules.TryGetValue(typeof(T), out ServerModule module);
            if (module == null)
            {
                module = modules.Values.FirstOrDefault(m => m is T);
            }
            return module as T;
        }

        /// <summary>
        ///     Gets all initialized modules.
        /// </summary>
        /// <returns></returns>
        protected List<ServerModule> GetInitializedModules()
        {
            return modules
                .Where(m => initializedModules.Contains(m.Key))
                .Select(m => m.Value)
                .ToList();
        }

        /// <summary>
        ///     Gets all unitialized modules.
        /// </summary>
        /// <returns></returns>
        protected List<ServerModule> GetUninitializedModules()
        {
            return modules
                .Where(m => !initializedModules.Contains(m.Key))
                .Select(m => m.Value)
                .ToList();
        }
        #endregion

        #region - Remote Connection Methods -
        protected virtual void HandleConnect(int connectionID)
        {
            if (NetLogFilter.logDebug) { Debug.LogFormat("Connection ID: {0} Connected", connectionID); }

            var peer = new Peer(connectionID)
            {
                IsConnected = true
            };

            connectedPeers[peer.connectionID] = peer;

            OnPeerConnected(peer);
        }

        protected virtual void HandleDisconnect(int connectionID)
        {
            if (NetLogFilter.logDebug) { Debug.LogFormat("Connection ID: {0} Disconnected", connectionID); }
            connectedPeers.TryGetValue(connectionID, out Peer peer);
            if (peer == null) { return; }

            peer.Dispose();

            connectedPeers.Remove(peer.connectionID);

            OnPeerDisconnected(peer);
        }

        protected void OnPeerConnected(Peer peer)
        {
            PeerConnected?.Invoke(peer);
        }

        protected void OnPeerDisconnected(Peer peer)
        {
            PeerDisconnected?.Invoke(peer);
        }
        #endregion

        #region - Handle Message Methods -
        private void HandleData(int connectionID, ArraySegment<byte> buffer, int channelID)
        {
            if (connectedPeers.TryGetValue(connectionID, out Peer peer))
            {
                IncomingMessage msg = MessageHelper.FromBytes(buffer, peer);
                if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnReceive(msg.OpCode, buffer.Count, connectionID); }
                PeerMessageReceived(msg);
            }
        }

        /// <summary>
        ///     Called after the peer parses the message. Only assigned to the local player. Use <see cref="IncomingMessage.Sender"/> to determine what peer sent the message.
        /// </summary>
        /// <param name="msg"></param>
        private void PeerMessageReceived(IncomingMessage msg)
        {
            handlers.TryGetValue(msg.OpCode, out PacketHandler handler);
            handler?.Handle(msg);
        }

        /// <summary>
        ///     Register a message handler
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public PacketHandler RegisterHandler(PacketHandler handler)
        {
            if (handler == null)
            {
                return null;
            }

            if (handlers.Remove(handler.OpCode))
            {
                if (NetLogFilter.logInfo) { Debug.LogFormat("{0} Handler Overwritten", handler.OpCode); }
            }
            handlers.Add(handler.OpCode, handler);
            return handler;
        }

        /// <summary>
        ///     Register a method handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handlerMethod"></param>
        /// <returns></returns>
        public PacketHandler RegisterHandler(short opCode, IncomingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            return RegisterHandler(handler);
        }

        /// <summary>
        ///     Remove a specific message handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <returns></returns>
        public bool RemoveHandler(short opCode)
        {
            return handlers.Remove(opCode);
        }

        /// <summary>
        ///     Remove a specific message handler.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public bool RemoveHandler(PacketHandler handler)
        {
            if (handlers[handler.OpCode] != handler) { return false; }
            return handlers.Remove(handler.OpCode);
        }

        /// <summary>
        ///     Remove all message handlers.
        /// </summary>
        public void ClearHandlers()
        {
            handlers.Clear();
        }

        private void OnHandlePing(IncomingMessage msg)
        {
            ServerTime.OnServerPing(msg);
        }
        #endregion

        #region - Messaging Methods -
        public void Send(short opcode, ISerializablePacket packet)
        {
            using (PooledNetWriter w = NetWriterPool.Get())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var segment = w.ToArraySegment();
                if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnSend(opcode, segment.Count, connectedPeers.Values.Count); }
                if (!isSimulated)
                {
                    foreach (var peer in connectedPeers.Values)
                    {
                        peer.SendMessage(segment, UDPTransport.Source.Server);
                    }
                }
                else
                {
                    foreach (var peer in connectedPeers.Values)
                    {
                        peer.SendSimulatedMessage(segment, UDPTransport.Source.Server);
                    }
                }
            }
        }

        public void Send(Peer peer, short opcode, ISerializablePacket packet)
        {
            if (peer == null || !peer.IsConnected) { return; }

            using (PooledNetWriter w = NetWriterPool.Get())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var segment = w.ToArraySegment();
                if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnSend(opcode, segment.Count, 1); }
                if (!isSimulated)
                {
                    peer.SendMessage(segment, UDPTransport.Source.Server);
                }
                else
                {
                    peer.SendSimulatedMessage(segment, UDPTransport.Source.Server);
                }
            }
        }
        #endregion
    }
}