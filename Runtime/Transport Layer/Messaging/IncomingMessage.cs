
using System;

namespace VaporNetworking
{
    public struct IncomingMessage
    {
        public ArraySegment<byte> Buffer { get; private set; }

        /// <summary>
        ///     Operation code (message type)
        /// </summary>
        public short OpCode { get; private set; }

        /// <summary>
        ///     Sender
        /// </summary>
        public Peer Sender { get; private set; }

        /// <summary>
        ///     Returns size of the data
        /// </summary>
        public int Size => Buffer.Count - 2;

        ///// <summary>
        /////     ID of the response callback.
        ///// </summary>
        //public int ResponseID { get; set; }

        ///// <summary>
        /////     If the message response is complete this is true.
        ///// </summary>
        //public bool CompleteResponse { get; set; }

        ///// <summary>
        /////     Message status code
        ///// </summary>
        //public ResponseStatus Status { get; set; }

        //public bool IsExpectingResponse { get { return ResponseID > -1 && CompleteResponse; } }

        public IncomingMessage(short opCode, ArraySegment<byte> data, Peer sender/*, int responseID = -1, bool completeResponse = false, ResponseStatus status = ResponseStatus.Default*/)
        {
            Sender = sender;

            OpCode = opCode;
            Buffer = data;

            //ResponseID = responseID;
            //CompleteResponse = completeResponse;
            //Status = status;
        }

        #region - Reponse Messages -
        /// <summary>
        ///     Response message to the sender
        /// </summary>
        /// <param name="msg"></param>
        public void Respond(IServerMessages server, short opcode, ISerializablePacket packet)
        {
            server.Send(Sender, opcode, packet);
        }
        #endregion

        /// <summary>
        ///     Writes content of the message into a packet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packetToBeFilled"></param>
        /// <returns></returns>
        public T Deserialize<T>(T packetToBeFilled) where T : struct, ISerializablePacket
        {
            using (PooledNetReader r = NetReaderPool.Get(Buffer))
            {
                r.ReadShort(); // This is the opcode that is still part of the data.
                packetToBeFilled.Deserialize(r);
                return packetToBeFilled;
            }
        }
    }
}