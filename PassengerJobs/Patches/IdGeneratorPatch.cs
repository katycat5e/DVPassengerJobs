using DV.Logic.Job;
using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(IdGenerator))]
    internal class IdGeneratorPatch
    {
        [HarmonyPatch(nameof(IdGenerator.GenerateJobID))]
        [HarmonyPrefix]
        public static bool GenerateJobIdPrefix(IdGenerator __instance, JobType jobType, StationsChainData jobStationsInfo, ref string __result)
        {
            if (!PassJobType.IsPJType(jobType))
            {
                return true;
            }

            string? yardId = null;
            if (jobStationsInfo != null)
            {
                yardId = jobStationsInfo.chainOriginYardId;
            }

            string typeStr = (jobType == PassJobType.Express) ? "PE" : "PR";
            string? idStr = FindUnusedID(typeStr, yardId);

            if (idStr != null)
            {
                __instance.RegisterJobId(idStr);
                __result = idStr;
            }
            else
            {
                PJMain.Warning($"Couldn't find free jobId for job type: {typeStr}! Using 0 for jobId number!");
                __result = (yardId != null) ? $"{yardId}-{typeStr}-{0:D2}" : $"{typeStr}-{0:D2}";
            }

            return false;
        }

        private static string? FindUnusedID(string typeStr, string? yardId = null)
        {
            int idNum = IdGenerator.idRng.Next(0, 100);

            for (int attemptNum = 0; attemptNum < 99; attemptNum++)
            {
                string idStr = (yardId != null) ? $"{yardId}-{typeStr}-{idNum:D2}" : $"{typeStr}-{idNum:D2}";

                if (!IdGenerator.Instance.existingJobIds.Contains(idStr))
                {
                    return idStr;
                }

                idNum = (idNum >= 99) ? 0 : (idNum + 1);
            }

            return null;
        }
    }
}
