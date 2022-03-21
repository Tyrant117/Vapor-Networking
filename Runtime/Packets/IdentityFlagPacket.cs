using System;

namespace VaporNetworking
{
    public struct IdentityFlagPacket : ISerializablePacket
    {
        public ulong id;
        public byte flag;

        public void Deserialize(NetReader r)
        {
            id = Compression.DecompressULong(r);
            flag = r.ReadByte();
        }

        public void Serialize(NetWriter w)
        {
            Compression.CompressULong(w, id);
            w.WriteByte(flag);
        }
    }
}