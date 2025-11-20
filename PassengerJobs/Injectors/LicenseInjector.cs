using DV.Booklets.Rendered;
using DV.InventorySystem;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using System;
using UnityEngine;
// TODO: ????

namespace PassengerJobs.Injectors
{
    public interface ILicenseData
    {
        Color Color { get; }

        string Name { get; }

        float Cost { get; }
        float InsuranceIncrease { get; }
        float TimeDecrease { get; }

        string PrefabName { get; }
        string RenderPrefabName { get; }
        GameObject? RenderPrefab { get; set; }

        string SamplePrefabName { get; }
        string SampleRenderPrefabName { get; }
        GameObject? SampleRenderPrefab { get; set; }
    }

    public class PassengerLicenseData : ILicenseData
    {
        public Color Color { get; set; }

        public string Name { get; set; } = "";

        public float Cost { get; set; }
        public float InsuranceIncrease { get; set; }
        public float TimeDecrease { get; set; }

        public string PrefabName { get; set; } = "";
        public string RenderPrefabName { get; set; } = "";
        public GameObject? RenderPrefab { get; set; }

        public string SamplePrefabName { get; set; } = "";
        public string SampleRenderPrefabName { get; set; } = "";
        public GameObject? SampleRenderPrefab { get; set; }
    }

    public static class LicenseInjector
    {
        public static JobLicenseType_v2 License1 { get; internal set; } = null!;
        public static JobLicenseType_v2 License2 { get; internal set; } = null!;

        public static readonly ILicenseData License1Data = new PassengerLicenseData
        {
            Color = new Color(0.278f, 0.518f, 0.69f),
            Name = "P 1",
            Cost = 69_000f,
            InsuranceIncrease = 150_000f,
            TimeDecrease = 0.0f,
            PrefabName = "LicensePassengers1",
            RenderPrefabName = "PJLicense1Render",
            SamplePrefabName = "LicenseInfoPassengers1",
            SampleRenderPrefabName = "PJlicense1InfoRender"
        };

        // TODO: License2 should require License1
        public static readonly ILicenseData License2Data = new PassengerLicenseData
        {
            Color = new Color(0.278f, 0.518f, 0.69f),
            Name = "Passengers 2",
            Cost = 100_000f,
            InsuranceIncrease = 150_000f,
            TimeDecrease = 0.0f,
            PrefabName = "LicensePassengers2",
            RenderPrefabName = "PJLicense2Render",
            SamplePrefabName = "LicenseInfoPassengers2",
            SampleRenderPrefabName = "PJlicense2InfoRender"
        };

        public static bool RegisterPassengerLicenses()
        {
            BundleLoader.EnsureInitialized();

            try
            {
                License1 = CreatePassengerLicense(License1Data, (JobLicenses)64);
                License2 = CreatePassengerLicense(License2Data, (JobLicenses)128);
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to inject new license definitions into LicenseManager", ex);
                return false;
            }
            return true;
        }

        private static JobLicenseType_v2 CreatePassengerLicense(
            ILicenseData data,
            JobLicenses v1Flag)
        {
            var license = ScriptableObject.CreateInstance<JobLicenseType_v2>();
            license.id = license.name = data.Name;
            license.localizationKey = LocalizationKey.LICENSE_NAME.K(); // später evtl. pro Lizenz anders
            license.localizationKeysDescription = new[] { LocalizationKey.LICENSE_DESCRIPTION.K() };
            license.v1 = v1Flag;

            license.color = data.Color;
            license.price = data.Cost;
            license.insuranceFeeQuotaIncrease = data.InsuranceIncrease;
            license.bonusTimeDecreasePercentage = data.TimeDecrease;

            SetupLicensePrefabs(license, data);
            DV.Globals.G.Types.jobLicenses.Add(license);

            return license;
        }

