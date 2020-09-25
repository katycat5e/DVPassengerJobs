using DV.Logic.Job;
using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    public static class PassengerJobs
    {
        internal static UnityModManager.ModEntry ModEntry;
        public static PJModSettings Settings { get; private set; }

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

            // Initialize settings
            Settings = UnityModManager.ModSettings.Load<PJModSettings>(ModEntry);
            ModEntry.OnGUI = DrawGUI;
            ModEntry.OnSaveGUI = SaveGUI;

            if( Settings.UniformConsists ) SkinManager_Patch.Initialize();

            var harmony = HarmonyInstance.Create("com.foxden.passenger_jobs");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

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
    }
}
