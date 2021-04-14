
namespace VaporNetworking
{
    /// <summary>
    ///     Message codes. Assign new ones in a partial class. Numbers below 100 should be reserved for the framework.
    /// </summary>
    public partial class OpCode
    {
        public const short Error = -1;

        public const short Register = 1;

        // These codes are IResponse Packets that request the server to respond to them.
        public const short LoginRequest = 2;
        public const short NewCharacterRequest = 3;
        public const short PlayerJoinWorldRequest = 4;

        public const short Ping = 60;
        public const short Pong = 61;

        public const short Profile = 100;
        public const short Interest = 101;
        public const short Path = 102;

        public const short Command = 104;
        public const short Admin = 105;
        public const short DamagePacket = 106;
        public const short Animation = 107;
        public const short QuickCommand = 108;

        public const short CreatureSpawn = 109;
        public const short PlayerSpawn = 110;

        public const short Move = 120;
        public const short AnimationState = 121;

        public const short Interact = 150;
        public const short DynamicInteract = 151;

        public const short LootBagRequest = 200;

        // Steamworks
        public const short AttemptP2PConnection = 1000;
    }
}