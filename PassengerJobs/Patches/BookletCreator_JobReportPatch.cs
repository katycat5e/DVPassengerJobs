using DV.Booklets;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using HarmonyLib;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace PassengerJobs.Patches
{
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
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // bool showWarning
                    yield return CodeInstruction.Call(typeof(BookletCreator_JobReportPatch), nameof(ExtractRuralTaskData));
                }
            }
        }

        private static void ExtractRuralTaskData(
            Task_data task_data, List<JobReportTasksTemplatePaperData.JobReportEntry> entries, bool showWarning)
        {
            if (task_data is not RuralTask_data rural_data) return;

            if (showWarning && !(rural_data.state == TaskState.Done))
            {

            }
        }
    }
}
