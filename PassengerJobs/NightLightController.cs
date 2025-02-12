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
        private TrainCar _trainCar = null!;
        private GameObject _redHolder = null!;
        private GameObject[] _glaresF = null!;
        private GameObject[] _glaresR = null!;
        private MeshRenderer[] _lampsF = null!;
        private MeshRenderer[] _lampsR = null!;
        private Material _onMat = null!;
        private Material _offMat = null!;
        private bool _frontOn = false;
        private bool _rearOn = false;
        private bool _hasLoco = false;

        public override void Awake()
        {
            base.Awake();
            _trainCar = TrainCar.Resolve(gameObject);

            _trainCar.TrainsetChanged += OnTrainsetChanged;

            StartCoroutine(Optimizer());
        }

        protected override bool GetNewLightState()
        {
            bool lightsPowered = !PJMain.Settings.CoachLightsRequirePower || AnyLocoPowered();

            return !PJMain.Settings.DisableCoachLights && base.GetNewLightState() && _hasLoco && lightsPowered;
        }

        internal void FeedRedLights(GameObject holder, GameObject[] glaresF, GameObject[] glaresR, MeshRenderer[] lampsF, MeshRenderer[] lampsR, Material onMat, Material offMat)
        {
            _redHolder = holder;
            _glaresF = glaresF;
            _glaresR = glaresR;
            _lampsF = lampsF;
            _lampsR = lampsR;
            _onMat = onMat;
            _offMat = offMat;

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

            ChangeFrontLights(!_trainCar.frontCoupler.coupledTo && lightsOn);
            ChangeRearLights(!_trainCar.rearCoupler.coupledTo && lightsOn);
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
                item.material = on ? _onMat : _offMat;
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
                item.material = on ? _onMat : _offMat;
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
                if (cabLights != null && cabLights.powerFuse.State)
                    return true;
            }

            return false;
        }
    }
}