        private static void SetupLicensePrefabs(JobLicenseType_v2 license, ILicenseData data)
        {
            var hazmatLicense = JobLicenses.Hazmat1.ToV2();

            // license book prefab
            license.licensePrefab = ModelUtility.CreateMockPrefab(hazmatLicense.licensePrefab);
            license.licensePrefab.name = data.PrefabName;
            var licenseRenderComp = license.licensePrefab.GetComponent<RuntimeRenderedStaticTextureBooklet>();
            string hazmatRenderName = licenseRenderComp.renderPrefabName;
            licenseRenderComp.renderPrefabName = data.RenderPrefabName;

            data.RenderPrefab = ModelUtility.CreateMockPrefab(Resources.Load<GameObject>(hazmatRenderName));
            data.RenderPrefab.name = data.RenderPrefabName;
            var staticRender = data.RenderPrefab.GetComponent<StaticLicenseBookletRender>();
            staticRender.jobLicense = license;

            // sample book prefab
            license.licenseInfoPrefab = ModelUtility.CreateMockPrefab(hazmatLicense.licenseInfoPrefab);
            license.licenseInfoPrefab.name = data.SamplePrefabName;
            var infoRenderComp = license.licenseInfoPrefab.GetComponent<RuntimeRenderedStaticTextureBooklet>();
            string hazmatInfoRenderName = infoRenderComp.renderPrefabName;
            infoRenderComp.renderPrefabName = data.SampleRenderPrefabName;

            data.SampleRenderPrefab = ModelUtility.CreateMockPrefab(Resources.Load<GameObject>(hazmatInfoRenderName));
            data.SampleRenderPrefab.name = data.SampleRenderPrefabName;
            var staticInfoRender = data.SampleRenderPrefab.GetComponent<StaticLicenseBookletRender>();
            staticInfoRender.jobLicense = license;
        }

        private static LicenseTemplatePaperData GetPassengerLicenseTemplateInternal(ILicenseData data)
        {
            // TODO: adjust language here for both licenses
            string name = LocalizationKey.LICENSE_NAME.L();
            string description = LocalizationKey.LICENSE_DESCRIPTION.L();

            Sprite shuntSprite = JobLicenses.Shunting.ToV2().icon;

            string costString = $"${data.Cost:F}";
            string insuranceIncrease = $"+${data.InsuranceIncrease:F}";
            string bonusDecrease = "N/A";

            // TODO: tidy up
            Sprite sprite;
            if (data.Name == "Passengers1") {
                sprite = BundleLoader.License1Sprite;
            } else
            {
                sprite = BundleLoader.License2Sprite;
            }

                return new LicenseTemplatePaperData(
                    name, description, data.Color, costString, insuranceIncrease, bonusDecrease, sprite, shuntSprite);
        }

        public static LicenseTemplatePaperData GetPassengerLicenseBasicTemplate()
            => GetPassengerLicenseTemplateInternal(License1Data);

        public static LicenseTemplatePaperData GetPassengerLicenseAdvancedTemplate()
            => GetPassengerLicenseTemplateInternal(License2Data);


        public static void SetLicense1Properties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, License1Data.PrefabName, LocalizationKey.LICENSE_ITEM_NAME.K());
        }

        public static void SetLicense1SampleProperties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, License1Data.SamplePrefabName, LocalizationKey.LICENSE_SAMPLE_ITEM_NAME.K());
        }

        public static void SetLicense2Properties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, License2Data.PrefabName, LocalizationKey.LICENSE_ITEM_NAME.K());
        }

        public static void SetLicense2SampleProperties(GameObject licenseObj)
        {
            SetBookletProperties(licenseObj, License2Data.SamplePrefabName, LocalizationKey.LICENSE_SAMPLE_ITEM_NAME.K());
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
            RefundSingleLicense(License1, License1Data);
            RefundSingleLicense(License2, License2Data);
        }

        private static void RefundSingleLicense(JobLicenseType_v2 license, ILicenseData data)
        {
            if (LicenseManager.Instance.IsJobLicenseAcquired(license))
            {
                LicenseManager.Instance.RemoveJobLicense(new[] { license });
                LicenseManager.Instance.SaveData(SaveGameManager.Instance.data);
                Inventory.Instance.AddMoney(data.Cost);
                PJMain.Log($"{data.Name} job license refunded and removed from player");
            }
            else PJMain.Log($"Player does not own {data.Name} job license");
        }

        public static void DestroySpawnedLicenses()
        {
            foreach (string bookName in new[] {
                License1Data.PrefabName, License1Data.SamplePrefabName,
                License2Data.PrefabName, License2Data.SamplePrefabName
            })
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
