using MPAPI.Interfaces.Packets;

namespace PassengerJobs.MP.Multiplayer.Packets;

public class ClientBoundPJPlatformStatePacket : IPacket
{
    public ushort WarehouseMachineNetId { get; set; }
    public ushort JobNetId { get; set; }
    public LocalizationKey State { get; set; }
}