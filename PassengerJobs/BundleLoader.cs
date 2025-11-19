using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using PassengerJobs.Injectors;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BundleLoader
    {
        public static Sprite License1Sprite { get; private set; } = null!;
        public static Sprite License2Sprite { get; private set; } = null!;

        private static bool MasterLoadFailed
        {
            set
            {
                SignLoadFailed = value;
                PlatformLoadFailed = value;
            }
        }

        public static bool SignLoadFailed { get; private set; }
        public static GameObject SignPrefab = null!;
        public static GameObject SmallSignPrefab = null!;
        public static GameObject LillySignPrefab = null!;

        public static bool PlatformLoadFailed { get; private set; }
        public static GameObject RuralPlatform = null!;
        public static GameObject RuralPlatformNoBase = null!;

        public static void EnsureInitialized()
        {
            if (SignPrefab && RuralPlatform) return;

            MasterLoadFailed = false;

            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");
            PJMain.Log("Attempting to load asset bundle contents");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if (bundle == null)
            {
                PJMain.Error("Failed to load asset bundle");
                MasterLoadFailed = true;
                return;
            }

            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/FlatscreenSign.prefab", ref SignPrefab);
            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/SmolStationSign.prefab", ref SmallSignPrefab);
            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/LillySign.prefab", ref LillySignPrefab);

            PlatformLoadFailed |= !TryLoadPrefab(bundle, "Assets/Platforms/RuralPlatform.prefab", ref RuralPlatform);
            PlatformLoadFailed |= !TryLoadPrefab(bundle, "Assets/Platforms/RuralPlatformNoBase.prefab", ref RuralPlatformNoBase);

            bundle.Unload(false);
        }

        private static bool TryLoadPrefab(AssetBundle bundle, string assetPath, ref GameObject prefab)
        {
            prefab = bundle.LoadAsset<GameObject>(assetPath);
            if (prefab)
            {
                ApplyDefaultShader(prefab);
                return true;
            }

            PJMain.Error($"Failed to load {assetPath} from asset bundle");
            return false;
        }

        public static void HandleSaveReload()
        {
            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            License1Sprite = bundle.LoadAsset<Sprite>("Assets/Passengers1.png");
            License2Sprite = bundle.LoadAsset<Sprite>("Assets/Passengers2.png");
            UnityEngine.Object.DontDestroyOnLoad(License1Sprite);
            UnityEngine.Object.DontDestroyOnLoad(License2Sprite);
            if (License1Sprite == null)
            {
                PJMain.Error("Failed to load license sprite from asset bundle");
            }
            else
            {
                LicenseInjector.License1.icon = License1Sprite;
            }
            if (License2Sprite == null)
            {
                PJMain.Error("Failed to load license sprite from asset bundle");
            }
            else
            {
                LicenseInjector.License1.icon = License2Sprite;
            }

            bundle.Unload(false);
        }

        private static Shader? _engineShader = null;

        private static Shader EngineShader
        {
            get
            {
                if (!_engineShader)
                {
                    var prefab = TrainCarType.LocoShunter.ToV2().prefab;
                    var exterior = prefab.transform.Find("LocoDE2_Body/ext 621_exterior");
                    var material = exterior.GetComponent<MeshRenderer>().material;
                    _engineShader = material.shader;
                }
                return _engineShader!;
            }
        }

        private static void ApplyDefaultShader(GameObject prefab)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    // replace opaque material shader
                    if ((material.shader.name == "Standard") && (material.GetFloat("_Mode") == 0))
                    {
                        material.shader = EngineShader;
                    }
                }
            }
        }
    }
}
