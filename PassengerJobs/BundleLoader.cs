using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BundleLoader
    {
        public static Sprite LicenseSprite { get; private set; } = null!;

        public static bool SignLoadFailed { get; private set; }
        public static GameObject SignPrefab { get; private set; } = null!;
        public static GameObject SmallSignPrefab { get; private set; } = null!;
        public static GameObject LillySignPrefab { get; private set; } = null!;

        public static void Initialize()
        {
            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");
            PJMain.Log("Attempting to load platform sign prefab");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if (bundle != null)
            {
                LicenseSprite = bundle.LoadAsset<Sprite>("Assets/Passengers1.png");
                UnityEngine.Object.DontDestroyOnLoad(LicenseSprite);
                if (LicenseSprite == null)
                {
                    PJMain.Error("Failed to load license sprite from asset bundle");
                }

                SignPrefab = bundle.LoadAsset<GameObject>("Assets/FlatscreenSign.prefab");
                if (SignPrefab == null)
                {
                    PJMain.Error("Failed to load platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                }

                SmallSignPrefab = bundle.LoadAsset<GameObject>("Assets/SmolStationSign.prefab");
                if (SmallSignPrefab == null)
                {
                    PJMain.Error("Failed to load small platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                }

                LillySignPrefab = bundle.LoadAsset<GameObject>("Assets/LillySign.prefab");
                if (LillySignPrefab == null)
                {
                    PJMain.Error("Failed to load small platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                }
            }
            else
            {
                PJMain.Error("Failed to load asset bundle");
                SignLoadFailed = true;
            }
        }
    }
}
