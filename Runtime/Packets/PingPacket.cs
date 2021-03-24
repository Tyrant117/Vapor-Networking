
using System;

namespace VaporNetworking
{
    public struct PingPacket : ISerializablePacket
    {
        public double lastPingPacketTime;

        public void Deserialize(NetReader reader)
        {
            lastPingPacketTime = reader.ReadDouble();
        }

        public void Serialize(NetWriter writer)
        {
            writer.Write(lastPingPacketTime);
        }
    }
}