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
        public class LicenseSprites
        {
            public readonly string BaseName;

            public LicenseSprites(string baseName)
            {
                BaseName = baseName;
            }

            public Sprite Icon;
            public string IconPath => $"Assets/Licenses/{BaseName}.png";

            public Sprite ItemSprite;
            public string ItemSpritePath => $"Assets/Licenses/{BaseName}Item.png";

            public Sprite ItemSampleSprite;
            public string ItemSampleSpritePath => $"Assets/Licenses/{BaseName}ItemSample.png";
        }

        public static bool SpritesLoadFailed { get; private set; }
        public static readonly LicenseSprites Pass1Sprites = new("Passengers1");
        public static readonly LicenseSprites Pass2Sprites = new("Passengers2");

        private static bool MasterLoadFailed
        {
            set
            {
                SpritesLoadFailed = value;
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

            SpritesLoadFailed |= !TryLoadSprites(bundle, Pass1Sprites);
            SpritesLoadFailed |= !TryLoadSprites(bundle, Pass2Sprites);

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

        private static bool TryLoadSprites(AssetBundle bundle, LicenseSprites sprites)
        {
            bool success = true;
            success &= TryLoadSprite(bundle, sprites.IconPath, out sprites.Icon);
            success &= TryLoadSprite(bundle, sprites.ItemSpritePath, out sprites.ItemSprite);
            success &= TryLoadSprite(bundle, sprites.ItemSampleSpritePath, out sprites.ItemSampleSprite);
            return success;
        }
        
        private static bool TryLoadSprite(AssetBundle bundle, string assetPath, out Sprite sprite)
        {
            sprite = bundle.LoadAsset<Sprite>(assetPath);
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

            SpritesLoadFailed = false;

            SpritesLoadFailed |= !TryLoadSprites(bundle, Pass1Sprites);
            LicenseInjector.License1.icon = Pass1Sprites.Icon;

            TryLoadSprites(bundle, Pass2Sprites);
            LicenseInjector.License2.icon = Pass2Sprites.Icon;

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
