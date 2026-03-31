#nullable disable
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using PassengerJobs.Injectors;
using System.IO;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BundleLoader
    {
        public static Sprite License1Sprite { get; private set; }
        public static Sprite License2Sprite { get; private set; }

        private static bool MasterLoadFailed
        {
            set
            {
                SignLoadFailed = value;
                PlatformLoadFailed = value;
            }
        }

        public static bool SignLoadFailed { get; private set; }
        public static GameObject SignPrefab;
        public static GameObject SmallSignPrefab;
        public static GameObject LillySignPrefab;

        public static bool PlatformLoadFailed { get; private set; }
        public static GameObject RuralPlatform;
        public static GameObject RuralPlatformNoBase;

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

            License1Sprite = TryLoadSprite(bundle, "Assets/Passengers1.png");
            License2Sprite = TryLoadSprite(bundle, "Assets/Passengers2.png");

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

        private static Sprite TryLoadSprite(AssetBundle bundle, string assetPath)
        {
            var sprite = bundle.LoadAsset<Sprite>(assetPath);
            if (!sprite)
            {
                PJMain.Error($"Failed to load sprite ({assetPath}) from asset bundle");
            }
            else
            {
                Object.DontDestroyOnLoad(sprite);
            }
            return sprite;
        }

        public static void HandleSaveReload()
        {
            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            License1Sprite = TryLoadSprite(bundle, "Assets/Passengers1.png");
            LicenseInjector.License1.icon = License1Sprite;

            License2Sprite = TryLoadSprite(bundle, "Assets/Passengers2.png");
            LicenseInjector.License2.icon = License2Sprite;

            bundle.Unload(false);
        }

        private static Shader _engineShader = null;

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
