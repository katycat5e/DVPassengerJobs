using System;
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
        private Transform[] _redLightsF = Array.Empty<Transform>();
        private Transform[] _redLightsR = Array.Empty<Transform>();

        public override void Awake()
        {
            base.Awake();
            _trainCar = TrainCar.Resolve(gameObject);
        }

        protected override bool GetNewLightState()
        {
            return !PJMain.Settings.DisableCoachLights && base.GetNewLightState() && IsLocoConnected;
        }

        private bool IsLocoConnected => _trainCar.brakeSystem.brakeset.cars.Any(b => b.hasCompressor);

        internal void FeedRedLights(Transform[] redLightsF, Transform[] redLightsR)
        {
            _redLightsF = redLightsF;
            _redLightsR = redLightsR;

            foreach (var item in redLightsF)
            {
                item.gameObject.SetActive(false);
            }

            foreach (var item in redLightsR)
            {
                item.gameObject.SetActive(false);
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
            foreach (var item in _redLightsF)
            {
                item.gameObject.SetActive(on);
            }
        }

        private void ChangeRearLights(bool on)
        {
            foreach (var item in _redLightsR)
            {
                item.gameObject.SetActive(on);
            }
        }
    }
}
