using DV.RenderTextureSystem.BookletRender;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassengerJobsMod
{
    static class PassLicenses
    {
        public const JobLicenses Passengers1 = (JobLicenses)64;
    }

    static class PassengerLicenseUtil
    {
        public static readonly Color PASSENGER_LICENSE_COLOR = new Color(0.278f, 0.518f, 0.69f);
        public const string PASS1_LICENSE_NAME = "Passengers 1";
        public const string PASS1_LICENSE_DESC =
            "This license grants you access to jobs hauling empty and full passenger coaches.";

        public const float PASS1_COST = 100_000f;
        // same as hazmat 2
        public const float PASS1_INSURANCE_INCREASE = 150_000f;
        public const float PASS1_TIME_DECREASE = 0.04f;


        private static FieldInfo licensePriceField = typeof(LicenseManager).GetField("jobLicenseToPrice", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo quotaBonusField = typeof(LicenseManager).GetField("jobLicenseToInsuranceQuotaAndBonusTimeUpdateData", BindingFlags.NonPublic | BindingFlags.Static);
        public static void RegisterPassengerLicenses()
        {

        }

        public static LicenseTemplatePaperData GetPassengerLicenseTemplate()
        {
            if( !IconsSpriteMap.jobLicenseToSpriteIcon.TryGetValue(PassLicenses.Passengers1, out Sprite licenseIcon) )
            {
                PassengerJobs.ModEntry.Logger.Warning("Couldn't load passenger license icon");
            }

            string costString = $"${PASS1_COST:F}";
            string insuranceIncrease = $"+${PASS1_INSURANCE_INCREASE:F}";

            float percentBonusDecrease = PASS1_TIME_DECREASE * 100f;
            string bonusDecrease = $"-{percentBonusDecrease:F}%";

            return new LicenseTemplatePaperData(
                PASS1_LICENSE_NAME, PASS1_LICENSE_DESC, PASSENGER_LICENSE_COLOR, costString, insuranceIncrease, bonusDecrease, licenseIcon);
        }
    }
}
