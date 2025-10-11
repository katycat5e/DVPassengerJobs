using MPAPI.Interfaces.Packets;

namespace PassengerJobs.MP.Multiplayer.Packets;

public class ClientBoundPJTransferCompletePacket : IPacket
{
    public ushort WarehouseMachineNetId { get; set; }
    public ushort TaskNetId { get; set; }
}