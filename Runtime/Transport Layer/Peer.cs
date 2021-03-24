using System;

namespace VaporNetworking
{
    public delegate void PeerActionHandler(Peer peer);

    public delegate void ResponseCallback(ResponseStatus status, ISerializablePacket response);

    [Serializable]
    public class Peer : IDisposable
    {
        // Server and Connection Info
        public readonly int connectionID;
        public bool IsConnected { get; set; }

        public Peer(int connectionID)
        {
            this.connectionID = connectionID;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose) { }

        #region --- Connection ---
        /// <summary>
        ///     Force a disconnect. Only works on the server.
        /// </summary>
        /// <param name="reason">Reason error code</param>
        public void Disconnect(int reason = 0)
        {
            IsConnected = false;
            UDPTransport.DisconnectPeer(connectionID);
        }
        #endregion

        #region --- Messaging ---
        /// <summary>
        ///     General implentation to send a message over the network. 
        /// </summary>
        public bool SendMessage(ArraySegment<byte> msg, UDPTransport.Source source)
        {
            if (!IsConnected) { return false; }

            return UDPTransport.Send(connectionID, msg, source);
        }

        public bool SendSimulatedMessage(ArraySegment<byte> msg, UDPTransport.Source source)
        {
            if (!IsConnected) { return false; }

            return UDPTransport.SendSimulated(connectionID, msg, source);
        }
        #endregion

        #region - Response Management -
        /// <summary>
        ///     Registers a response.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="timeout">Seconds</param>
        /// <returns></returns>
        public virtual int RegisterResponse(ResponseCallback callback, int timeout)
        {
            return -1;
        }

        /// <summary>
        ///     Triggers the response if the msg has one.
        /// </summary>
        /// <param name="msg"></param>
        public virtual void TriggerResponse(int responseID, ResponseStatus status, ISerializablePacket msg)
        {

        }
        #endregion
    }
}