using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace VaporNetworking
{
    public class NetReader
    {
        internal ArraySegment<byte> buffer;

        public NetReader(byte[] bytes)
        {
            buffer = new ArraySegment<byte>(bytes);
        }

        public NetReader(ArraySegment<byte> segment)
        {
            buffer = segment;
        }

        public int Position;
        /// <summary>Total number of bytes to read from buffer</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count;
        }
        /// <summary>Remaining bytes that can be read, for convenience.</summary>
        public int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length - Position;
        }

        #region - Blittable -
        // ReadBlittable<T> from DOTSNET
        // this is extremely fast, but only works for blittable types.
        // => private to make sure nobody accidentally uses it for non-blittable
        //
        // Benchmark: see NetworkWriter.WriteBlittable!
        //
        // Note:
        //   ReadBlittable assumes same endianness for server & client.
        //   All Unity 2018+ platforms are little endian.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T ReadBlittable<T>()
            where T : unmanaged
        {
            // check if blittable for safety
#if UNITY_EDITOR
            if (!UnsafeUtility.IsBlittable(typeof(T)))
            {
                throw new ArgumentException($"{typeof(T)} is not blittable!");
            }
#endif

            // calculate size
            //   sizeof(T) gets the managed size at compile time.
            //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
            // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
            // => for blittable types, sizeof(T) is even recommended:
            // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
            int size = sizeof(T);

            // enough data to read?
            if (Position + size > buffer.Count)
            {
                throw new EndOfStreamException($"ReadBlittable<{typeof(T)}> out of range: {ToString()}");
            }

            // read blittable
            T value;
            fixed (byte* ptr = &buffer.Array[buffer.Offset + Position])
            {
                // cast buffer to a T* pointer and then read from it.
                value = *(T*)ptr;
            }
            Position += size;
            return value;
        }


        // blittable'?' template for code reuse
        // note: bool isn't blittable. need to read as byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? ReadBlittableNullable<T>()
            where T : unmanaged =>
                ReadByte() != 0 ? ReadBlittable<T>() : default(T?);
        #endregion

        #region - Reading -
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte() => ReadBlittable<byte>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte? ReadByteNullable() => ReadBlittableNullable<byte>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte() { return (sbyte)ReadByte(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte? ReadSByteNullable() => ReadBlittableNullable<sbyte>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar() => (char)ReadBlittable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char? ReadCharNullable() => (char?)ReadBlittableNullable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean() => ReadBlittable<byte>() != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool? ReadBoolNullable()
        {
            byte? value = ReadBlittableNullable<byte>();
            return value.HasValue ? (value.Value != 0) : default(bool?);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort() => (short)ReadUShort();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short? ReadShortNullable() => ReadBlittableNullable<short>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUShort() => ReadBlittable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort? ReadUShortNullable() => ReadBlittableNullable<ushort>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt() => ReadBlittable<int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? ReadIntNullable() => ReadBlittableNullable<int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt() => ReadBlittable<uint>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint? ReadUIntNullable() => ReadBlittableNullable<uint>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()  => ReadBlittable<long>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long? ReadLongNullable() => ReadBlittableNullable<long>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadULong() => ReadBlittable<ulong>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong? ReadULongNullable() => ReadBlittableNullable<ulong>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat() => ReadBlittable<float>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ReadFloatNullable() => ReadBlittableNullable<float>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble() => ReadBlittable<double>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double? ReadDoubleNullable() => ReadBlittableNullable<double>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal() => ReadBlittable<decimal>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal? ReadDecimalNullable() => ReadBlittableNullable<decimal>();

        static readonly UTF8Encoding encoding = new(false, true);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            // read number of bytes
            ushort size = ReadUShort();
            if (size == 0)
            {
                return null;
            }

            int realSize = size - 1;

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize >= NetWriter.MaxStringLength)
            {
                throw new EndOfStreamException("ReadString too long: " + realSize + ". Limit is: " + NetWriter.MaxStringLength);
            }

            ArraySegment<byte> data = ReadBytesSegment(realSize);

            // convert directly from buffer to string via encoding
            return encoding.GetString(data.Array, data.Offset, data.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes(int count)
        {
            byte[] bytes = new byte[count];
            ReadBytes(bytes, count);
            return bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException($"ReadBytes can't read {count} + bytes because the passed byte[] only has length {bytes.Length}");
            }
            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
            }

            Array.Copy(buffer.Array, buffer.Offset + Position, bytes, 0, count);
            Position += count;
            return bytes;
        }

        // useful to parse payloads etc. without allocating
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException("ReadBytesSegment can't read " + count + " bytes because it would read past the end of the stream. " + ToString());
            }

            // return the segment
            ArraySegment<byte> result = new(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesAndSize()
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = ReadUInt();
            return count == 0 ? null : ReadBytes(checked((int)(count - 1u)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesAndSizeSegment()
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = ReadUInt();
            return count == 0 ? default : ReadBytesSegment(checked((int)(count - 1u)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ReadVector2() => ReadBlittable<Vector2>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2? ReadVector2Nullable() => ReadBlittableNullable<Vector2>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int ReadVector2Int() => ReadBlittable<Vector2Int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ReadVector3() => ReadBlittable<Vector3>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3? ReadVector3Nullable() => ReadBlittableNullable<Vector3>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int ReadVector3Int() => ReadBlittable<Vector3Int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int? ReadVector3IntNullable() => ReadBlittableNullable<Vector3Int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ReadVector4() => ReadBlittable<Vector4>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4? ReadVector4Nullable() => ReadBlittableNullable<Vector4>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadColor() => ReadBlittable<Color>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color? ReadColorNullable() => ReadBlittableNullable<Color>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 ReadColor32() => ReadBlittable<Color32>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32? ReadColor32Nullable() => ReadBlittableNullable<Color32>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ReadQuaternion() => ReadBlittable<Quaternion>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion? ReadQuaternionNullable() => ReadBlittableNullable<Quaternion>();
        #endregion
    }
}