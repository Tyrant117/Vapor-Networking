using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace VaporNetworking
{
    public class UDPClient : MonoBehaviour
    {
        public const string TAG = "<color=cyan><b>[Client]</b></color>";

        public static UDPClient instance;

        public bool connectOnStart;
        public bool isSimulated;

        private bool isInitialized;

        #region Inspector
        [Tooltip("Log level of this script")]
        public NetLogFilter.LogLevel logLevel;
        [Tooltip("Spews debug info that comes from update calls. Could be a lot of messages.")]
        public bool spewDebug;
        [Tooltip("True if you want to recieve diagnostics on the messages being sent and recieved.")]
        public bool messageDiagnostics;

        [Tooltip("Address to the server")]
        public string gameServerIp = "127.0.0.1";

        [Tooltip("Port of the server")]
        public int gameServerPort = 7777;
        #endregion

        private readonly bool retryOnTimeout = true;

        #region Connections
        protected int connectionID = -1;
        private float stopConnectingTime;
        private bool isAttemptingReconnect;

        public Peer ServerPeer { get; protected set; }

        private readonly float pingFrequency = 2.0f;
        private double lastPingTime;
        #endregion

        #region Modules
        private Dictionary<Type, ClientModule> modules; // Modules added to the network manager
        private HashSet<Type> initializedModules; // set of initialized modules on the network manager
        #endregion

        #region Messaging
        private Dictionary<short, PacketHandler> handlers; // key value pair to handle messages.
        #endregion

        #region Current Connection
        private ConnectionStatus status;
        /// <summary>
        ///     Current connections status. If changed invokes StatusChanged event./>
        /// </summary>
        public ConnectionStatus Status
        {
            get { return status; }
            set
            {
                if (status != value && StatusChanged != null)
                {
                    status = value;
                    StatusChanged.Invoke(status);
                    return;
                }
                status = value;
            }
        }

        /// <summary>
        ///     True if we are connected to another socket
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        ///     True if we are trying to connect to another socket
        /// </summary>
        public bool IsConnecting { get; protected set; }

        /// <summary>
        ///     IP Address of the connection
        /// </summary>
        public string ConnectionIP { get; protected set; }

        /// <summary>
        ///     Port of the connection
        /// </summary>
        public int ConnectionPort { get; protected set; }
        #endregion

        #region Event Handling
        /// <summary>
        ///     Event is invoked when we successfully connect to another socket.
        /// </summary>
        public event Action Connected;

        /// <summary>
        ///     Event is invoked when we are disconnected from another socket.
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        ///     Event is invoked when the connection status changes.
        /// </summary>
        public event Action<ConnectionStatus> StatusChanged;
        #endregion

        #region - Unity Methods and Initialization -
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            NetLogFilter.CurrentLogLevel = (int)logLevel;
            NetLogFilter.spew = spewDebug;
            NetLogFilter.messageDiagnostics = messageDiagnostics;

            handlers = new Dictionary<short, PacketHandler>();
            this.modules = new Dictionary<Type, ClientModule>();
            initializedModules = new HashSet<Type>();

            isInitialized = false;

            Connected += OnConnected;
            Disconnected += OnDisconnected;

            var modules = GetComponentsInChildren<ClientModule>();
            foreach (var mod in modules)
            {
                AddModule(mod);
            }

            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            InitializeModules();

            UDPTransport.OnClientConnected = HandleConnect;
            UDPTransport.OnClientDataReceived = HandleData;
            UDPTransport.OnClientDisconnected = HandleDisconnect;

            RegisterHandler(OpCode.Pong, OnHandlePong);

            if (connectOnStart)
            {
                StartCoroutine(StartConnection());
            }

            UDPTransport.Init(false, true, isSimulated);
            isInitialized = true;
        }

        public void StartupClient()
        {
            StartCoroutine(StartConnection());
        }

        private IEnumerator StartConnection()
        {
            // Add a delay for other connections
            yield return new WaitForSeconds(0.2f);
            Connect(gameServerIp, gameServerPort);
        }

        protected void Update()
        {
            if (connectionID == -1) { return; }

            if (IsConnecting && !IsConnected)
            {
                // Attempt Connection
                if (Time.time > stopConnectingTime)
                {
                    StopConnecting(true);
                    return;
                }
                Status = ConnectionStatus.Connecting;
            }

            if (Status == ConnectionStatus.Connected && Time.time - lastPingTime >= pingFrequency)
            {
                Send(OpCode.Ping, ServerTime.Ping);
                lastPingTime = Time.timeAsDouble;
            }
        }

        protected void LateUpdate()
        {
            if (!isInitialized || !isSimulated) { return; }

            while (UDPTransport.ReceiveSimulatedMessage(UDPTransport.Source.Client, out int connID, out UDPTransport.TransportEvent transportEvent, out ArraySegment<byte> data))
            {
                switch (transportEvent)
                {
                    case UDPTransport.TransportEvent.Connected:
                        HandleConnect();
                        break;
                    case UDPTransport.TransportEvent.Data:
                        HandleData(data, 1);
                        break;
                    case UDPTransport.TransportEvent.Disconnected:
                        HandleDisconnect();
                        break;
                }
            }
        }

        internal static void NetworkEarlyUpdate()
        {
            // process all incoming messages first before updating the world
            UDPTransport.ClientEarlyUpdate();
        }

        internal static void NetworkLateUpdate()
        {
            UDPTransport.ClientLateUpdate();
        }

        private void OnApplicationQuit()
        {
            UDPTransport.Disconnect();
        }
        #endregion

        #region - Module Methods -
        /// <summary>
        ///     Adds a network module to the manager.
        /// </summary>
        /// <param name="module"></param>
        protected void AddModule(ClientModule module)
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
        public void AddModuleAndInitialize(ClientModule module)
        {
            AddModule(module);
            InitializeModules();
        }

        /// <summary>
        ///     Checks if the maanger has the module.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool HasModule(ClientModule module)
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

                    // Not all dependencies have been initialized. Wait until they are.
                    //if (!mod.Value.Dependencies.All(d => initializedModules.Any(d.IsAssignableFrom))) { continue; }

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
        public T GetModule<T>() where T : ClientModule
        {
            modules.TryGetValue(typeof(T), out ClientModule module);
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
        protected List<ClientModule> GetInitializedModules()
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
        protected List<ClientModule> GetUninitializedModules()
        {
            return modules
                .Where(m => !initializedModules.Contains(m.Key))
                .Select(m => m.Value)
                .ToList();
        }
        #endregion

        #region - Connection Methods -
        /// <summary>
        ///     Starts connecting to another socket. Default timeout of 10s.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public UDPClient Connect(string ip, int port)
        {
            Connect(ip, port, 10);
            return this;
        }
        /// <summary>
        ///     Starts connecting to another socket with a specified timeout.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeout">Milliseconds</param>
        /// <returns></returns>
        public UDPClient Connect(string ip, int port, int timeout)
        {
            connectionID = 0;
            stopConnectingTime = Time.time + timeout;
            ConnectionIP = ip;
            ConnectionPort = port;


            IsConnecting = true;
            if (!isSimulated)
            {
                UDPTransport.Connect(ip, port);
            }
            else
            {
                UDPTransport.SimulatedConnect(connectionID);
                HandleConnect();
            }
            return this;
        }

        /// <summary>
        ///     Disconnect the <see cref="ClientSocket"/> from the <see cref="ServerSocket"/>.
        /// </summary>
        public void Disconnect()
        {
            UDPTransport.Disconnect();

            HandleDisconnect();
        }

        /// <summary>
        ///     Disconnects and attempts connecting again.
        /// </summary>
        public void Reconnect()
        {
            isAttemptingReconnect = true;
            Disconnect();
            Connect(ConnectionIP, ConnectionPort);
        }

        /// <summary>
        ///     Stops trying to connect to the socket
        /// </summary>
        private void StopConnecting(bool timedOut = false)
        {
            IsConnecting = false;
            Status = ConnectionStatus.Disconnected;
            if (timedOut && retryOnTimeout)
            {
                if (NetLogFilter.logInfo) { Debug.LogFormat("{2} Retrying to connect to server at || {0}:{1}", gameServerIp, gameServerPort, TAG); }
                Connect(gameServerIp, gameServerPort);
            }
        }

        protected virtual void HandleConnect()
        {
            IsConnecting = false;
            IsConnected = true;

            Debug.Log("Connected");

            Status = ConnectionStatus.Connected;

            ServerPeer = new Peer(connectionID)
            {
                IsConnected = true
            };

            OnConnectionHandled();
        }

        protected void OnConnectionHandled()
        {
            Connected?.Invoke();
        }

        private void HandleDisconnect()
        {
            Status = ConnectionStatus.Disconnected;
            IsConnected = false;
            UDPTransport.Disconnect();
            connectionID = -1;

            if (ServerPeer != null)
            {
                ServerPeer.Dispose();
                ServerPeer.IsConnected = false;
            }
            Disconnected?.Invoke();
        }

        private void OnDisconnected()
        {
            if (NetLogFilter.logInfo) { Debug.LogFormat("Disconnected from || {0}:{1}", gameServerIp, gameServerPort); }

            if (!isAttemptingReconnect)
            {

            }
            isAttemptingReconnect = false;
        }

        private void OnConnected()
        {
            if (NetLogFilter.logInfo) { Debug.LogFormat("Connected to || {0}:{1}", gameServerIp, gameServerPort); }
        }
        #endregion

        #region - Handle Message Methods -
        private void HandleData(ArraySegment<byte> buffer, int channelID)
        {
            if (ServerPeer == null) { return; }

            IncomingMessage msg = MessageHelper.FromBytes(buffer, ServerPeer);
            if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnReceive(msg.OpCode, buffer.Count, connectionID); }

            PeerMessageReceived(msg);
        }

        /// <summary>
        ///     Called after the peer parses the message. Only assigned to the local player. Use <see cref="IncomingMessage.Sender"/> to determine what peer sent the message.
        /// </summary>
        /// <param name="msg"></param>
        protected void PeerMessageReceived(IncomingMessage msg)
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
            return RegisterHandler(new PacketHandler(opCode, handlerMethod));
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

        private void OnHandlePong(IncomingMessage msg)
        {
            ServerTime.OnClientPong(msg);
        }
        #endregion

        #region --- Messaging Methods ---
        private void Send(ArraySegment<byte> msg)
        {
            if (!isSimulated)
            {
                ServerPeer.SendMessage(msg, UDPTransport.Source.Client);
            }
            else
            {
                ServerPeer.SendSimulatedMessage(msg, UDPTransport.Source.Client);
            }
        }

        /// <summary>
        ///     Sends a message to server.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="qos"></param>
        public void Send(short opcode, ISerializablePacket packet)
        {
            if (!IsConnected) { return; }

            using (PooledNetWriter w = NetWriterPool.Get())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var segment = w.ToArraySegment();
                if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnSend(opcode, segment.Count, 1); }
                Send(segment);
            }
        }

        public int RegisterResponse(ResponseCallback callback, int timeout = 5)
        {
            return ServerPeer.RegisterResponse(callback, timeout);
        }
        #endregion
    }
}