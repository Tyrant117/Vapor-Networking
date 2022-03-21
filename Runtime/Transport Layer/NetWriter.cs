using System;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace VaporNetworking
{
    /// <summary>
    /// Binary stream writer for writing simple types to byte arrays.
    /// </summary>
    public class NetWriter
    {
        public const int MaxStringLength = 1024 * 32;


        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        // => 1500 bytes by default because on average, most packets will be <= MTU
        byte[] buffer = new byte[1500];

        /// <summary>Next position to write to the buffer</summary>
        public int Position;

        /// <summary>Reset both the position and length of the stream</summary>
        // Leaves the capacity the same so that we can reuse this writer without
        // extra allocations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Position = 0;
        }

        // NOTE that our runtime resizing comes at no extra cost because:
        // 1. 'has space' checks are necessary even for fixed sized writers.
        // 2. all writers will eventually be large enough to stop resizing.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int value)
        {
            if (buffer.Length < value)
            {
                int capacity = Math.Max(value, buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
            }
        }

        /// <summary>Copies buffer until 'Position' to a new array.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ToArray()
        {
            byte[] data = new byte[Position];
            Array.ConstrainedCopy(buffer, 0, data, 0, Position);
            return data;
        }

        /// <summary>Returns allocation-free ArraySegment until 'Position'.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, Position);
        }

        // This returns and array segment, but first goes through a byte[] buffer. Will cause allocations, but cannot be overwritten.
        public ArraySegment<byte> ToArraySegmentCopy()
        {
            var buffer = ToArray(); // GC Alloc
            return new ArraySegment<byte>(buffer);
        }

        #region - Blittable -
        // WriteBlittable<T> from DOTSNET.
        // this is extremely fast, but only works for blittable types.
        //
        // Benchmark:
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2018 LTS (debug mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |  30.35 | 29.86 | 48.99 | 32.54 |  4.93 |
        //     blittable* |   5.69 |  5.52 | 27.51 |  7.78 |  5.65 |
        //
        //     * without IsBlittable check
        //     => 4-6x faster!
        //
        //   WriteQuaternion x 100k, Macbook Pro 2015 @ 2.2Ghz, Unity 2020.1 (release mode)
        //
        //                | Median |  Min  |  Max  |  Avg  |  Std  | (ms)
        //     before     |   9.41 |  8.90 | 23.02 | 10.72 |  3.07 |
        //     blittable* |   1.48 |  1.40 | 16.03 |  2.60 |  2.71 |
        //
        //     * without IsBlittable check
        //     => 6x faster!
        //
        // Note:
        //   WriteBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        //   => run NetworkWriterTests.BlittableOnThisPlatform() to verify!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteBlittable<T>(T value)
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                Debug.LogError($"{typeof(T)} is not blittable!");
                return;
            }
#endif
            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // ensure capacity
            // NOTE that our runtime resizing comes at no extra cost because:
            // 1. 'has space' checks are necessary even for fixed sized writers.
            // 2. all writers will eventually be large enough to stop resizing.
            EnsureCapacity(Position + size);

            // write blittable
            fixed (byte* ptr = &buffer[Position])
            {
                // cast buffer to T* pointer, then assign value to the area
                *(T*)ptr = value;
            }
            Position += size;
        }

        // blittable'?' template for code reuse
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteBlittableNullable<T>(T? value)
            where T : unmanaged
        {
            // bool isn't blittable. write as byte.
            WriteByte((byte)(value.HasValue ? 0x01 : 0x00));

            // only write value if exists. saves bandwidth.
            if (value.HasValue)
                WriteBlittable(value.Value);
        }
        #endregion

        #region - Writing -
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteNullable(byte? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByteNullable(sbyte? value) => WriteBlittableNullable(value);
        // char is not blittable. convert to ushort.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value) => WriteBlittable((ushort)value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCharNullable(char? value) => WriteBlittableNullable((ushort?)value);
        // bool is not blittable. convert to byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value) => WriteBlittable((byte)(value ? 1 : 0));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolNullable(bool? value) => WriteBlittableNullable(value.HasValue ? ((byte)(value.Value ? 1 : 0)) : new byte?());
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShort(short value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShortNullable(short? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUShort(ushort value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUShortNullable(ushort? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt(int value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntNullable(int? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt(uint value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUIntNullable(uint? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLong(long value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLongNullable(long? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteULong(ulong value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteULongNullable(ulong? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloatNullable(float? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleNullable(double? value) => WriteBlittableNullable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal value) => WriteBlittable(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimalNullable(decimal? value) => WriteBlittableNullable(value);

        // cache encoding instead of creating it with BinaryWriter each time
        // 1000 readers before:  1MB GC, 30ms
        // 1000 readers after: 0.8MB GC, 18ms
        static readonly UTF8Encoding encoding = new(false, true);
        static readonly byte[] stringBuffer = new byte[MaxStringLength];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                WriteUShort(0);
                return;
            }

            // write string with same method as NetworkReader
            // convert to byte[]
            int size = encoding.GetBytes(value, 0, value.Length, stringBuffer, 0);

            // check if within max size
            if (size >= MaxStringLength)
            {
                throw new IndexOutOfRangeException($"NetworkWriter.Write(string) too long: {size}. Limit: {MaxStringLength}");
            }

            // write size and bytes
            WriteUShort(checked((ushort)(size + 1)));
            WriteBytes(stringBuffer, 0, size);
        }

        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(Position + count);
            Array.ConstrainedCopy(buffer, offset, this.buffer, Position, count);
            Position += count;
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSize(byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays
            // write 0 for null array, increment normal size by 1 to save bandwidth
            // (using size=-1 for null would limit max size to 32kb instead of 64kb)
            if (buffer == null)
            {
                WriteUInt(0u);
                return;
            }
            WriteUInt(checked((uint)count) + 1u);
            WriteBytes(buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSize(byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSizeSegment(ArraySegment<byte> buffer)
        {
            uint length = (uint)buffer.Count;
            WriteUInt(length);
            for (int i = 0; i < length; i++)
            {
                WriteByte(buffer.Array[buffer.Offset + i]);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2(Vector2 value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2Nullable(Vector2? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3(Vector3 value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3Nullable(Vector3? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector4(Vector4 value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector4Nullable(Vector4? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2Int(Vector2Int value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2IntNullable(Vector2Int? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3Int(Vector3Int value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3IntNullable(Vector3Int? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor(Color value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColorNullable(Color? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor32(Color32 value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor32Nullable(Color32? value) => WriteBlittableNullable(value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteQuaternion(Quaternion value) => WriteBlittable(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteQuaternionNullable(Quaternion? value) => WriteBlittableNullable(value);
        #endregion
    }
}