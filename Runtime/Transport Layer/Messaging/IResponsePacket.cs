namespace VaporNetworking
{
    public interface IResponsePacket
    {
        /// <summary>
        ///     ID of the response callback.
        /// </summary>
        int ResponseID { get; set; }

        /// <summary>
        ///     If the message response is complete this is true.
        /// </summary>
        bool CompleteResponse { get; set; }

        /// <summary>
        ///     Message status code
        /// </summary>
        ResponseStatus Status { get; set; }
    }
}