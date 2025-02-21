using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using PassengerJobs.Injectors;
using System.IO;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BundleLoader
    {
        public static Sprite LicenseSprite { get; private set; } = null!;

        public static bool SignLoadFailed { get; private set; }
        public static GameObject SignPrefab = null!;
        public static GameObject SmallSignPrefab = null!;
        public static GameObject LillySignPrefab = null!;

        public static GameObject RuralPlatform = null!;
        public static GameObject RuralPlatformNoBase = null!;

        public static void EnsureInitialized()
        {
            if (SignPrefab && RuralPlatform) return;

            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");
            PJMain.Log("Attempting to load platform sign prefab");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if (bundle == null)
            {
                PJMain.Error("Failed to load asset bundle");
                SignLoadFailed = true;
                return;
            }

            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/FlatscreenSign.prefab", ref SignPrefab);
            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/SmolStationSign.prefab", ref SmallSignPrefab);
            SignLoadFailed |= !TryLoadPrefab(bundle, "Assets/LillySign.prefab", ref LillySignPrefab);

            TryLoadPrefab(bundle, "Assets/Platforms/RuralPlatform.prefab", ref RuralPlatform);
            TryLoadPrefab(bundle, "Assets/Platforms/RuralPlatformNoBase.prefab", ref RuralPlatformNoBase);

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

            PJMain.Error($"Failed to load {prefab} from asset bundle");
            return false;
        }

        public static void HandleSaveReload()
        {
            string bundlePath = Path.Combine(PJMain.ModEntry.Path, "passengerjobs");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            LicenseSprite = bundle.LoadAsset<Sprite>("Assets/Passengers1.png");
            UnityEngine.Object.DontDestroyOnLoad(LicenseSprite);
            if (LicenseSprite == null)
            {
                PJMain.Error("Failed to load license sprite from asset bundle");
            }
            else
            {
                LicenseInjector.License.icon = LicenseSprite;
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
