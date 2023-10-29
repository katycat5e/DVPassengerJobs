using HarmonyLib;
using PassengerJobs.Generation;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.Awake))]
    internal class StationJobsControllerPatch
    {
        public static void Postfix(StationProceduralJobsController __instance)
        {
            string yardId = __instance.stationController.stationInfo.YardID;
            if (!RouteManager.IsPassengerStation(yardId)) return;

            RouteManager.OnStationControllerStart(__instance.stationController);
            __instance.gameObject.AddComponent<PassengerJobGenerator>();
        }
    }
}
