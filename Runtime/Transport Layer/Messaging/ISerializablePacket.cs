using System;

namespace VaporNetworking
{
    public interface ISerializablePacket
    {
        /// <summary>
        ///     Serializes the data contained in the class to send over the network.
        /// </summary>
        /// <param name="writer"></param>
        void Serialize(NetWriter writer);
        /// <summary>
        ///     Deserializes the data contained by the reader into the class. <see cref="FromBytes{T}(byte[], T)"/>
        /// </summary>
        /// <param name="reader"></param>
        void Deserialize(NetReader reader);
    }
}