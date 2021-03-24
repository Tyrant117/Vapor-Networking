using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public static class MessageHelper
    {
        /// <summary>
        ///     Deserialized data into the provided packet and returns it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T Deserialize<T>(int offset, ArraySegment<byte> data, T packet) where T : struct, ISerializablePacket
        {
            using (PooledNetReader r = NetReaderPool.GetReader(data))
            {
                r.Position = offset;
                packet.Deserialize(r);
                return packet;
            }
        }

        public static void CreateAndFinalize(NetWriter w, short opCode, ISerializablePacket packet/*, int responseID = -1, bool comepleteResponse = false, ResponseStatus status = ResponseStatus.Default*/)
        {
            w.Write(opCode);
            packet.Serialize(w);
        }

        /// <summary>
        ///     Constructs the message buffer into an incoming message.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public static IncomingMessage FromBytes(ArraySegment<byte> buffer, Peer sender)
        {
            using (PooledNetReader r = NetReaderPool.GetReader(buffer))
            {
                var opCode = r.ReadInt16();
                var msg = new IncomingMessage(opCode, buffer, sender/*, responseID, completeResponse, status*/);

                return msg;
            }
        }
    }
}