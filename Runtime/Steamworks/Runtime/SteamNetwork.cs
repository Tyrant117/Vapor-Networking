#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
#if STEAMWORKS
using Steamworks;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace VaporNetworking.Steam
{
    public class SteamNetwork : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Button]
#endif
        public void DefineSteamworks()
        {
#if UNITY_EDITOR
            UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone, out var defines);
            var defList = new List<string>(defines);
            if (!defList.Contains("STEAMWORKS"))
            {
                defList.Add("STEAMWORKS");
            }
            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone, defList.ToArray());
#endif
        }

#if STEAMWORKS
        internal static class ArrayPool
        {
            [ThreadStatic]
            private static IntPtr[] pointerBuffer;

            public static IntPtr[] GetPointerBuffer()
            {
                if (pointerBuffer == null)
                    pointerBuffer = new IntPtr[256];

                return pointerBuffer;
            }
        }




        public static SteamNetwork instance;

        private bool isInitialized;
        private bool isSetup;
        private bool isRunning;
        private NetWriter w;

        // Used in reading messaged from the steam connection.
        private byte[] msgBuffer = new byte[1200]; // Base buffer a new message is written to

        #region Inspector
        [Header("Logging"), Tooltip("Log level for network debugging")]
        public NetLogFilter.LogLevel logLevel;
        [Tooltip("Spews all debug logs that come from update methods. Warning: could be a lot of messages")]
        public bool debugSpew;
        [Tooltip("True if you want to recieve diagnostics on the messages being sent and recieved.")]
        public bool messageDiagnostics;
        #endregion

        #region Lobby
        public SteamNetworkLobby Lobby { get; private set; }
        public int MaxPlayers { get; set; }

        public Func<string> SetDefaultLobbyMemberData;
        #endregion

        #region Connections
        public SteamConnection LocalConnection { get; set; }

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
        private Dictionary<Type, SteamNetworkModule> modules; // Modules added to the network manager
        private HashSet<Type> initializedModules; // set of initialized modules on the network manager
        #endregion

        #region Event Handling
        public event Action Started;

        public event PeerActionHandler PeerConnected;
        public event PeerActionHandler PeerDisconnected;
        #endregion

        #region --- Unity Methods and Initialization ---
        protected void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            w = new NetWriter();
            NetLogFilter.CurrentLogLevel = (int)logLevel;
            NetLogFilter.spew = debugSpew;
            NetLogFilter.messageDiagnostics = messageDiagnostics;
            QualitySettings.vSyncCount = 0;

            this.modules = new Dictionary<Type, SteamNetworkModule>();
            initializedModules = new HashSet<Type>();

            isInitialized = false;

            var modules = GetComponentsInChildren<SteamNetworkModule>();
            foreach (var mod in modules)
            {
                AddModule(mod);
            }

            DontDestroyOnLoad(gameObject);
        }

        public void Startup()
        {
            InitializeModules();
            SetupLobby();

            SteamNetworkingUtils.InitRelayNetworkAccess();
            isInitialized = true;

            Started?.Invoke();
            isRunning = true;
            Debug.Log("Server Started");
        }

        private void SetupLobby()
        {
            if (isSetup) { return; }
            Lobby = new SteamNetworkLobby(this);


            isSetup = true;
        }

        public void Shutdown()
        {
            UnloadModules();

            isRunning = false;
            isInitialized = false;
            isSetup = false;
            Debug.Log("Server Shutdown");
        }

        protected void LateUpdate()
        {
            if (!isRunning) { return; }

            IntPtr[] nativeMessages = ArrayPool.GetPointerBuffer();
            int msgCount = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, nativeMessages, 256);
            for (int i = 0; i < msgCount; i++)
            {
                RecieveMessage(nativeMessages[i]);
            }

            //while (SteamNetworking.IsP2PPacketAvailable(out uint _PacketSize) && _PacketSize > 0)
            //{
            //    // size of the new buffer
            //    if (SteamNetworking.ReadP2PPacket(msgBuffer, _PacketSize, out uint bufferSize, out CSteamID _IDofSender))
            //    {
            //        ArraySegment<byte> segment = new ArraySegment<byte>(msgBuffer, 0, (int)bufferSize);
            //        HandleData(_IDofSender, segment);
            //    }
            //}
        }

        private void RecieveMessage(IntPtr msgPtr)
        {
            var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);
            try
            {
                Array.Resize(ref msgBuffer, msg.m_cbSize);
                Marshal.Copy(msg.m_pData, msgBuffer, 0, msg.m_cbSize);
                ArraySegment<byte> segment = new ArraySegment<byte>(msgBuffer, 0, msg.m_cbSize);

                HandleData(msg.m_identityPeer.GetSteamID(), segment);
            }
            finally
            {
                msg.Release();
            }

        }
        #endregion

        #region --- Module Methods ---
        /// <summary>
        ///     Adds a network module to the manager.
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(SteamNetworkModule module)
        {
            if (modules.ContainsKey(module.GetType()))
            {
                if (NetLogFilter.logWarn) { Debug.Log($"Module has already been added. {module} || ({Time.time})"); }
            }
            modules.Add(module.GetType(), module);
        }

        /// <summary>
        ///     Adds a network module to the manager and initializes all modules.
        /// </summary>
        /// <param name="module"></param>
        public void AddModuleAndInitialize(SteamNetworkModule module)
        {
            AddModule(module);
            InitializeModules();
        }

        /// <summary>
        ///     Checks if the maanger has the module.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool HasModule(SteamNetworkModule module)
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
                    if (!mod.Value.Dependencies.All(d => initializedModules.Any(d.IsAssignableFrom))) { continue; }

                    mod.Value.Network = this;
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

        public void UnloadModules()
        {
            foreach (var mod in modules)
            {
                mod.Value.Unload(this);
            }
        }

        /// <summary>
        ///     Gets the module of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : SteamNetworkModule
        {
            modules.TryGetValue(typeof(T), out SteamNetworkModule module);
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
        public List<SteamNetworkModule> GetInitializedModules()
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
        public List<SteamNetworkModule> GetUninitializedModules()
        {
            return modules
                .Where(m => !initializedModules.Contains(m.Key))
                .Select(m => m.Value)
                .ToList();
        }
        #endregion

        #region - Lobby -
        public void OnSetDefaultLobbyMemberData(SteamConnection conn)
        {
            var result = SetDefaultLobbyMemberData?.Invoke();
            SteamMatchmaking.SetLobbyMemberData(conn.LobbyID, SteamNetworkLobby.LobbyMemberData, result);
        }
        #endregion

        #region --- Handle Message Methods ---
        private void HandleData(CSteamID sender, ArraySegment<byte> buffer)
        {
            if (LocalConnection == null) { return; }

            IncomingMessage msg = MessageHelper.FromBytes(buffer, LocalConnection);
            //if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnReceive(msg.OpCode, buffer.Count, connectionID); }

            if (LocalConnection.SteamID == sender)
            {
                LocalConnection.PeerMessageReceived(msg);
            }
            else if (LocalConnection.connectedPeers.TryGetValue(sender, out var conn))
            {
                conn.PeerMessageReceived(msg);
            }
        }

        /// <summary>
        ///     Register a method handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handlerMethod"></param>
        /// <returns></returns>
        public PacketHandler RegisterHandler(CSteamID peerID, short opCode, IncomingMessageHandler handlerMethod)
        {
            Dictionary<short, PacketHandler> handlers = null;
            if (LocalConnection.SteamID == peerID)
            {
                handlers = LocalConnection.handlers;
            }
            else if (LocalConnection.connectedPeers.TryGetValue(peerID, out var conn))
            {
                handlers = conn.handlers;
            }
            if(handlers == null) { return null; }

            var handler = new PacketHandler(opCode, handlerMethod);
            if (handlers.Remove(handler.OpCode))
            {
                if (NetLogFilter.logInfo) { Debug.LogFormat("{0} Handler Overwritten", handler.OpCode); }
            }
            handlers.Add(handler.OpCode, handler);
            return handler;
        }

        /// <summary>
        ///     Remove a specific message handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <returns></returns>
        public bool RemoveHandler(CSteamID peerID, short opCode)
        {
            if (LocalConnection.SteamID == peerID)
            {
                return LocalConnection.handlers.Remove(opCode);
            }
            else if (LocalConnection.connectedPeers.TryGetValue(peerID, out var conn))
            {
                return conn.handlers.Remove(opCode);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///     Remove all message handlers.
        /// </summary>
        public void ClearHandlers(CSteamID peerID)
        {
            if (LocalConnection.SteamID == peerID)
            {
                LocalConnection.handlers.Clear();
            }
            else if (LocalConnection.connectedPeers.TryGetValue(peerID, out var conn))
            {
                conn.handlers.Clear();
            }
        }
        #endregion

        public int RegisterResponse(ResponseCallback callback, int timeout = 5)
        {
            return LocalConnection.RegisterResponse(callback, timeout);
        }
#endif
    }
}