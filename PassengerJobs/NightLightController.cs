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

        protected void SetAllLightColours(Color lightColor)
        {
            foreach (var light in _lights)
            {
                light.color = lightColor;
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

        private TrainCar _trainCar = null!;
        private MeshRenderer _interior = null!;
        private Material _lampOff = null!;
        private Material? _lampOn;
        private bool _inOn = false;
        private bool _tempState = false;

        private GameObject _redHolder = null!;
        private GameObject[] _glaresF = null!;
        private GameObject[] _glaresR = null!;
        private MeshRenderer[] _lampsF = null!;
        private MeshRenderer[] _lampsR = null!;
        private bool _frontOn = false;
        private bool _rearOn = false;
        private bool _hasLoco = false;

        private Material InteriorMat
        {
            get => _interior.sharedMaterials[MaterialIndex];
            set
            {
                var mats = _interior.sharedMaterials;
                mats[MaterialIndex] = value;
                _interior.sharedMaterials = mats;
            }
        }
        private Material LampOff => _lampOff;
        private Material LampOn
        {
            get
            {
                if (_lampOn == null)
                {
                    _lampOn = LampHelper.GetLitMaterialFromModular(LampOff);
                }

                return _lampOn;
            }
        }

        public override void Awake()
        {
            base.Awake();
            _trainCar = TrainCar.Resolve(gameObject);
            _interior = _trainCar.transform.Find("CarPassenger/CarPassengerInterior_LOD0").GetComponent<MeshRenderer>();

            _trainCar.TrainsetChanged += OnTrainsetChanged;

            RefreshMaterials();
            StartCoroutine(Optimizer());

            PJMain.Settings.OnSettingsSaved += UpdateColours;
        }

        private void OnDestroy()
        {
            PJMain.Settings.OnSettingsSaved -= UpdateColours;
        }

        protected override bool GetNewLightState()
        {
            bool lightsPowered = !PJMain.Settings.CoachLightsRequirePower || AnyLocoPowered();

            return !PJMain.Settings.DisableCoachLights && base.GetNewLightState() && _hasLoco && lightsPowered;
        }

        internal void FeedRedLights(GameObject holder, GameObject[] glaresF, GameObject[] glaresR, MeshRenderer[] lampsF, MeshRenderer[] lampsR)
        {
            _redHolder = holder;
            _glaresF = glaresF;
            _glaresR = glaresR;
            _lampsF = lampsF;
            _lampsR = lampsR;

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

            InteriorMat = on ? LampOn : LampOff;
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
                item.sharedMaterial = on ? LampHelper.RedLitMaterial : LampHelper.RedUnlitMaterial;
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
                item.sharedMaterial = on ? LampHelper.RedLitMaterial : LampHelper.RedUnlitMaterial;
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

        private void OnTrainsetChanged(Trainset trainset)
        {
            _hasLoco = trainset.locoIndices.Count > 0;
        }

        private bool AnyLocoPowered()
        {
            foreach (var index in _trainCar.trainset.locoIndices)
            {
                var loco = _trainCar.trainset?.cars[index];
                if (loco == null) continue;

                //steam locos also have a cabLightsController, the power fuse is the dynamo
                var cabLights = loco.SimController.cabLightsController;
                if (cabLights && (cabLights.powerFuse?.State == true))
                {
                    return true;
                }
            }

            return false;
        }

        private void BeforeSkinChange()
        {
            _tempState = _inOn;
            ChangeInteriorLampMaterial(false);
        }

        private void AfterSkinChange()
        {
            RefreshMaterials();
            ChangeInteriorLampMaterial(_tempState);
        }

        private void RefreshMaterials()
        {
            _lampOff = InteriorMat;
            _lampOn = null;
        }

        protected void UpdateColours(PJModSettings settings)
        {
            LampHelper.RemoveFromCache(LampOff);
            BeforeSkinChange();
            AfterSkinChange();
            SetAllLightColours(LampHelper.LitColour);
        }
    }
}
