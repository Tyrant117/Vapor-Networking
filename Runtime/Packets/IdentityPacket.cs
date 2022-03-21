using System;

namespace VaporNetworking
{
    public struct IdentityPacket : ISerializablePacket
    {
        public ulong identity;

        public void Deserialize(NetReader r)
        {
            identity = Compression.DecompressULong(r);
        }

        public void Serialize(NetWriter w)
        {
            Compression.CompressULong(w, identity);
        }
    }
}