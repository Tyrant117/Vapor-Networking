
namespace VaporNetworking
{
    public delegate void IncomingMessageHandler(IncomingMessage message);

    public class PacketHandler
    {
        private readonly IncomingMessageHandler handler;
        private readonly short opCode;

        public PacketHandler(short opCode, IncomingMessageHandler handler)
        {
            this.opCode = opCode;
            this.handler = handler;
        }

        public short OpCode
        {
            get { return opCode; }
        }

        public void Handle(IncomingMessage msg)
        {
            handler.Invoke(msg);
        }
    }
}