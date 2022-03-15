using DV.Logic.Job;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    public static class PassengerJobs
    {
        public static UnityModManager.ModEntry ModEntry;
        public static PJModSettings Settings { get; private set; }
        public static bool Enabled { get; private set; } = false;

        internal static UnityModManager.ModEntry SlicedCarsModEntry;

        public static bool SmallerCoachesEnabled
        {
            get => (SlicedCarsModEntry != null) && SlicedCarsModEntry.Active;
        }

        #region Enable/Disable

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            CargoTypes.cargoTypeToCargoMassPerUnit[CargoType.Passengers] = 3000f;

            if( AccessTools.Field(typeof(ResourceTypes), "cargoToFullCargoDamagePrice")?.GetValue(null) is Dictionary<CargoType, float> cdpDict )
            {
                cdpDict[CargoType.Passengers] = 70_000f;
            }
            else
            {
                ModEntry.Logger.Warning("Failed to adjust passenger damage cost");
            }

            try
            {
                PassengerLicenseUtil.RegisterPassengerLicenses();
            }
            catch( Exception ex )
            {
                var sb = new StringBuilder("Failed to inject new license definitions into LicenseManager:\n");
                for( ; ex != null; ex = ex.InnerException )
                {
                    sb.AppendLine(ex.Message);
                }
                ModEntry.Logger.Error(sb.ToString());

                return false;
            }

            PlatformManager.TryLoadSignLocations();

            // Initialize settings
            Settings = UnityModManager.ModSettings.Load<PJModSettings>(ModEntry);
            Settings.DoPurge = false;

            ModEntry.OnGUI = DrawGUI;
            ModEntry.OnSaveGUI = SaveGUI;

            // Find companion mods
            SkinManager_Patch.Initialize();
            DVTime_Patch.Initialize();

            SlicedCarsModEntry = UnityModManager.FindMod("SlicedPassengerCars");
            if( SmallerCoachesEnabled )
            {
                // can fit mas cars
                PassengerJobGenerator.MAX_CARS_COMMUTE += 1;
                PassengerJobGenerator.MAX_CARS_EXPRESS += 1;

                ModEntry.Logger.Log("Detected coach resize patch, making consists longer");
            }

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Enabled = true;

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

        #endregion
    }
}
