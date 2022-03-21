#if STEAMWORKS
using Steamworks;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking.Steam
{
    public class SteamNetworkLobby
    {
#if STEAMWORKS
        public const string LobbyData = "LD";
        public const string LobbyMemberData = "LMD";


        private SteamNetwork network;
        private Dictionary<short, PacketHandler> handlers; // key value pair to handle messages.

        // Callbacks
        //Lobby
        private Callback<LobbyChatUpdate_t> lobbyUpdated; // Called when a person enters or leaves the lobby.
        private Callback<LobbyChatMsg_t> lobbyMessage; // Called when someone sends a lobby message.
        private Callback<LobbyDataUpdate_t> lobbyDataUpdated; // Called when lobby metadata is updated.

        // Lobby Setup
        private Callback<LobbyMatchList_t> lobbyListReceived; //Callback from SteamMatchmaking.RequestLobbyList();
        private Callback<LobbyEnter_t> lobbyEnterReceived; // Callback from SteamMatchmaking.JoinLobby();
        private Callback<LobbyCreated_t> lobbyCreated; // Lobby creation callback after creating a lobby.

        // Events
        // Lobby
        public event SteamConnectionActionHandler LobbyLocalConnectionConnected; // When the local player joins the lobby this is called. 
        public event SteamConnectionActionHandler LobbyLocalConnectionDisconnected; // When the local player leaves the lobby this is called. 
        public event SteamConnectionActionHandler PeerInfoSetup; // Called to fill the local players peer info with data.

        public event SteamConnectionActionHandler HostSetup; // Called when the host is ready to setup, mostly for UI
        public event SteamConnectionActionHandler ClientSetup; // Called when the client is ready to setup, mostly for UI
        public event SteamConnectionActionHandler BecomeHost; // Called when a host leaves a lobby and the peer in the parameter becomes the host

        public event Action<CSteamID> LobbyPeerJoin; // When a client other than the local player joins the lobby, this is called.
        public event Action<CSteamID> LobbyPeerLeave; // When a client other than the local player leaves the lobby, this is called.

        public event Action<CSteamID, string> LobbyMemberDataUpdated;
        public event Action<string> LobbyDataUpdated;
        public event Action<List<CSteamID>> LobbyListReceived;
        public event Action<CSteamID> LobbyCreated;

        public SteamNetworkLobby(SteamNetwork network)
        {
            this.network = network;

            lobbyUpdated = Callback<LobbyChatUpdate_t>.Create(OnLobbyUpdated);
            lobbyMessage = Callback<LobbyChatMsg_t>.Create(OnLobbyMessaged);
            lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

            lobbyListReceived = Callback<LobbyMatchList_t>.Create(OnLobbyListReceived);
            lobbyEnterReceived = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);

            LobbyLocalConnectionConnected += OnLobbyLocalPeerConnected;
            LobbyLocalConnectionDisconnected += OnLobbyLocalPeerDisconnected;
        }

        #region - Connection -
        /// <summary>
        ///     Called when the local player joins the lobby.
        /// </summary>
        /// <param name="connection"></param>
        private void OnLobbyLocalPeerConnected(SteamConnection connection)
        {
            network.LocalConnection = connection;
            network.MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(connection.LobbyID);
            network.OnSetDefaultLobbyMemberData(connection);

            // Get Lobby Member Data
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(connection.LobbyID);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyMemberByIndex(connection.LobbyID, i);
                if (id == connection.SteamID) { continue; }
                connection.AddConnection(id);
            }

            //GetModule<AuthModule>().BeginAuthentication(authenticationLevel);
            if (connection.IsHost)
            {
                HostSetup?.Invoke(connection);
            }
            else
            {
                ClientSetup?.Invoke(connection);
            }

            connection.IsConnected = true;
        }

        /// <summary>
        ///     Called when the local player disconnects. Resets the status of the network manager to default.
        /// </summary>
        /// <param name="peer"></param>
        private void OnLobbyLocalPeerDisconnected(SteamConnection peer)
        {
            //isActive = false;
            //GetModule<AuthModule>().CancelAuthTicket();
            //if (GetModule<LobbyModule>().LobbyDisconnected != null)
            //{
            //    GetModule<LobbyModule>().LobbyDisconnected.Invoke();
            //}
            //peer.Dispose();

            //var modules = GetComponentsInChildren<NetworkModule>();
            //foreach (var mod in modules)
            //{
            //    mod.Unload(this);
            //    AddModule(mod);
            //}

            //isActive = true;
        }

        /// <summary>
        ///     Called when a non local peer joins the lobby.
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerConnected(CSteamID peerID, CSteamID lobbyID)
        {
            if (network.LocalConnection.AddConnection(peerID))
            {
                LobbyPeerJoin?.Invoke(peerID);
            }
        }

        /// <summary>
        ///     Called when a non local peer leaves the lobby.
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerDisconnected(CSteamID peerID, CSteamID lobbyID)
        {
            if (!network.LocalConnection.connectedPeers.ContainsKey(peerID))
            {
                if (NetLogFilter.logInfo) { Debug.Log($"Peer has already left {peerID} || {Time.time}"); }
                return;
            }

            
            //CSteamID checkHost = SteamMatchmaking.GetLobbyOwner(this.peer.LobbyID);
            //if (host != checkHost)
            //{
            //    host = checkHost;
            //    if (useDissonance)
            //    {
            //        // if this game is using dissonance stop it and then reinvoke it.
            //        GetModule<DissonanceModule>().StopDissonance.Invoke();
            //    }
            //    if (host == this.peer.SteamID)
            //    {
            //        isHost = true;
            //        if (BecomeHost != null)
            //        {
            //            BecomeHost.Invoke(this.peer);
            //        }
            //        else
            //        {
            //            if (NetLogFilter.logError) { Debug.Log(string.Format("{0} <color=red>Become Host Failed To Invoke.</color> ({1})", TAG, Time.time)); }
            //        }
            //    }
            //    else
            //    {
            //        if (useDissonance)
            //        {
            //            // TODO read dissonance to see if i really need to stop the client too or just the server instance.
            //            GetModule<DissonanceModule>().InitClient.Invoke(this.peer.SteamID);
            //        }
            //    }
            //}

            //// close the auth session if the host. TODO potentially could make it optional betwen authing just the host or all members of the lobby.
            //if (isHost) { SteamUser.EndAuthSession(peer.SteamID); }


            if (network.LocalConnection.RemoveConnection(peerID))
            {
                LobbyPeerLeave?.Invoke(peerID);
            }

            if (NetLogFilter.logInfo) { Debug.Log($"Peer Disconnected {peerID} || {Time.time}"); }
        }
        #endregion

        #region - Lobby Messaging -
        /// <summary>
        ///     Register a method handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handlerMethod"></param>
        /// <returns></returns>
        public PacketHandler RegisterHandler(short opCode, IncomingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            if (handler == null)
            {
                return null;
            }

            if (handlers.Remove(handler.OpCode))
            {
                if (NetLogFilter.logInfo) { Debug.Log($"{handler.OpCode} Handler Overwritten"); }
            }
            handlers.Add(handler.OpCode, handler);
            return handler;
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
        ///     Remove all message handlers.
        /// </summary>
        public void ClearHandlers()
        {
            handlers.Clear();
        }

        /// <summary>
        ///     Handles a message received from the steam lobby message system. Max size is 1024 bytes.
        /// </summary>
        /// <param name="msg"></param>
        private void OnLobbyMessageReceived(IncomingMessage msg)
        {
            handlers.TryGetValue(msg.OpCode, out PacketHandler handler);
            handler?.Handle(msg);
        }

        /// <summary>
        ///     Sends a message over the steam lobby message system. Max size is 1024 bytes.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="qos"></param>
        public void Send(short opcode, ISerializablePacket packet)/* OutgoingMessage msg)*/
        {
            if (!network.LocalConnection.InLobby) { return; }

            using (PooledNetWriter w = NetWriterPool.Get())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var array = w.ToArray();
                SteamMatchmaking.SendLobbyChatMsg(network.LocalConnection.LobbyID, array, array.Length);
            }
        }
        #endregion

        #region - Steam Callbacks -
        /// <summary>
        ///     Called when a player joins or leaves the lobby.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyUpdated(LobbyChatUpdate_t pCallback)
        {
            var state = (EChatMemberStateChange)pCallback.m_rgfChatMemberStateChange;
            if (NetLogFilter.logInfo) { Debug.Log($"Lobby updated by {pCallback.m_ulSteamIDUserChanged}. ({Time.time})"); }
            switch (state)
            {
                case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                    OnPeerConnected(new CSteamID(pCallback.m_ulSteamIDUserChanged), new CSteamID(pCallback.m_ulSteamIDLobby));
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeLeft:
                    OnPeerDisconnected(new CSteamID(pCallback.m_ulSteamIDUserChanged), new CSteamID(pCallback.m_ulSteamIDLobby));
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeDisconnected:
                    OnPeerDisconnected(new CSteamID(pCallback.m_ulSteamIDUserChanged), new CSteamID(pCallback.m_ulSteamIDLobby));
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeKicked:
                    break;
                case EChatMemberStateChange.k_EChatMemberStateChangeBanned:
                    break;
            }
        }

        /// <summary>
        ///     Called when the lobby receives a message.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyMessaged(LobbyChatMsg_t pCallback)
        {
            if (NetLogFilter.logInfo) { Debug.Log($"Recieved message from {SteamFriends.GetFriendPersonaName(new CSteamID(pCallback.m_ulSteamIDUser))}. ({Time.time})"); }

            byte[] msg = new byte[1024];
            SteamMatchmaking.GetLobbyChatEntry(network.LocalConnection.LobbyID, (int)pCallback.m_iChatID, out CSteamID id, msg, msg.Length, out EChatEntryType type);
            var segment = new ArraySegment<byte>(msg);
            var incMsg = MessageHelper.FromBytes(segment, network.LocalConnection.connectedPeers[id]);
            OnLobbyMessageReceived(incMsg);
        }

        /// <summary>
        ///     Called when either the lobby or a lobby member has their data updated.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
        {
            if (pCallback.m_ulSteamIDLobby != pCallback.m_ulSteamIDMember)
            {
                if (NetLogFilter.logInfo) { Debug.Log($"Lobby Member Data Updated | ({Time.time})"); }
                CSteamID lobby = new CSteamID(pCallback.m_ulSteamIDLobby);
                CSteamID member = new CSteamID(pCallback.m_ulSteamIDMember);
                string info = SteamMatchmaking.GetLobbyMemberData(lobby, member, LobbyMemberData);
                LobbyMemberDataUpdated?.Invoke(member, info);
            }
            else
            {
                if (NetLogFilter.logInfo) { Debug.Log($"Lobby Data Updated | ({Time.time})"); }
                CSteamID lobby = new CSteamID(pCallback.m_ulSteamIDLobby);
                string data = SteamMatchmaking.GetLobbyData(lobby, LobbyData);
                LobbyDataUpdated?.Invoke(data);
            }
        }


        /// <summary>
        ///     Called when the player recieves a steam lobby list after requestng the lobbies. Determines if the list is for a playlist or a custom game.
        /// <para/>
        ///     If it is a playlist determines if a valid lobby exists or if one must be created. If one is created that player is host.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyListReceived(LobbyMatchList_t pCallback)
        {
            if (NetLogFilter.logInfo) { Debug.Log($"Lobby list recieved, getting matches | ({Time.time})"); }
            List<CSteamID> validIds = new List<CSteamID>();
            for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                if (id.IsValid())
                {
                    validIds.Add(id);
                }
            }

            LobbyListReceived?.Invoke(validIds);
        }

        /// <summary>
        ///     Called when a player sucessfully finds a lobby to join. Starts the tranistion to the lobby scene: <see cref="HVR_MultiplayerSetup.LobbyScene"/>
        /// <para/>
        ///     Assigns the scene load event <see cref="JoinNewMatch(Scene, LoadSceneMode)"/> to be called when the lobby scene is loaded.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyEntered(LobbyEnter_t pCallback)
        {
            if (NetLogFilter.logInfo) { Debug.Log($"Lobby Entered | ({Time.time})"); }
            CSteamID id = SteamUser.GetSteamID();
            if (id.IsValid())
            {
                SteamConnection conn = new SteamConnection(id, new CSteamID(pCallback.m_ulSteamIDLobby));
                LobbyLocalConnectionConnected?.Invoke(conn);
            }
            else
            {
                if (NetLogFilter.logError) { Debug.Log("<color=red>SteamID is Invalid</color>"); }
            }
        }

        /// <summary>
        ///     Callback for when a steam lobby is created. Assign connection info and match info here for players to read when they connect.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnLobbyCreated(LobbyCreated_t pCallback)
        {
            // record which lobby we're in
            if (NetLogFilter.logInfo) { Debug.Log($"Lobby Created | ({Time.time})"); }
            if (pCallback.m_eResult == EResult.k_EResultOK)
            {
                // success
                if (NetLogFilter.logInfo) { Debug.Log($"LobbyID: {pCallback.m_ulSteamIDLobby}. ({Time.time})"); }
                LobbyCreated?.Invoke(new CSteamID(pCallback.m_ulSteamIDLobby));
            }
            else
            {
                if (NetLogFilter.logInfo) { Debug.Log($"Failed to create lobby (lost connection to Steam back-end servers) ({Time.time})"); }
            }
        }
        #endregion
#endif
    }
}