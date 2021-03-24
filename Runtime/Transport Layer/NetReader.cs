using System;
using System.IO;
using System.Text;
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
        public int Length => buffer.Count;

        #region - Reading -
        public byte ReadByte() { if (Position + 1 > buffer.Count) { throw new EndOfStreamException("ReadByte out of range:" + ToString()); } return buffer.Array[buffer.Offset + Position++]; }
        public sbyte ReadSByte() { return (sbyte)ReadByte(); }
        public char ReadChar() { return (char)ReadUInt16(); }
        public bool ReadBoolean() { return ReadByte() != 0; }
        public short ReadInt16() { return (short)ReadUInt16(); }
        public ushort ReadUInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return value;
        }
        public int ReadInt32() { return (int)ReadUInt32(); }
        public uint ReadUInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return value;
        }
        public long ReadInt64() { return (long)ReadUInt64(); }
        public ulong ReadUInt64()
        {
            ulong value = 0;
            value |= ReadByte();
            value |= ((ulong)ReadByte()) << 8;
            value |= ((ulong)ReadByte()) << 16;
            value |= ((ulong)ReadByte()) << 24;
            value |= ((ulong)ReadByte()) << 32;
            value |= ((ulong)ReadByte()) << 40;
            value |= ((ulong)ReadByte()) << 48;
            value |= ((ulong)ReadByte()) << 56;
            return value;
        }
        public decimal ReadDecimal()
        {
            UIntDecimal converter = new UIntDecimal();
            converter.longValue1 = ReadUInt64();
            converter.longValue2 = ReadUInt64();
            return converter.decimalValue;
        }
        public float ReadSingle()
        {
            UIntFloat converter = new UIntFloat();
            converter.intValue = ReadUInt32();
            return converter.floatValue;
        }
        public double ReadDouble()
        {
            UIntDouble converter = new UIntDouble();
            converter.longValue = ReadUInt64();
            return converter.doubleValue;
        }

        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        public string ReadString()
        {
            // read number of bytes
            ushort size = ReadUInt16();

            if (size == 0)
                return null;

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

        public byte[] ReadBytes(int count)
        {
            byte[] bytes = new byte[count];
            ReadBytes(bytes, count);
            return bytes;
        }

        public byte[] ReadBytes(byte[] bytes, int count)
        {
            // check if passed byte array is big enough
            if (count > bytes.Length)
            {
                throw new EndOfStreamException("ReadBytes can't read " + count + " + bytes because the passed byte[] only has length " + bytes.Length);
            }

            ArraySegment<byte> data = ReadBytesSegment(count);
            Array.Copy(data.Array, data.Offset, bytes, 0, count);
            return bytes;
        }

        // useful to parse payloads etc. without allocating
        public ArraySegment<byte> ReadBytesSegment(int count)
        {
            // check if within buffer limits
            if (Position + count > buffer.Count)
            {
                throw new EndOfStreamException("ReadBytesSegment can't read " + count + " bytes because it would read past the end of the stream. " + ToString());
            }

            // return the segment
            ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
            Position += count;
            return result;
        }

        public byte[] ReadBytesAndSize()
        {
            // count = 0 means the array was null
            // otherwise count -1 is the length of the array
            uint count = ReadPackedUInt32();
            return count == 0 ? null : ReadBytes(checked((int)(count - 1u)));
        }

        public ArraySegment<byte> ReadBytesAndSizeSegment()
        {
            // count = 0 means the array was null
            // otherwise count - 1 is the length of the array
            uint count = ReadPackedUInt32();
            return count == 0 ? default : ReadBytesSegment(checked((int)(count - 1u)));
        }

        public int ReadPackedInt32()
        {
            uint data = ReadPackedUInt32();
            return (int)((data >> 1) ^ -(data & 1));
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.
        public UInt32 ReadPackedUInt32()
        {
            UInt64 value = ReadPackedUInt64();
            if (value > UInt32.MaxValue)
            {
                throw new IndexOutOfRangeException("ReadPackedUInt32() failure, value too large");
            }
            return (UInt32)value;
        }

        public UInt64 ReadPackedUInt64()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + 256 * (a0 - ((UInt64)241)) + a1;
            }

            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return 2288 + (((UInt64)256) * a1) + a2;
            }

            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16);
            }

            byte a4 = ReadByte();
            if (a0 == 251)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24);
            }

            byte a5 = ReadByte();
            if (a0 == 252)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32);
            }

            byte a6 = ReadByte();
            if (a0 == 253)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40);
            }

            byte a7 = ReadByte();
            if (a0 == 254)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48);
            }

            byte a8 = ReadByte();
            if (a0 == 255)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48) + (((UInt64)a8) << 56);
            }

            throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
        }

        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector2Int ReadVector2Int()
        {
            return new Vector2Int(ReadPackedInt32(), ReadPackedInt32());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Vector3Int ReadVector3Int()
        {
            return new Vector3Int(ReadPackedInt32(), ReadPackedInt32(), ReadPackedInt32());
        }

        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }
        #endregion
    }
}