
using System;

namespace VaporNetworking
{
    public struct EmptyPacket : ISerializablePacket
    {
        public void Serialize(NetWriter writer)
        {
        }

        public void Deserialize(NetReader reader)
        {
        }
    }
}