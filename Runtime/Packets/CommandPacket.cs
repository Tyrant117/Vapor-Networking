
using System;

namespace VaporNetworking
{
    public struct CommandPacket : ISerializablePacket, ICommandPacket
    {
        public byte Command { get ; set ; }
        public ArraySegment<byte> data; // Recieves The Data


        public void Deserialize(NetReader r)
        {
            Command = r.ReadByte();
            data = r.ReadBytesAndSizeSegment();
        }

        public void Serialize(NetWriter w)
        {
            w.Write(Command);
            w.WriteBytesAndSizeSegment(data);
        }
    }
}