using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DV.RenderTextureSystem.BookletRender.VehicleCatalogPageTemplatePaper;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(SaveGameManager))]
    internal class SaveGameManagerPatch
    {
        [HarmonyPatch(nameof(SaveGameManager.DoSaveIO))]
        [HarmonyPrefix]
        public static void InjectPassengersSaveData(SaveGameData data)
        {
            SaveDataInjector.InjectDataIntoSaveGame(data);

            // refund license in case mod is uninstalled
            if (LicenseManager.Instance.IsJobLicenseAcquired(LicenseInjector.License1))
            {
                float? money = data.GetFloat(SaveGameKeys.Player_money);
                if (money.HasValue)
                {
                    float newBalance = money.Value + LicenseInjector.License1Data.Cost;
                    data.SetFloat(SaveGameKeys.Player_money, newBalance);
                }
            }
            if (LicenseManager.Instance.IsJobLicenseAcquired(LicenseInjector.License2))
            {
                float? money = data.GetFloat(SaveGameKeys.Player_money);
                if (money.HasValue)
                {
                    float newBalance = money.Value + LicenseInjector.License2Data.Cost;
                    data.SetFloat(SaveGameKeys.Player_money, newBalance);
                }
            }
        }

        [HarmonyPatch(nameof(SaveGameManager.FindStartGameData))]
        [HarmonyPostfix]
        public static void ExtractPassengersSaveData(SaveGameManager __instance)
        {
            SaveDataInjector.loadedData = null;
            if (__instance.data == null) return;

            SaveDataInjector.ExtractDataFromSaveGame(__instance.data);
        }
    }
}
