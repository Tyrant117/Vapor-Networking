using System;
using System.Collections.Generic;
using UnityEngine;
#if STEAMWORKS
using Steamworks;
#endif

namespace VaporNetworking.Steam
{
    public class SteamNetworkModule : MonoBehaviour
    {
#if STEAMWORKS
        /// <summary>
        ///     Other servermodules this one is dependant on
        /// </summary>
        private readonly List<Type> dependencies = new List<Type>();

        /// <summary>
        ///     Returns a list of module types this module depends on.
        /// </summary>
        public IEnumerable<Type> Dependencies
        {
            get { return dependencies; }
        }

        public SteamNetwork Network { get; set; }

        /// <summary>
        ///     Called by master server when module should be started
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Initialize(SteamNetwork client)
        {

        }

        /// <summary>
        ///     Called when the manager unloads all the modules.
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Unload(SteamNetwork client)
        {

        }

        /// <summary>
        ///     Adds a dependency to the list. Should be called in awake method of a module.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddDependency<T>()
        {
            dependencies.Add(typeof(T));
        }
#endif
    }
}