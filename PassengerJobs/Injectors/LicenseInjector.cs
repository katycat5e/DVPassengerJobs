using DV.Booklets.Rendered;
using DV.InventorySystem;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using System;
using UnityEngine;

namespace PassengerJobs.Injectors
{
    public static class LicenseData
    {
        public static readonly Color Color = new(0.278f, 0.518f, 0.69f);
        public const string Name = "Passengers";

        public const float Cost = 100_000f;
        // same as hazmat 2
        public const float InsuranceIncrease = 150_000f;
        public const float TimeDecrease = 0.0f;

        public const string PrefabName = "LicensePassengers1";
        public const string RenderPrefabName = "PJLicenseRender";
        public static GameObject? RenderPrefab = null;

        public const string SamplePrefabName = "LicenseInfoPassengers1";
        public const string SampleRenderPrefabName = "PJlicenseInfoRender";
        public static GameObject? SampleRenderPrefab = null;
    }

    public static class LicenseInjector
    {
        public static JobLicenseType_v2 License { get; internal set; } = null!;

        public static bool RegisterPassengerLicenses()
        {
            try
            {
                // create license data object
                License = ScriptableObject.CreateInstance<JobLicenseType_v2>();
                License.id = License.name = LicenseData.Name;
                License.localizationKey = LocalizationKey.LICENSE_NAME.K();
                License.localizationKeysDescription = new[] { LocalizationKey.LICENSE_DESCRIPTION.K() };
                License.v1 = (JobLicenses)64;

                License.color = LicenseData.Color;
                License.icon = BundleLoader.LicenseSprite;

                License.price = LicenseData.Cost;
                License.insuranceFeeQuotaIncrease = LicenseData.InsuranceIncrease;
                License.bonusTimeDecreasePercentage = LicenseData.TimeDecrease;

                SetupLicensePrefabs();


                DV.Globals.G.Types.jobLicenses.Add(License);
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to inject new license definitions into LicenseManager", ex);
                return false;
            }
            return true;
        }

        private static void SetupLicensePrefabs()
        {
            // license book prefab
            var hazmatLicense = JobLicenses.Hazmat1.ToV2();
            License.licensePrefab = ModelUtility.CreateMockPrefab(hazmatLicense.licensePrefab);
            License.licensePrefab.name = LicenseData.PrefabName;
            var licenseRenderComp = License.licensePrefab.GetComponent<RuntimeRenderedStaticTextureBooklet>();
            string hazmatRenderName = licenseRenderComp.renderPrefabName;
            licenseRenderComp.renderPrefabName = LicenseData.RenderPrefabName;

            // license render prefab
            LicenseData.RenderPrefab = ModelUtility.CreateMockPrefab(Resources.Load<GameObject>(hazmatRenderName));
            LicenseData.RenderPrefab.name = LicenseData.RenderPrefabName;
            var staticRender = LicenseData.RenderPrefab.GetComponent<StaticLicenseBookletRender>();
            staticRender.jobLicense = License;

            // sample book prefab
            License.licenseInfoPrefab = ModelUtility.CreateMockPrefab(hazmatLicense.licenseInfoPrefab);
            License.licenseInfoPrefab.name = LicenseData.SamplePrefabName;
            var infoRenderComp = License.licenseInfoPrefab.GetComponent<RuntimeRenderedStaticTextureBooklet>();
            string hazmatInfoRenderName = infoRenderComp.renderPrefabName;
            infoRenderComp.renderPrefabName = LicenseData.SampleRenderPrefabName;

            // sample render prefab
            LicenseData.SampleRenderPrefab = ModelUtility.CreateMockPrefab(Resources.Load<GameObject>(hazmatInfoRenderName));
            LicenseData.SampleRenderPrefab.name = LicenseData.SampleRenderPrefabName;
            var staticInfoRender = LicenseData.SampleRenderPrefab.GetComponent<StaticLicenseBookletRender>();
            staticInfoRender.jobLicense = License;
        }

        public static LicenseTemplatePaperData GetPassengerLicenseTemplate()
        {
            string name = LocalizationKey.LICENSE_NAME.L();
            string description = LocalizationKey.LICENSE_DESCRIPTION.L();

            Sprite shuntSprite = JobLicenses.Shunting.ToV2().icon;

            string costString = $"${LicenseData.Cost:F}";
            string insuranceIncrease = $"+${LicenseData.InsuranceIncrease:F}";

            //float percentBonusDecrease = PASS1_TIME_DECREASE * 100f;
            string bonusDecrease = "N/A"; // "$"-{percentBonusDecrease:F}%";

            return new LicenseTemplatePaperData(
                name, description, LicenseData.Color, costString, insuranceIncrease, bonusDecrease, BundleLoader.LicenseSprite, shuntSprite);
        }

        public static void SetLicenseProperties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, LicenseData.PrefabName, LocalizationKey.LICENSE_ITEM_NAME.K());
        }

        public static void SetLicenseSampleProperties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, LicenseData.SamplePrefabName, LocalizationKey.LICENSE_SAMPLE_ITEM_NAME.K());
        }

        private static void SetBookletProperties(GameObject licenseObj, string bookletName, string nameLocalKey)
        {
            licenseObj.name = bookletName;

            // get the inventory spec component
            InventoryItemSpec itemSpec = licenseObj.GetComponent<InventoryItemSpec>();
            if (itemSpec)
            {
                itemSpec.localizationKeyName = nameLocalKey;
                itemSpec.itemPrefabName = bookletName;
            }
            else PJMain.Warning($"Couldn't set inventory name on {bookletName}");

            licenseObj.SetActive(true);
        }

        public static void RefundLicenses()
        {
            if (LicenseManager.Instance.IsJobLicenseAcquired(License))
            {
                LicenseManager.Instance.RemoveJobLicense(new[] { License });
                LicenseManager.Instance.SaveData(SaveGameManager.Instance.data);
                Inventory.Instance.AddMoney(LicenseData.Cost);
                PJMain.Log($"{LicenseData.Name} job license refunded and removed from player");
            }
            else PJMain.Log($"Player does not own {LicenseData.Name} job license");
        }

        public static void DestroySpawnedLicenses()
        {
            foreach (string bookName in new[] { LicenseData.PrefabName, LicenseData.SamplePrefabName } )
            {
                GameObject licenseObj = GameObject.Find(bookName);
                if( licenseObj != null )
                {
                    PJMain.Log($"Deleting booklet {bookName}");
                    UnityEngine.Object.Destroy(licenseObj);
                }
            }
        }
    }
}
