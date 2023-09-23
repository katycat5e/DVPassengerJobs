using DV.Logic.Job;
using HarmonyLib;
using PassengerJobs.Generation;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(Job))]
    internal class JobPatch
    {
        [HarmonyPatch(nameof(Job.GetPotentialBonusPaymentForTheJob))]
        [HarmonyPrefix]
        public static bool GetPotentialBonusPrefix(Job __instance, ref float __result)
        {
            if (__instance.jobType == PassJobType.Express)
            {
                __result = PassengerJobGenerator.GetBonusPayment(__instance.initialWage);
                return false;
            }
            return true;
        }
    }
}
