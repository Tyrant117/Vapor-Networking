using System;

namespace VaporNetworking
{
    public struct SimulatedMessage
    {
        public readonly int connectionID;
        public readonly SimulatedEventType eventType;
        public readonly byte[] data;

        public SimulatedMessage(int connectionID, SimulatedEventType eventType, byte[] data)
        {
            this.connectionID = connectionID;
            this.eventType = eventType;
            this.data = data;
        }
    }
}