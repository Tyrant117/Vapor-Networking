using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VaporNetworking
{
    public static class ServerTime
    {
        // how often are we sending ping messages
        // used to calculate network time and RTT
        public static float PingFrequency = 2.0f;

        // average out the last few results from Ping
        public static int PingWindowSize = 10;

        private static ExponentialMovingAverage rtt = new(10);
        private static ExponentialMovingAverage offset = new(10);

        // the true offset guaranteed to be in this range
        private static double offsetMin = double.MinValue;
        private static double offsetMax = double.MaxValue;

        public static double LocalTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnityEngine.Time.timeAsDouble;
        }

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod]
        public static void Reset()
        {
            PingFrequency = 2.0f;
            PingWindowSize = 10;
            rtt = new ExponentialMovingAverage(PingWindowSize);
            offset = new ExponentialMovingAverage(PingWindowSize);
            offsetMin = double.MinValue;
            offsetMax = double.MaxValue;
        }

        internal static PingPacket Ping => new() { lastPingPacketTime = LocalTime };

        // executed at the server when we receive a ping message
        // reply with a pong containing the time from the client
        // and time from the server
        internal static void OnServerPing(IncomingMessage msg)
        {
            var ping = msg.Deserialize(new PingPacket());

            var packet = new PongPacket
            {
                clientTime = ping.lastPingPacketTime,
                serverTime = LocalTime
            };

            UDPServer.instance.Send(msg.Sender, OpCode.Pong, packet);
        }

        // Executed at the client when we receive a Pong message
        // find out how long it took since we sent the Ping
        // and update time offset
        internal static void OnClientPong(IncomingMessage msg)
        {
            var pong = msg.Deserialize(new PongPacket());

            double now = LocalTime;

            // how long did this message take to come back
            double newRtt = now - pong.clientTime;
            rtt.Add(newRtt);

            // the difference in time between the client and the server
            // but subtract half of the rtt to compensate for latency
            // half of rtt is the best approximation we have
            double newOffset = now - newRtt * 0.5f - pong.serverTime;

            double newOffsetMin = now - newRtt - pong.serverTime;
            double newOffsetMax = now - pong.serverTime;
            offsetMin = Math.Max(offsetMin, newOffsetMin);
            offsetMax = Math.Min(offsetMax, newOffsetMax);

            if (offset.Value < offsetMin || offset.Value > offsetMax)
            {
                // the old offset was offrange,  throw it away and use new one
                offset = new ExponentialMovingAverage(PingWindowSize);
                offset.Add(newOffset);
            }
            else if (newOffset >= offsetMin || newOffset <= offsetMax)
            {
                // new offset looks reasonable,  add to the average
                offset.Add(newOffset);
            }
        }

        // returns the same time in both client and server
        // time should be a double because after a while
        // float loses too much accuracy if the server is up for more than
        // a few days.  I measured the accuracy of float and I got this:
        // for the same day,  accuracy is better than 1 ms
        // after 1 day,  accuracy goes down to 7 ms
        // after 10 days, accuracy is 61 ms
        // after 30 days , accuracy is 238 ms
        // after 60 days, accuracy is 454 ms
        // in other words,  if the server is running for 2 months,
        // and you cast down to float,  then the time will jump in 0.4s intervals.
        // Notice _offset is 0 at the server
        public static double Time
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => LocalTime - offset.Value;
        }

        // measure volatility of time.
        // the higher the number,  the less accurate the time is
        public static double TimeVar => offset.Variance;

        // standard deviation of time
        public static double TimeSd => Math.Sqrt(TimeVar);

        public static double Offset => offset.Value;

        // how long does it take for a message to go
        // to the server and come back
        public static double Rtt => rtt.Value;

        // measure volatility of rtt
        // the higher the number,  the less accurate rtt is
        public static double RttVar => rtt.Variance;

        // standard deviation of rtt
        public static double RttSd => Math.Sqrt(RttVar);
    }
}