namespace VaporNetworking
{
    public struct ResponseTimeoutPacket : ISerializablePacket, IResponsePacket
    {
        public int ResponseID { get; set; }
        public bool CompleteResponse { get; set; }
        public ResponseStatus Status { get; set; }

        public void Deserialize(NetReader r)
        {
        }

        public void Serialize(NetWriter w)
        {
        }
    }
}
