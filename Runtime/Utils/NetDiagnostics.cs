using System;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public static class NetDiagnostics
    {
        public readonly struct MessageInfo
        {
            /// <summary>
            /// The opcode that this message was sent to.
            /// </summary>
            public readonly short OpCode;

            /// <summary>
            /// The size of the message.
            /// </summary>
            public readonly int Size;

            /// <summary>
            /// The number of connections the message was sent to or the connection ID the message was sent to if recieving on the server.
            /// </summary>
            public readonly int Connections;

            internal MessageInfo(short opcode, int size, int conns)
            {
                OpCode = opcode;
                Size = size;
                Connections = conns;
            }
        }

        private static Queue<int> bytesIn = new Queue<int>(10);
        private static Queue<int> bytesOut = new Queue<int>(10);

        private static float lastTime;
        private static int outInterval;
        private static int inInterval;

        private static int totalBytesIn;
        private static int totalBytesOut;

        public static int aveBytesIn;
        public static int aveBytesOut;


        #region Throughput
        public static void AverageThroughput(float time)
        {
            if(time - lastTime > 1)
            {
                lastTime = time;
                bytesIn.Enqueue(inInterval);
                bytesOut.Enqueue(outInterval);
                totalBytesIn += inInterval;
                totalBytesOut += outInterval;

                if(bytesIn.Count > 10)
                {
                    totalBytesIn -= bytesIn.Dequeue();
                }
                if (bytesOut.Count > 10)
                {
                    totalBytesOut -= bytesOut.Dequeue();
                }

                inInterval = 0;
                outInterval = 0;

                aveBytesIn = totalBytesIn / bytesIn.Count;
                aveBytesOut = totalBytesOut / bytesOut.Count;
            }
        }
        #endregion


        #region Outgoing Messages
        internal static void OnSend(short opcode, int size, int conns)
        {
            outInterval += size;
            if (opcode == 60 || opcode == 61 || opcode == 100 || opcode == 101) { return; }
            Debug.Log($"[Sent] OpCode: {opcode} || Size: {size} || Connections/ID: {conns}");
        }
        #endregion

        #region Incomming Messages

        internal static void OnReceive(short opcode, int size, int conns)
        {
            inInterval += size;
            if (opcode == 60 || opcode == 61 || opcode == 100 || opcode == 101) { return; }
            Debug.Log($"[Recieved] OpCode: {opcode} || Size: {size} || Connections/ID: {conns}");
        }
        #endregion
    }
}