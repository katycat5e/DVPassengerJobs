using DV.Booklets;
using HarmonyLib;
using PassengerJobs.Generation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_JobMissingLicense))]
    internal static class BookletCreator_JobMissingLicensePatch
    {
        private static readonly Type _getTemplateDisplayClass;
        private static readonly FieldInfo _jobField;
        private static readonly FieldInfo _jobTypeField;
        private static readonly FieldInfo _jobColorField;

        static BookletCreator_JobMissingLicensePatch()
        {
            _getTemplateDisplayClass = AccessTools.TypeByName("DV.Booklets.BookletCreator_JobMissingLicense+<>c__DisplayClass2_0");
            _jobField = AccessTools.Field(_getTemplateDisplayClass, "job");
            _jobTypeField = AccessTools.Field(_getTemplateDisplayClass, "jobType");
            _jobColorField = AccessTools.Field(_getTemplateDisplayClass, "jobColor");
        }

        [HarmonyPatch(nameof(BookletCreator_JobMissingLicense.GetMissingLicenseTemplateData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileGetTemplateData(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var endSwitchLabel = generator.DefineLabel();

            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Ldstr) && ((string)instruction.operand).StartsWith("Unsupported"))
                {
                    // insert check for passenger job into default switch case before failure message
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Ldfld, _jobField);

                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Ldflda, _jobTypeField);

                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Ldflda, _jobColorField);

                    yield return CodeInstruction.Call(typeof(BookletCreator_JobMissingLicensePatch), nameof(CheckPassengerJobCases));

                    // if result is true, branch to end of switch else continue with failure path
                    yield return new CodeInstruction(OpCodes.Brtrue_S, endSwitchLabel);
                }
                else if (instruction.opcode == OpCodes.Ldarg_1)
                {
                    instruction.labels.Add(endSwitchLabel);
                }

                yield return instruction;
            }
        }

        private static bool CheckPassengerJobCases(Job_data job, ref string jobType, ref Color jobColor)
        {
            PJMain.Log($"Check job {job.ID}, {job.type}");
            if (job.type == PassJobType.Express)
            {
                jobType = LocalizationKey.JOB_EXPRESS_NAME.L();
                jobColor = BookletUtility.ExpressColor;
                return true;
            }

            if (job.type == PassJobType.Local)
            {
                jobType = LocalizationKey.JOB_REGIONAL_NAME.L();
                jobColor = BookletUtility.LocalColor;
                return true;
            }

            return false;
        }
    }
}
