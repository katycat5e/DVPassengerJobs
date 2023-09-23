using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CarSpawner))]
    internal static class CarSpawnerPatch
    {
        public static bool PJCall = false;

        [HarmonyPatch(nameof(CarSpawner.GetTrackMiddleBasedSpawnData))]
        [HarmonyPostfix]
        public static void GetSpawnDataPostfix(ref CarSpawner.SpawnData __result)
        {
            if (PJCall)
            {
                PJMain.Log($"Spawn: {__result.track.logicTrack.ID} {__result.carData.Length} cars, result = {__result.result}, {__result.message}");
            }
        }
    }
}
