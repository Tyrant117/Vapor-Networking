using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VaporNetworking
{
    public static class Compression
    {
        private const float QuaternionMinRange = -0.707107f;
        private const float QuaternionMaxRange = 0.707107f;
        private const ushort TenBitsMax = 0x3FF;

        // scale a float within min/max range to an ushort between min/max range
        // note: can also use this for byte range from byte.MinValue to byte.MaxValue
        public static ushort ScaleFloatToUShort(float value, float minValue, float maxValue, ushort minTarget, ushort maxTarget)
        {
            // note: C# ushort - ushort => int, hence so many casts
            // max ushort - min ushort only fits into something bigger
            int targetRange = maxTarget - minTarget;
            float valueRange = maxValue - minValue;
            float valueRelative = value - minValue;
            return (ushort)(minTarget + (ushort)(valueRelative / valueRange * targetRange));
        }

        // scale an ushort within min/max range to a float between min/max range
        // note: can also use this for byte range from byte.MinValue to byte.MaxValue
        public static float ScaleUShortToFloat(ushort value, ushort minValue, ushort maxValue, float minTarget, float maxTarget)
        {
            // note: C# ushort - ushort => int, hence so many casts
            float targetRange = maxTarget - minTarget;
            ushort valueRange = (ushort)(maxValue - minValue);
            ushort valueRelative = (ushort)(value - minValue);
            return minTarget + (valueRelative / (float)valueRange * targetRange);
        }

        // helper function to access 'nth' component of quaternion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float QuaternionElement(Quaternion q, int element)
        {
            switch (element)
            {
                case 0: return q.x;
                case 1: return q.y;
                case 2: return q.z;
                case 3: return q.w;
                default: return 0;
            }
        }

        // quaternion compression //////////////////////////////////////////////
        // smallest three: https://gafferongames.com/post/snapshot_compression/
        // compresses 16 bytes quaternion into 4 bytes

        // helper function to find largest absolute element
        // returns the index of the largest one
        public static int LargestAbsoluteComponentIndex(Vector4 value, out float largestAbs, out Vector3 withoutLargest)
        {
            // convert to abs
            Vector4 abs = new Vector4(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z), Mathf.Abs(value.w));

            // set largest to first abs (x)
            largestAbs = abs.x;
            withoutLargest = new Vector3(value.y, value.z, value.w);
            int largestIndex = 0;

            // compare to the others, starting at second value
            // performance for 100k calls
            //   for-loop:       25ms
            //   manual checks:  22ms
            if (abs.y > largestAbs)
            {
                largestIndex = 1;
                largestAbs = abs.y;
                withoutLargest = new Vector3(value.x, value.z, value.w);
            }
            if (abs.z > largestAbs)
            {
                largestIndex = 2;
                largestAbs = abs.z;
                withoutLargest = new Vector3(value.x, value.y, value.w);
            }
            if (abs.w > largestAbs)
            {
                largestIndex = 3;
                largestAbs = abs.w;
                withoutLargest = new Vector3(value.x, value.y, value.z);
            }

            return largestIndex;
        }


        // note: assumes normalized quaternions
        public static uint CompressQuaternion(Quaternion q)
        {
            // note: assuming normalized quaternions is enough. no need to force
            //       normalize here. we already normalize when decompressing.

            // find the largest component index [0,3] + value
            int largestIndex = LargestAbsoluteComponentIndex(new Vector4(q.x, q.y, q.z, q.w), out float _, out Vector3 withoutLargest);

            // from here on, we work with the 3 components without largest!

            // "You might think you need to send a sign bit for [largest] in
            // case it is negative, but you don�t, because you can make
            // [largest] always positive by negating the entire quaternion if
            // [largest] is negative. in quaternion space (x,y,z,w) and
            // (-x,-y,-z,-w) represent the same rotation."
            if (QuaternionElement(q, largestIndex) < 0)
                withoutLargest = -withoutLargest;

            // put index & three floats into one integer.
            // => index is 2 bits (4 values require 2 bits to store them)
            // => the three floats are between [-0.707107,+0.707107] because:
            //    "If v is the absolute value of the largest quaternion
            //     component, the next largest possible component value occurs
            //     when two components have the same absolute value and the
            //     other two components are zero. The length of that quaternion
            //     (v,v,0,0) is 1, therefore v^2 + v^2 = 1, 2v^2 = 1,
            //     v = 1/sqrt(2). This means you can encode the smallest three
            //     components in [-0.707107,+0.707107] instead of [-1,+1] giving
            //     you more precision with the same number of bits."
            // => the article recommends storing each float in 9 bits
            // => our uint has 32 bits, so we might as well store in (32-2)/3=10
            //    10 bits max value: 1023=0x3FF (use OSX calc to flip 10 bits)
            ushort aScaled = ScaleFloatToUShort(withoutLargest.x, QuaternionMinRange, QuaternionMaxRange, 0, TenBitsMax);
            ushort bScaled = ScaleFloatToUShort(withoutLargest.y, QuaternionMinRange, QuaternionMaxRange, 0, TenBitsMax);
            ushort cScaled = ScaleFloatToUShort(withoutLargest.z, QuaternionMinRange, QuaternionMaxRange, 0, TenBitsMax);

            // now we just need to pack them into one integer
            // -> index is 2 bit and needs to be shifted to 31..32
            // -> a is 10 bit and needs to be shifted 20..30
            // -> b is 10 bit and needs to be shifted 10..20
            // -> c is 10 bit and needs to be at      0..10
            return (uint)(largestIndex << 30 | aScaled << 20 | bScaled << 10 | cScaled);
        }

        // Quaternion normalizeSAFE from ECS math.normalizesafe()
        // => useful to produce valid quaternions even if client sends invalid
        //    data
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Quaternion QuaternionNormalizeSafe(Quaternion value)
        {
            // The smallest positive normal number representable in a float.
            const float FLT_MIN_NORMAL = 1.175494351e-38F;

            Vector4 v = new Vector4(value.x, value.y, value.z, value.w);
            float length = Vector4.Dot(v, v);
            return length > FLT_MIN_NORMAL
                   ? value.normalized
                   : Quaternion.identity;
        }

        // note: gives normalized quaternions
        public static Quaternion DecompressQuaternion(uint data)
        {
            // get cScaled which is at 0..10 and ignore the rest
            ushort cScaled = (ushort)(data & TenBitsMax);

            // get bScaled which is at 10..20 and ignore the rest
            ushort bScaled = (ushort)((data >> 10) & TenBitsMax);

            // get aScaled which is at 20..30 and ignore the rest
            ushort aScaled = (ushort)((data >> 20) & TenBitsMax);

            // get 2 bit largest index, which is at 31..32
            int largestIndex = (int)(data >> 30);

            // scale back to floats
            float a = ScaleUShortToFloat(aScaled, 0, TenBitsMax, QuaternionMinRange, QuaternionMaxRange);
            float b = ScaleUShortToFloat(bScaled, 0, TenBitsMax, QuaternionMinRange, QuaternionMaxRange);
            float c = ScaleUShortToFloat(cScaled, 0, TenBitsMax, QuaternionMinRange, QuaternionMaxRange);

            // calculate the omitted component based on a�+b�+c�+d�=1
            float d = Mathf.Sqrt(1 - a * a - b * b - c * c);
            var value = largestIndex switch
            {
                0 => new Vector4(d, a, b, c),
                1 => new Vector4(a, d, b, c),
                2 => new Vector4(a, b, d, c),
                _ => new Vector4(a, b, c, d),
            };

            // ECS Rotation only works with normalized quaternions.
            // make sure that's always the case here to avoid ECS bugs where
            // everything stops moving if the quaternion isn't normalized.
            // => NormalizeSafe returns a normalized quaternion even if we pass
            //    in NaN from deserializing invalid values!
            return QuaternionNormalizeSafe(new Quaternion(value.x, value.y, value.z, value.w));
        }

        // varint compression //////////////////////////////////////////////////
        // compress ulong varint.
        // same result for int, short and byte. only need one function.
        // NOT an extension. otherwise weaver might accidentally use it.
        public static void CompressULong(NetWriter writer, ulong value)
        {
            if (value <= 240)
            {
                writer.WriteByte((byte)value);
                return;
            }
            if (value <= 2287)
            {
                writer.WriteByte((byte)(((value - 240) >> 8) + 241));
                writer.WriteByte((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                writer.WriteByte((byte)249);
                writer.WriteByte((byte)((value - 2288) >> 8));
                writer.WriteByte((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                writer.WriteByte((byte)250);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                writer.WriteByte((byte)251);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                writer.WriteByte((byte)252);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                writer.WriteByte((byte)253);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                writer.WriteByte((byte)254);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                writer.WriteByte((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                writer.WriteByte((byte)255);
                writer.WriteByte((byte)(value & 0xFF));
                writer.WriteByte((byte)((value >> 8) & 0xFF));
                writer.WriteByte((byte)((value >> 16) & 0xFF));
                writer.WriteByte((byte)((value >> 24) & 0xFF));
                writer.WriteByte((byte)((value >> 32) & 0xFF));
                writer.WriteByte((byte)((value >> 40) & 0xFF));
                writer.WriteByte((byte)((value >> 48) & 0xFF));
                writer.WriteByte((byte)((value >> 56) & 0xFF));
            }
        }

        // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CompressLong(NetWriter writer, long i)
        {
            ulong zigzagged = (ulong)((i >> 63) ^ (i << 1));
            CompressULong(writer, zigzagged);
        }

        // NOT an extension. otherwise weaver might accidentally use it.
        public static ulong DecompressULong(NetReader reader)
        {
            byte a0 = reader.ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = reader.ReadByte();
            if (a0 <= 248)
            {
                return 240 + ((a0 - (ulong)241) << 8) + a1;
            }

            byte a2 = reader.ReadByte();
            if (a0 == 249)
            {
                return 2288 + ((ulong)a1 << 8) + a2;
            }

            byte a3 = reader.ReadByte();
            if (a0 == 250)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16);
            }

            byte a4 = reader.ReadByte();
            if (a0 == 251)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24);
            }

            byte a5 = reader.ReadByte();
            if (a0 == 252)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32);
            }

            byte a6 = reader.ReadByte();
            if (a0 == 253)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40);
            }

            byte a7 = reader.ReadByte();
            if (a0 == 254)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48);
            }

            byte a8 = reader.ReadByte();
            if (a0 == 255)
            {
                return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48) + (((ulong)a8) << 56);
            }

            throw new IndexOutOfRangeException($"DecompressVarInt failure: {a0}");
        }

        // zigzag decoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long DecompressVarInt(NetReader reader)
        {
            ulong data = DecompressULong(reader);
            return ((long)(data >> 1)) ^ -((long)data & 1);
        }
    }
}
