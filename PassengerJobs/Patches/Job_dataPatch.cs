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
    [HarmonyPatch(typeof(Job_data))]
    internal class Job_dataPatch
    {
        private static readonly MethodInfo _selectMethod = typeof(Enumerable)
            .GetMethods()
            .Single(m => (m.Name == "Select") && (m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2))
            .MakeGenericMethod(typeof(Task), typeof(Task_data));

        [HarmonyPatch(MethodType.Constructor, typeof(Job))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileCtor(IEnumerable<CodeInstruction> instructions)
        {
            bool skipping = false;
            foreach (var instr in instructions)
            {
                if (skipping)
                {
                    if (instr.Calls(_selectMethod))
                    {
                        skipping = false;
                        yield return CodeInstruction.Call((IEnumerable<Task> t) => GetTaskData(t));
                    }
                }
                else
                {
                    if (instr.opcode == OpCodes.Ldsfld)
                    {
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
