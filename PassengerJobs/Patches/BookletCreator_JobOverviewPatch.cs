using DV.Booklets;
using DV.RenderTextureSystem.BookletRender;
using HarmonyLib;
using PassengerJobs.Generation;
using System.Collections.Generic;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_JobOverview))]
    internal static class BookletCreator_JobOverviewPatch
    {
        [HarmonyPatch(nameof(BookletCreator_JobOverview.GetJobOverviewTemplateData))]
        [HarmonyPrefix]
        public static bool GetJobOverviewTemplateDataPrefix(Job_data job, ref List<TemplatePaperData> __result)
        {
            if (PassJobType.IsPJType(job.type))
            {
                __result = GetOverviewTemplateData(job);
                return false;
            }
            return true;
        }

        public static List<TemplatePaperData> GetOverviewTemplateData(Job_data job)
        {
            var passData = BookletUtility.ExtractPassengerJobData(job);
            return new() { BookletUtility.CreatePassengerOverviewPage(passData) };
        }
    }
}
