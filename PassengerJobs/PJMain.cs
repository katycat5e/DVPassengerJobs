using DVLangHelper.Runtime;
using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
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
            Translations.AddTranslationsFromCsv(Path.Combine(ModEntry.Path, "translations.csv"));
            Translations.AddTranslationsFromWebCsv("https://docs.google.com/spreadsheets/d/1sQ26qpB6czqGC0ObV6Y7OfwIEqPtGm1SBCLYvp47PSY/export?format=csv&gid=1132930393");

            // inject licenses
            if (!LicenseInjector.RegisterPassengerLicenses()) return false;

            // load route config
            if (!RouteManager.LoadConfig()) return false;

            SignManager.TryLoadSignLocations();

            CargoInjector.RegisterPassengerCargo();

            //PlatformManager.TryLoadSignLocations();

            // Initialize settings
            ReloadSettings();
            //Settings.DoPurge = false;

            ModEntry.OnGUI = DrawGUI;
            ModEntry.OnSaveGUI = SaveGUI;

            // Find companion mods
            TryLoadSkinManager();
            MultiplayerShim.TryInitialise();

            DV.Globals.G.Types.RecalculateCaches();

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

#if DEBUG
            var dtAssembly = Bootstrapper.TryLoadAssembly("PassengerJobs.DebugTools.dll");

            if (dtAssembly is not null)
            {
                Log("Loaded Debug Tools");
                harmony.PatchAll(dtAssembly);
            }
#endif

            return true;
        }
        
        private static void TryLoadSkinManager()
        {
            if (UnityModManager.FindMod("SkinManagerMod")?.Active == true)
            {
                if (Bootstrapper.TryLoadAssembly("PassengerJobs.Skins.dll") is not null)
                {
                    Log("Activated Skin Manager integration");
                }
            }
            else
            {
                Log("Skin Manager not found, skipping integration");
            }
        }

        #endregion

        #region Settings

        public static void ReloadSettings()
        {
            Settings = UnityModManager.ModSettings.Load<PJModSettings>(ModEntry);
        }

        static void DrawGUI( UnityModManager.ModEntry entry )
        {
            Settings.Draw(entry);

            if (Settings.MPActive)
            {
                GUILayout.Label("<color=\"red\">Settings are locked while a multiplayer session is active.</color>");
            }
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
        public static void LogDebug(string msg)
        {
#if DEBUG
            ModEntry.Logger.Log($"[Debug] {msg}");
#endif
        }

         #endregion
    }
}
