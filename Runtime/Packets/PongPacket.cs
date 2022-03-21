
using System;

namespace VaporNetworking
{
    public struct PongPacket : ISerializablePacket
    {
        public double clientTime;
        public double serverTime;

        public void Deserialize(NetReader reader)
        {
            clientTime = reader.ReadDouble();
            serverTime = reader.ReadDouble();
        }

        public void Serialize(NetWriter writer)
        {
            writer.WriteDouble(clientTime);
            writer.WriteDouble(serverTime);
        }
    }
}