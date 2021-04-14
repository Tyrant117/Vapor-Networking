#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
#if STEAMWORKS
using Steamworks;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking.Steam
{
    public class SteamNetworkIdentity : MonoBehaviour
    {
#if STEAMWORKS
        /// <summary>All spawned NetworkIdentities by netId. Available on server and client.</summary>
        // server sees ALL spawned ones.
        // client sees OBSERVED spawned ones.
        public static readonly Dictionary<CSteamID, SteamNetworkIdentity> spawned = new Dictionary<CSteamID, SteamNetworkIdentity>();


        public SteamConnection connection;
        public SteamConnection Connection 
        {
            get { return connection; }
            set
            {
                connection.RemoveOwnedObject(this);
                connection = value;
                connection.AddOwnedObject(this);
            }
        }

#if ODIN_INSPECTOR
        [ReadOnly, ShowInInspector]
#endif
        public ulong SteamID => Connection != null ? Connection.SteamID.m_SteamID : 0;
#if ODIN_INSPECTOR
        [ReadOnly, ShowInInspector]
#endif
        public bool IsLocal => Connection != null ? Connection.IsLocal : true;
#endif
    }
}
