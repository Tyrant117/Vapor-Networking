using System;

namespace VaporNetworking
{
    public struct IdentityPacket : ISerializablePacket
    {
        public ulong identity;

        public void Deserialize(NetReader reader)
        {
            identity = reader.ReadPackedUInt64();
        }

        public void Serialize(NetWriter writer)
        {
            writer.WritePackedUInt64(identity);
        }
    }
}