using DV.Booklets;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using HarmonyLib;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace PassengerJobs.Patches
{
    using EntryState = JobReportTasksTemplatePaperData.EntryState;

    [HarmonyPatch(typeof(BookletCreator_JobReport))]
    internal class BookletCreator_JobReportPatch
    {
        [HarmonyPatch(nameof(BookletCreator_JobReport.ExtractTaskInfo))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileGetTaskData(IEnumerable<CodeInstruction> instructions)
        {
            // insert handling of rural task after switch jump table (intercept default case)
            // V_0 = List<JobReportEntry> list
            // V_4 = Task_data task_data

            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Switch)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // Task_data
                    yield return new CodeInstruction(OpCodes.Ldloc_0); // List<JobReportEntry>
                    yield return new CodeInstruction(OpCodes.Ldarga_S, 1); // ref bool showWarning
                    yield return CodeInstruction.Call(typeof(BookletCreator_JobReportPatch), nameof(ExtractRuralTaskData));
                }
            }
        }

        private static void ExtractRuralTaskData(
            Task_data task_data, List<JobReportTasksTemplatePaperData.JobReportEntry> entries, ref bool showWarning)
        {
            if (task_data is not RuralTask_data rural_data) return;

            bool anyTaskWarnings = false;

            if (showWarning && (rural_data.state != TaskState.Done))
            {
                if (rural_data.cars.Any(c => c.derailed))
                {
                    entries.Add(new(LocalizationAPI.L("job/report_derailed"), string.Empty, EntryState.WARNING));
                    anyTaskWarnings = true;
                }
                else
                {
                    bool anyCarPresent = false;
                    bool allCarsPresent = true;

                    foreach (var car_data in rural_data.cars)
                    {
                        anyCarPresent |= car_data.isOnDestinationTrack;
                        allCarsPresent &= car_data.isOnDestinationTrack;
                    }

                    if (anyCarPresent && !allCarsPresent)
                    {
                        entries.Add(new(LocalizationAPI.L("job/report_missing"), string.Empty, EntryState.WARNING));
                        anyTaskWarnings = true;
                    }
                }
            }

            string statusKey = rural_data.isLoading ? "job/report_load_cars" : "job/report_unload_cars";
            string statusText = LocalizationAPI.L(statusKey, new[]
            {
                LocalizationAPI.L(CargoInjector.PassengerCargo.localizationKeyShort),
                rural_data.stationId
            });

            string completionTime = string.Empty;
            EntryState taskState;
            if (rural_data.state == TaskState.Done)
            {
                completionTime = TimeSpan.FromSeconds(rural_data.taskFinishTime - rural_data.taskStartTime).ToString("hh\\:mm\\:ss");
                taskState = EntryState.COMPLETED;
            }
            else
            {
                taskState = anyTaskWarnings ? EntryState.IN_PROGRESS_WITH_X_MARK : EntryState.IN_PROGRESS;
            }

            if (anyTaskWarnings)
            {
                statusText += LocalizationAPI.L("job/report_see_warnings");
                entries.Insert(entries.Count - 1, new(statusText, completionTime, taskState));
            }
            else
            {
                entries.Add(new(statusText, completionTime, taskState));
            }

            showWarning = showWarning && !anyTaskWarnings;
        }
    }
}
