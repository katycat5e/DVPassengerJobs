using MPAPI.Interfaces.Packets;
using UnityEngine;
using static PassengerJobs.PJModSettings;

namespace PassengerJobs.Multiplayer.Packets;

public class ClientBoundPJSettingsPacket : IPacket
{
    public bool UseCustomWages {get; set; }
    public CoachLightMode CoachLights { get; set; }
    public bool UseCustomCoachLightColour { get; set; }
    public Color CustomCoachLightColour { get; set; }
    public bool CoachLightsRequirePower { get; set; }
}