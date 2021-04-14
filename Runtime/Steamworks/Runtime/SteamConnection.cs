#if STEAMWORKS
using Steamworks;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace VaporNetworking.Steam
{
    public delegate void SteamConnectionActionHandler(SteamConnection peer);

    public class SteamConnection : Peer
    {
#if STEAMWORKS
        public CSteamID SteamID { get; private set; }
        public CSteamID LobbyID { get; private set; }

        public SteamNetworkingIdentity SteamIdentity { get; set; }

        public Dictionary<CSteamID, SteamConnection> connectedPeers = new Dictionary<CSteamID, SteamConnection>();

        public CSteamID HostID => SteamMatchmaking.GetLobbyOwner(LobbyID);
        public bool IsAuthenticated { get; private set; }
        public bool InLobby => LobbyID.IsLobby();
        public bool IsHost => SteamID == HostID;
        public bool IsLocal => network.LocalConnection == this;

        public bool IsAlive(float timeout) => ServerTime.Time - lastMessageTime < timeout;
        public double lastMessageTime;

        public SteamNetworkingIdentity identity;

        public SteamNetworkIdentity MainObject { get; set; }
        public readonly HashSet<SteamNetworkIdentity> ownedObjects = new HashSet<SteamNetworkIdentity>();

        private SteamNetwork network;
        public Dictionary<short, PacketHandler> handlers; // key value pair to handle messages.

        // Sessions
        private Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequest; // Called sometimes when a P2P message is recieved for connection.
        private Callback<SteamNetworkingMessagesSessionFailed_t> failConnection; // Called sometimes when a P2P message fails to make a connection.

        public SteamConnection(CSteamID steamID, CSteamID lobbyID) : base(0)
        {
            SteamID = steamID;
            LobbyID = lobbyID;
            lastMessageTime = ServerTime.Time;
            handlers = new Dictionary<short, PacketHandler>();

            sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            failConnection = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionConnectFail);
        }

        protected override void Dispose(bool dispose)
        {
            base.Dispose(dispose);
            sessionRequest.Dispose();
            failConnection.Dispose();
        }

        public void AddOwnedObject(SteamNetworkIdentity obj)
        {
            ownedObjects.Add(obj);
        }

        public void RemoveOwnedObject(SteamNetworkIdentity obj)
        {
            ownedObjects.Remove(obj);
        }

        public void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            HashSet<SteamNetworkIdentity> tmp = new HashSet<SteamNetworkIdentity>(ownedObjects);
            foreach (SteamNetworkIdentity netIdentity in tmp)
            {
                if (netIdentity != null)
                {
                    SteamNetwork.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            ownedObjects.Clear();
        }


        #region - Connection -
        public bool AddConnection(CSteamID peerID)
        {
            if (peerID == SteamID) { return false; }
            if (connectedPeers.ContainsKey(peerID)) { return true; }

            connectedPeers.Add(peerID, new SteamConnection(peerID, LobbyID));
            AttemptConnection(peerID);
            return true;
        }

        public bool RemoveConnection(CSteamID id)
        {
            if (connectedPeers.TryGetValue(id, out var conn))
            {
                SteamNetworkingMessages.CloseSessionWithUser(ref conn.identity);
            }
            return connectedPeers.Remove(id);
        }

        /// <summary>
        ///     Begins a p2p conenctiona attempt with the target.
        /// </summary>
        /// <param name="target"></param>
        private void AttemptConnection(CSteamID target)
        {
            SteamNetworkingIdentity id = new SteamNetworkingIdentity();
            id.SetSteamID(target);
            Send(OpCode.AttemptP2PConnection, new IdentityPacket() { identity = SteamID.m_SteamID }, id);
        }

        /// <summary>
        ///     Called when a request to create a p2p connection occurs.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t pCallback)
        {
            if (NetLogFilter.logInfo) { Debug.Log($"Session request from {pCallback.m_identityRemote.GetSteamID()}. ({Time.time})"); }

            if (connectedPeers.TryGetValue(pCallback.m_identityRemote.GetSteamID(), out var conn))
            {
                conn.IsConnected = true;
                conn.identity = pCallback.m_identityRemote;
                SteamNetworkingMessages.AcceptSessionWithUser(ref conn.identity);

                //SteamNetworking.AcceptP2PSessionWithUser(pCallback.m_identityRemote.GetSteamID());
            }
        }

        /// <summary>
        ///     Called if failure to create a p2p connection occurs.
        /// </summary>
        /// <param name="pCallback"></param>
        private void OnSessionConnectFail(SteamNetworkingMessagesSessionFailed_t pCallback)
        {
            if (NetLogFilter.logError) { Debug.Log($"<color=red>Session request from {pCallback.m_info.m_identityRemote.GetSteamID()} failed.</color> | Error {pCallback.m_info.m_szEndDebug} ({Time.time})"); }
        }
        #endregion

        /// <summary>
        ///     Called after the peer parses the message. Only assigned to the local player. Use <see cref="IncomingMessage.Sender"/> to determine what peer sent the message.
        /// </summary>
        /// <param name="msg"></param>
        public void PeerMessageReceived(IncomingMessage msg)
        {
            handlers.TryGetValue(msg.OpCode, out PacketHandler handler);
            handler?.Handle(msg);
        }

        /// <summary>
        ///     Sends a message to the target.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="qos"></param>
        public void Send(short opcode, ISerializablePacket packet, SteamNetworkingIdentity target)/* OutgoingMessage msg)*/
        {
            if (!IsConnected) { return; }
            if (!IsLocal) { return; }

            using (PooledNetWriter w = NetWriterPool.GetWriter())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var segment = w.ToArray();
                //if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnSend(opcode, segment.Count, 1); }

                // Initialize unmanaged memory to hold the array.
                int size = Marshal.SizeOf(segment[0]) * segment.Length;
                IntPtr pnt = Marshal.AllocHGlobal(size);

                try
                {
                    // Copy the array to unmanaged memory.
                    Marshal.Copy(segment, 0, pnt, segment.Length);
                    EResult result = SteamNetworkingMessages.SendMessageToUser(ref target, pnt, (uint)size, Constants.k_nSteamNetworkingSend_ReliableNoNagle, 0);
                    if (NetLogFilter.logInfo) { Debug.Log($"Packet to {SteamFriends.GetFriendPersonaName(target.GetSteamID())} was sent with resilt: {result}. ({Time.time})"); }
                }
                finally
                {
                    // Free the unmanaged memory.
                    Marshal.FreeHGlobal(pnt);
                }


                //if (SteamNetworking.SendP2PPacket(target, segment, (uint)segment.Length, EP2PSend.k_EP2PSendReliable))
                //{
                //    if (NetLogFilter.logInfo) { Debug.Log($"Packet to {SteamFriends.GetFriendPersonaName(target)} was successfully sent. ({Time.time})"); }
                //}
                //else
                //{
                //    if (NetLogFilter.logInfo) { Debug.Log($"Packet to {SteamFriends.GetFriendPersonaName(target)} failed to send. ({Time.time})"); }
                //}
            }
        }

        public void Send(short opcode, ISerializablePacket packet)
        {
            if (!IsConnected) { return; }
            if (!IsLocal) { return; }

            using (PooledNetWriter w = NetWriterPool.GetWriter())
            {
                MessageHelper.CreateAndFinalize(w, opcode, packet);
                var segment = w.ToArray();
                //if (NetLogFilter.messageDiagnostics) { NetDiagnostics.OnSend(opcode, segment.Count, 1); }

                // Initialize unmanaged memory to hold the array.
                int size = Marshal.SizeOf(segment[0]) * segment.Length;
                IntPtr pnt = Marshal.AllocHGlobal(size);

                try
                {
                    foreach (var conn in connectedPeers.Values)
                    {
                        Marshal.Copy(segment, 0, pnt, segment.Length);
                        EResult result = SteamNetworkingMessages.SendMessageToUser(ref conn.identity, pnt, (uint)size, Constants.k_nSteamNetworkingSend_ReliableNoNagle, 0);
                        if (NetLogFilter.logInfo) { Debug.Log($"Packet to {SteamFriends.GetFriendPersonaName(conn.identity.GetSteamID())} was sent with resilt: {result}. ({Time.time})"); }
                    }
                    // Copy the array to unmanaged memory.
                }
                finally
                {
                    // Free the unmanaged memory.
                    Marshal.FreeHGlobal(pnt);
                }
            }


        }
#endif
    }
}
