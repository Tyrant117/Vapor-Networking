using System;
using System.Runtime.CompilerServices;

namespace VaporNetworking
{
    public sealed class PooledNetWriter : NetWriter, IDisposable
    {
        public void Dispose() => NetWriterPool.Return(this);
    }

    public static class NetWriterPool
    {
        // reuse Pool<T>
        // we still wrap it in NetworkWriterPool.Get/Recycle so we can reset the
        // position before reusing.
        // this is also more consistent with NetworkReaderPool where we need to
        // assign the internal buffer before reusing.
        static readonly Pool<PooledNetWriter> Pool = new Pool<PooledNetWriter>(
            () => new PooledNetWriter(),
            // initial capacity to avoid allocations in the first few frames
            // 1000 * 1200 bytes = around 1 MB.
            1000
        );

        /// <summary>Get a writer from the pool. Creates new one if pool is empty.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledNetWriter Get()
        {
            // grab from pool & reset position
            PooledNetWriter writer = Pool.Get();
            writer.Reset();
            return writer;
        }

        /// <summary>Return a writer to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(PooledNetWriter writer)
        {
            Pool.Return(writer);
        }
    }
}
