using System;
using System.Runtime.CompilerServices;

namespace VaporNetworking
{
    public sealed class PooledNetReader : NetReader, IDisposable
    {
        internal PooledNetReader(byte[] bytes) : base(bytes) { }
        internal PooledNetReader(ArraySegment<byte> segment) : base(segment) { }

        public void Dispose()
        {
            NetReaderPool.Return(this);
        }
    }

    public static class NetReaderPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkReaderPool.Get/Recyle so we can reset the
        // position and array before reusing.
        static readonly Pool<PooledNetReader> Pool = new Pool<PooledNetReader>(
            // byte[] will be assigned in GetReader
            () => new PooledNetReader(new byte[] { }),
            // initial capacity to avoid allocations in the first few frames
            1000
        );

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetReader Get(byte[] bytes)
        {
            // grab from pool & set buffer
            PooledNetReader reader = Pool.Get();
            SetBuffer(reader, bytes);
            return reader;
        }

        /// <summary>Get the next reader in the pool. If pool is empty, creates a new Reader</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetReader Get(ArraySegment<byte> segment)
        {
            // grab from pool & set buffer
            PooledNetReader reader = Pool.Get();
            SetBuffer(reader, segment);
            return reader;
        }

        /// <summary>Returns a reader to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SetBuffer(NetReader reader, byte[] bytes)
        {
            reader.buffer = new ArraySegment<byte>(bytes);
            reader.Position = 0;
        }

        /// <summary>Returns a reader to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SetBuffer(NetReader reader, ArraySegment<byte> segment)
        {
            reader.buffer = segment;
            reader.Position = 0;
        }

        /// <summary>Returns a reader to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(PooledNetReader reader) => Pool.Return(reader);
    }
}