using HarmonyLib;
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
                CarSpawner.Instance.DeleteTrainCars(__instance.trainCarsForJobChain, true);
                __instance.trainCarsForJobChain.Clear();
            }
        }
    }
}
