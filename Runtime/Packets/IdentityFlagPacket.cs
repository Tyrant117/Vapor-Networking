using System;

namespace VaporNetworking
{
    public struct IdentityFlagPacket : ISerializablePacket
    {
        public ulong id;
        public byte flag;

        public void Deserialize(NetReader r)
        {
            id = r.ReadPackedUInt64();
            flag = r.ReadByte();
        }

        public void Serialize(NetWriter w)
        {
            w.WritePackedUInt64(id);
            w.Write(flag);
        }
    }
}