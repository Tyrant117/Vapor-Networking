﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public class ServerModule : MonoBehaviour
    {
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

        public UDPServer Server { get; set; }

        /// <summary>
        ///     Called by master server when module should be started
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Initialize(UDPServer server)
        {

        }

        /// <summary>
        ///     Called when the manager unloads all the modules.
        /// </summary>
        /// <param name="manager"></param>
        public virtual void Unload(UDPServer server)
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
    }
}