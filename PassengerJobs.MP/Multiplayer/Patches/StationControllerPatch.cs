using HarmonyLib;
using MPAPI;

namespace PassengerJobs.MP.Multiplayer.Patches;

[HarmonyPatch(typeof(StationController))]
public static class StationControllerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StationController.Awake))]
    public static void AwakePostfix(StationController __instance)
    {
        if (MultiplayerAPI.Instance.IsHost)
            return;

        MultiplayerManager.Client?.RegisterForJobAddedEvents(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(StationController.CleanupStations))]
    public static void CleanupStationsPrefix()
    {
        if (MultiplayerAPI.Instance.IsHost)
            return;

        foreach (var controller in StationController.allStations)
            MultiplayerManager.Client?.UnregisterForJobAddedEvents(controller);
    }
}
