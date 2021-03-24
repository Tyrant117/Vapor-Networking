
namespace VaporNetworking
{
    public interface IServerMessages
    {
        void Send(short opcode, ISerializablePacket packet);

        void Send(Peer peer, short opcode, ISerializablePacket packet);

        //void Send(Peer peer, short opcode, ISerializablePacket packet, int responseID, bool comepleteResponse, ResponseStatus status);
    }
}