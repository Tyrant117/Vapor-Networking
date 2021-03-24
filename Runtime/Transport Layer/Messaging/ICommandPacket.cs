using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public interface ICommandPacket
    {
        byte Command { get; set; }
    }
}