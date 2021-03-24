
namespace VaporNetworking
{
    public enum ResponseStatus
    {
        /// <summary>
        ///     Default is either when the message has no response required or when the local player responds to himself.
        ///     It should always be handled in the response callback as the local player responding to himself.
        ///     Other peers should never respond with default.
        /// </summary>
        Default = 0,
        Success = 1,
        Timeout = 2,
        Failed = 3,
        /// <summary>
        /// Invalid is called when the player has done something that should not be possible. It is a special case of failure.
        /// </summary>
        Invalid = 4
    }

    public enum ConnectionStatus
    {
        None,
        Connecting,
        Connected,
        Disconnected
    }
}