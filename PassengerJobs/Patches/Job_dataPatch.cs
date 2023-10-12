using DV.Booklets;
using DV.Logic.Job;
using HarmonyLib;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace PassengerJobs.Patches
{
    [HarmonyPatch]
    internal class Job_dataPatch
    {
        private static readonly MethodInfo _selectMethod = typeof(Enumerable)
            .GetMethods()
            .Single(m => (m.Name == "Select") && (m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2))
            .MakeGenericMethod(typeof(Task), typeof(Task_data));

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> GetTargetMethods()
        {
            return new[]
            {
                AccessTools.Constructor(typeof(Job_data), new[] { typeof(Job) }),
                AccessTools.Constructor(typeof(Task_data), new[] { typeof(Task) })
            };
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileJob_dataCtor(IEnumerable<CodeInstruction> instructions)
        {
            bool skipping = false;
            CodeInstruction? preSkip = null;

            foreach (var instr in instructions)
            {
                if (skipping)
                {
                    if (instr.Calls(_selectMethod))
                    {
                        skipping = false;
                        var newCall = CodeInstruction.Call((IEnumerable<Task> t) => GetTaskData(t));

                        if (preSkip != null)
                        {
                            newCall.MoveLabelsFrom(preSkip);
                        }

                        newCall.MoveLabelsFrom(instr);
                        yield return newCall;
                    }
                }
                else
                {
                    if (instr.opcode == OpCodes.Ldsfld)
                    {
                        preSkip = instr;
                        skipping = true;
                    }
                    else
                    {
                        yield return instr;
                    }
                }
            }
        }

        private static IEnumerable<Task_data> GetTaskData(IEnumerable<Task> tasks)
        {
            foreach (var task in tasks)
            {
                if (task is RuralLoadingTask ruralTask)
                {
                    yield return new RuralTask_data(ruralTask);
                }
                else
                {
                    yield return new Task_data(task);
                }
            }
        }
    }
}
