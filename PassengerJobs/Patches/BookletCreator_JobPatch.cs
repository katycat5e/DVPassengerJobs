using DV.Booklets;
using DV.RenderTextureSystem.BookletRender;
using HarmonyLib;
using PassengerJobs.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_Job))]
    internal static class BookletCreator_JobPatch
    {
        [HarmonyPatch(nameof(BookletCreator_Job.GetBookletTemplateData))]
        [HarmonyPrefix]
        public static bool GetBookletTemplateDataPrefix(Job_data job, ref List<TemplatePaperData> __result)
        {
            if (job.type == PassJobType.Express)
            {
                __result = GetBookletTemplateData(job);
                return false;
            }
            return true;
        }

        public static List<TemplatePaperData> GetBookletTemplateData(Job_data job)
        {
            var passData = BookletUtility.ExtractPassengerJobData(job);
            return BookletUtility.CreateExpressJobBooklet(passData).ToList();
        }
    }
}
