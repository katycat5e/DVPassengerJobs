using DV.Simulation.Cars;
using System.Linq;
using UnityEngine;

namespace PassengerJobs
{
    public abstract class NightLightController : MonoBehaviour
    {
        private bool _currentlyLit;
        private SpriteLightsEvent _events = null!;
        private Light[] _lights = null!;

        public virtual void Awake()
        {
            _events = WorldTimeBasedEvents.Instance.GetComponent<SpriteLightsEvent>();
            _lights = GetComponentsInChildren<Light>();
        }

        public void Start()
        {
            SetLightsOn(true);
        }

        public void Update()
        {
            bool lightsOn = GetNewLightState();

            if (lightsOn != _currentlyLit)
            {
                SetLightsOn(lightsOn);
            }
        }

        protected virtual bool GetNewLightState()
        {
            return _events.LightTypeOn[(int)SpriteLightType.StreetSpriteLight];
        }

        protected virtual void SetLightsOn(bool lightsOn)
        {
            _currentlyLit = lightsOn;

            foreach (var light in _lights)
            {
                light.enabled = lightsOn;
            }
        }
    }

    public class PlatformLightController : NightLightController
    {
        private Renderer[] _renderers = null!;

        public override void Awake()
        {
            base.Awake();
            _renderers = GetComponentsInChildren<Renderer>();
        }

        protected override void SetLightsOn(bool lightsOn)
        {
            base.SetLightsOn(lightsOn);

            Color emitColor = lightsOn ? Color.white : Color.black;
            foreach (var renderer in _renderers)
            {
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_EmissionMap"))
                    {
                        material.SetColor("_EmissionColor", emitColor);
                    }
                }
            }
        }
    }

    public class CoachLightController : NightLightController
    {
        private const int MaterialIndex = 3;

        private static Material? _litMat;
        private static Material LitMaterial
        {
            get
            {
                if (_litMat == null)
                {
                    // Copy the lit version of the MetalTrim material.
                    DV.Globals.G.Types.TryGetLivery("LocoDM1U", out var dm1u);
                    _litMat = new Material(dm1u.prefab.GetComponentInChildren<CabLightsController>().lightsLit);

                    // I want a texture with the same size and I want it painted black.
                    var og = _litMat.mainTexture;
                    var tex = new Texture2D(og.width, og.height);
                    int size = og.width * og.height;
                    Color[] pixels = new Color[size];

                    for (int i = 0; i < size; i++)
                    {
                        pixels[i] = Color.black;
                    }

                    tex.SetPixels(pixels);
                    tex.Apply();

                    // Copy the texture region corresponding to the lamp part only.
                    // Leave out a 1px border above and below to avoid bleeding.
                    Graphics.CopyTexture(og, 0, 0, 0, 1, 512, 254, tex, 0, 0, 0, 1);

                    // Replace the emission texture with this.
                    _litMat.SetTexture("_EmissionMap", tex);
                }

                return _litMat;
            }
        }

        private TrainCar _trainCar = null!;
        private MeshRenderer _interior = null!;
        private Material _lampMat = null!;
        private bool _inOn = false;

        private GameObject _redHolder = null!;
        private GameObject[] _glaresF = null!;
        private GameObject[] _glaresR = null!;
        private MeshRenderer[] _lampsF = null!;
        private MeshRenderer[] _lampsR = null!;
        private Material _redOnMat = null!;
        private Material _redOffMat = null!;
        private bool _frontOn = false;
        private bool _rearOn = false;

        private Material InteriorMat
        {
            get => _interior.materials[MaterialIndex];
            set
            {
                var mats = _interior.materials;
                mats[MaterialIndex] = value;
                _interior.materials = mats;
            }
        }

        public override void Awake()
        {
            base.Awake();
            _trainCar = TrainCar.Resolve(gameObject);
            _interior = _trainCar.transform.Find("CarPassenger/CarPassengerInterior_LOD0").GetComponent<MeshRenderer>();
            _lampMat = InteriorMat;

            StartCoroutine(Optimizer());
        }

        protected override bool GetNewLightState()
        {
            return !PJMain.Settings.DisableCoachLights && base.GetNewLightState() && IsLocoConnected;
        }

        private bool IsLocoConnected => _trainCar.brakeSystem.brakeset.cars.Any(b => b.hasCompressor);

        internal void FeedRedLights(GameObject holder, GameObject[] glaresF, GameObject[] glaresR, MeshRenderer[] lampsF, MeshRenderer[] lampsR, Material onMat, Material offMat)
        {
            _redHolder = holder;
            _glaresF = glaresF;
            _glaresR = glaresR;
            _lampsF = lampsF;
            _lampsR = lampsR;
            _redOnMat = onMat;
            _redOffMat = offMat;

            foreach (var item in glaresF)
            {
                item.SetActive(false);
            }

            foreach (var item in glaresR)
            {
                item.SetActive(false);
            }
        }

        protected override void SetLightsOn(bool lightsOn)
        {
            base.SetLightsOn(lightsOn);

            ChangeInteriorLampMaterial(lightsOn);
            ChangeFrontLights(!_trainCar.frontCoupler.coupledTo && lightsOn);
            ChangeRearLights(!_trainCar.rearCoupler.coupledTo && lightsOn);
        }

        private void ChangeInteriorLampMaterial(bool on)
        {
            if (_inOn == on) return;

            InteriorMat = on ? LitMaterial : _lampMat;
            _inOn = on;
        }

        private void ChangeFrontLights(bool on)
        {
            if (_frontOn == on) return;

            foreach (var item in _glaresF)
            {
                item.SetActive(on);
            }

            foreach (var item in _lampsF)
            {
                item.material = on ? _redOnMat : _redOffMat;
            }

            _frontOn = on;
        }

        private void ChangeRearLights(bool on)
        {
            if (_rearOn == on) return;

            foreach (var item in _glaresR)
            {
                item.SetActive(on);
            }

            foreach (var item in _lampsR)
            {
                item.material = on ? _redOnMat : _redOffMat;
            }

            _rearOn = on;
        }

        private  System.Collections.IEnumerator Optimizer()
        {
            while (true)
            {
                yield return WaitFor.Seconds(0.3f);

                var player = PlayerManager.PlayerTransform;

                if (player == null) continue;

                _redHolder.SetActive(Vector3.SqrMagnitude(player.position - _redHolder.transform.position) <= 2250000);
            }
        }
    }
}
