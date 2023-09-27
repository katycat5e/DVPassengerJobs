using DV.Common;
using DVLangHelper.Runtime;
using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityModManagerNet;

namespace PassengerJobs
{
    public static class PJMain
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; } = null!;
        public static PJModSettings Settings { get; private set; } = null!;
        public static bool Enabled => ModEntry.Active;
        public static TranslationInjector Translations { get; internal set; } = null!;


        #region Enable/Disable

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;

            Translations = new TranslationInjector("cc.foxden.passenger_jobs");
            Translations.AddTranslationsFromWebCsv("https://docs.google.com/spreadsheets/d/1sQ26qpB6czqGC0ObV6Y7OfwIEqPtGm1SBCLYvp47PSY/export?format=csv");

            BundleLoader.Initialize();

            // inject licenses
            if (!LicenseInjector.RegisterPassengerLicenses()) return false;
            
            // load route config
            if (!RouteSelector.LoadConfig()) return false;

            SignManager.TryLoadSignLocations();

            CargoInjector.RegisterPassengerCargo();

            //PlatformManager.TryLoadSignLocations();

            // Initialize settings
            Settings = UnityModManager.ModSettings.Load<PJModSettings>(ModEntry);
            //Settings.DoPurge = false;

            ModEntry.OnGUI = DrawGUI;
            ModEntry.OnSaveGUI = SaveGUI;

            // Find companion mods
            //SkinManager_Patch.Initialize();

            DV.Globals.G.Types.RecalculateCaches();

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        #endregion

        #region Settings

        static void DrawGUI( UnityModManager.ModEntry entry )
        {
            Settings.Draw(entry);
        }

        static void SaveGUI( UnityModManager.ModEntry entry )
        {
            Settings.Save(entry);
        }

        #endregion

        #region Logging

        public static void Log( string msg ) => ModEntry.Logger.Log(msg);
        public static void Warning( string msg ) => ModEntry.Logger.Warning(msg);
        public static void Error( string msg ) => ModEntry.Logger.Error(msg);
        public static void Error(string msg, Exception ex)
        {
            ModEntry.Logger.LogException(ex);
            ModEntry.Logger.Error(msg);
        }

        #endregion
    }

    //[HarmonyPatch(typeof(FastTravelController))]
    //internal static class FastTravelPatch
    //{
    //    private static readonly MethodInfo _isAllowedMethod = AccessTools.Method(typeof(GameFeatureFlags), nameof(GameFeatureFlags.IsAllowed));

    //    [HarmonyPatch(nameof(FastTravelController.OnFastTravelRequested))]
    //    [HarmonyTranspiler]
    //    public static IEnumerable<CodeInstruction> IsAllowedPrefix(IEnumerable<CodeInstruction> instructions)
    //    {
    //        bool first = true;
    //        bool prevWasCall = false;
    //        foreach (var instruction in instructions)
    //        {
    //            if (prevWasCall)
    //            {
    //                yield return new CodeInstruction(OpCodes.Brfalse_S, instruction.operand);
    //                prevWasCall = false;
    //                continue;
    //            }
    //            else
    //            {
    //                yield return instruction;
    //            }

    //            if (instruction.Calls(_isAllowedMethod))
    //            {
    //                if (first)
    //                {
    //                    first = false;
    //                }
    //                else
    //                {
    //                    prevWasCall = true;
    //                }
    //            }
    //        }
    //    }
    //}
}
