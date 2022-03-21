using System;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public class ServerModule : MonoBehaviour
    {
        public UDPServer Server { get; private set; }

        /// <summary>
        ///     Called by master server when module should be started
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Initialize(UDPServer server)
        {
            Server = server;
        }

        /// <summary>
        ///     Called when the manager unloads all the modules.
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Unload(UDPServer server)
        {

        }
    }
}