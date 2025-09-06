using HarmonyLib;
using MPAPI;
using MPAPI.Types;
using PassengerJobs.MP.Multiplayer;

namespace PassengerJobs.MP;

public static class Bootstrap
{
    public static void Initialize()
    {
        if (!MultiplayerAPI.IsMultiplayerLoaded)
            return;

        // Loaded API Version is the version of MultiplayerAPI.dll loaded in memory.
        // Supported API Version is the version of MultiplayerAPI.dll that Multiplayer mod was built against.
        // Supported API version must be equal to the Loaded API Version.
        PJMain.Log($"Multiplayer mod is loaded. Loaded Multiplayer API Version: {MultiplayerAPI.LoadedApiVersion}, Multiplayer's API Version: {MultiplayerAPI.SupportedApiVersion}");

        // All players are required to have Passenger Jobs mod installed.
        MultiplayerAPI.Instance.SetModCompatibility(PJMain.ModEntry.Info.Id, MultiplayerCompatibility.All);

        Harmony harmony = new("cc.foxden.passenger_jobs.mp");
        harmony.PatchAll();

        MultiplayerManager.Init();
    }
}
