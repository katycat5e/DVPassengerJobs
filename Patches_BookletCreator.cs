using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DV.RenderTextureSystem.BookletRender;
using DV.Logic.Job;
using UnityEngine.UI;
using UnityEngine;

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
}
