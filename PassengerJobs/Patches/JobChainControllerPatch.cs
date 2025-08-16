using HarmonyLib;
using System.Linq;
using PassengerJobs.Generation;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(JobChainController))]
    internal class JobChainControllerPatch
    {
        [HarmonyPatch(nameof(JobChainController.GetJobChainSaveData))]
        [HarmonyPostfix]
        public static void GetJobChainSaveDataPostfix(JobChainController __instance, ref JobChainSaveData __result)
        {
            if (__instance is PassengerChainController)
            {
                __result = new PassengerChainSaveData(__result);
            }
        }

        [HarmonyPatch(nameof(JobChainController.OnAnyJobFromChainAbandoned))]
        [HarmonyPrefix]
        public static void OnJobAbandonedPrefix(JobChainController __instance)
        {
            if (__instance is PassengerChainController)
            {
                var trainCars = __instance.carsForJobChain.Select(lc => TrainCarRegistry.Instance.logicCarToTrainCar[lc]).ToList();
                CarSpawner.Instance.DeleteTrainCars(trainCars, true);
                __instance.carsForJobChain.Clear();
            }
        }
    }
}
