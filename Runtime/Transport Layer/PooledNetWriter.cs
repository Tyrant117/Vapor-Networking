using System;
using System.Collections.Generic;

namespace VaporNetworking
{
    public class PooledNetWriter : NetWriter, IDisposable
    {
        public void Dispose()
        {
            NetWriterPool.Recycle(this);
        }
    }

    public static class NetWriterPool
    {
        static readonly Stack<PooledNetWriter> pool = new Stack<PooledNetWriter>();

        public static PooledNetWriter GetWriter()
        {
            if (pool.Count != 0)
            {
                PooledNetWriter writer = pool.Pop();
                // reset cached writer length and position
                writer.SetLength(0);
                return writer;
            }

            return new PooledNetWriter();
        }

        public static void Recycle(PooledNetWriter writer)
        {
            pool.Push(writer);
        }
    }
}
