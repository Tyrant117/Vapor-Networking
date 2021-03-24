using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VaporNetworking
{
    public class PooledNetReader : NetReader, IDisposable
    {
        internal PooledNetReader(byte[] bytes) : base(bytes) { }
        internal PooledNetReader(ArraySegment<byte> segment) : base(segment) { }

        public void Dispose()
        {
            NetReaderPool.Recycle(this);
        }
    }

    public static class NetReaderPool
    {
        static readonly Stack<PooledNetReader> pool = new Stack<PooledNetReader>();

        public static PooledNetReader GetReader(byte[] bytes)
        {
            if (pool.Count != 0)
            {
                PooledNetReader reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, bytes);
                return reader;
            }

            return new PooledNetReader(bytes);
        }

        public static PooledNetReader GetReader(ArraySegment<byte> segment)
        {
            if (pool.Count != 0)
            {
                PooledNetReader reader = pool.Pop();
                // reset buffer
                SetBuffer(reader, segment);
                return reader;
            }

            return new PooledNetReader(segment);
        }

        // SetBuffer methods mirror constructor for ReaderPool
        static void SetBuffer(NetReader reader, byte[] bytes)
        {
            reader.buffer = new ArraySegment<byte>(bytes);
            reader.Position = 0;
        }

        static void SetBuffer(NetReader reader, ArraySegment<byte> segment)
        {
            reader.buffer = segment;
            reader.Position = 0;
        }

        public static void Recycle(PooledNetReader reader)
        {
            pool.Push(reader);
        }
    }
}