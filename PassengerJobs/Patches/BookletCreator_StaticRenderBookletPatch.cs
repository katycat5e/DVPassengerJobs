using DV.Booklets;
using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_StaticRenderBooklet))]
    internal static class BookletCreator_StaticRenderBookletPatch
    {
        private static readonly MethodInfo _resourcesLoadMethod =
            AccessTools.Method(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Type) });

        [HarmonyPatch(nameof(BookletCreator_StaticRenderBooklet.Render))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            // replace call to Resources.Load() in order to inject passenger prefabs
            bool skipping = false;

            foreach (var instruction in instructions)
            {
                // skip until after the Resources.Load call
                if (skipping)
                {
                    if (instruction.Calls(_resourcesLoadMethod))
                    {
                        skipping = false;
                    }
                    continue;
                }

                if (instruction.opcode == OpCodes.Ldtoken)
                {
                    yield return CodeInstruction.Call((string s) => LoadRenderPrefab(s));
                    skipping = true;
                    continue;
                }

                yield return instruction;
            }
        }

        private static UnityEngine.Object LoadRenderPrefab(string name)
        {
            var result = name switch
            {
                LicenseData.RenderPrefabName => LicenseData.RenderPrefab,
                LicenseData.SampleRenderPrefabName => LicenseData.SampleRenderPrefab,
                _ => Resources.Load<GameObject>(name),
            };

            result!.SetActive(true);
            return result;
        }
    }
}
