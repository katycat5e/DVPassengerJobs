using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DV.RenderTextureSystem.BookletRender;
using DV.Logic.Job;
using UnityEngine.UI;
using UnityEngine;
using System.Reflection;

namespace PassengerJobsMod
{
    // BookletCreator.GetJobLicenseTemplateData
    [HarmonyPatch(typeof(BookletCreator), "GetJobLicenseTemplateData")]
    class BC_GetTemplate_Patch
    {
        static bool Prefix( JobLicenses jobLicense, ref LicenseTemplatePaperData __result )
        {
            if( jobLicense == PassLicenses.Passengers1 )
            {
                // override the BookletCreator method
                __result = PassengerLicenseUtil.GetPassengerLicenseTemplate();
                return false;
            }

            return true;
        }
    }

    // FrontPageTemplatePaper.DisplayRequiredLicenses
    [HarmonyPatch(typeof(FrontPageTemplatePaper), "DisplayRequiredLicenses")]
    class FPTP_DisplayLicenses_Patch
    {
        static void Postfix( JobLicenses requiredLicenses, Image[] ___requiredLicenseSlots )
        {
            if( requiredLicenses.HasFlag(PassLicenses.Passengers1) )
            {
                // get first non-active slot
                Image slot = ___requiredLicenseSlots.FirstOrDefault(img => !img.gameObject.activeSelf);
                if( slot == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Can't fit Passengers 1 license on job overview");
                    return;
                }

                if( PassengerLicenseUtil.Pass1Sprite == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Missing icon for {PassengerLicenseUtil.PASS1_LICENSE_NAME}");
                    return;
                }

                slot.sprite = PassengerLicenseUtil.Pass1Sprite;
                slot.gameObject.SetActive(true);
            }
        }
    }

    // BookletCreator.CreateLicense()
    [HarmonyPatch(typeof(BookletCreator), nameof(BookletCreator.CreateLicense))]
    [HarmonyPatch(new Type[] { typeof(JobLicenses), typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    static class BC_CreateLicense_Patch
    {
        public const string COPIED_PREFAB_NAME = "LicenseHazmat1";
        private static readonly MethodInfo spawnLicenseMethod = AccessTools.Method(typeof(BookletCreator), "SpawnLicenseRelatedPrefab");

        static bool Prefix( JobLicenses license, Vector3 position, Quaternion rotation, Transform parent )
        {
            if( license != PassLicenses.Passengers1 ) return true;

            // we'll try to copy from the Hazmat 1 license prefab
            GameObject licenseObj = spawnLicenseMethod.Invoke(null,
                new object[] { COPIED_PREFAB_NAME, position, rotation, true, parent }) as GameObject;

            PassengerLicenseUtil.SetLicenseObjectProperties(licenseObj, PassBookletType.Passengers1License);

            PassengerJobs.ModEntry.Logger.Log("Created Passengers 1 license");
            return false;
        }
    }

    // BookletCreator.CreateLicenseInfo()
    [HarmonyPatch(typeof(BookletCreator), nameof(BookletCreator.CreateLicenseInfo))]
    [HarmonyPatch(new Type[] { typeof(JobLicenses), typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    static class BC_CreateLicenseInfo_Patch
    {
        public const string COPIED_PREFAB_NAME = "LicenseHazmat1Info";
        private static readonly MethodInfo spawnLicenseMethod = AccessTools.Method(typeof(BookletCreator), "SpawnLicenseRelatedPrefab");

        static bool Prefix( JobLicenses license, Vector3 position, Quaternion rotation, Transform parent )
        {
            if( license != PassLicenses.Passengers1 ) return true;

            // we'll try to copy the Hazmat 1 info prefab
            GameObject infoObj = spawnLicenseMethod.Invoke(null,
                new object[] { COPIED_PREFAB_NAME, position, rotation, false, parent }) as GameObject;

            PassengerLicenseUtil.SetLicenseObjectProperties(infoObj, PassBookletType.Passengers1Info);

            PassengerJobs.ModEntry.Logger.Log("Created Passengers 1 info page");
            return false;
        }
    }
}
