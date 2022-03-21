using UnityEngine;

namespace VaporNetworking
{
    public class ClientModule : MonoBehaviour
    {
        public UDPClient Client { get; private set; }

        /// <summary>
        ///     Called by master server when module should be started
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Initialize(UDPClient client)
        {
            Client = client;
        }

        /// <summary>
        ///     Called when the manager unloads all the modules.
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Unload(UDPClient client)
        {

        }
    }
}