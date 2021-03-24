
namespace VaporNetworking
{
    public interface IClientMessages
    {
        void Send(short opcode, ISerializablePacket packet);
        //void Send(short opcode, ISerializablePacket packet, ResponseCallback callback, int timeout = 5);
        int RegisterResponse(ResponseCallback callback, int timeout = 5);
    }
}