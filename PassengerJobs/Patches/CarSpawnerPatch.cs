using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CarSpawner))]
    internal class CarSpawnerPatch
    {
        private static readonly Vector3 RedPos1 = new Vector3(0.925f, 2.125f, 11.83f);
        private static readonly Vector3 RedPos2 = new Vector3(-0.925f, 2.125f, 11.83f);
        private static readonly Vector3 RedPos3 = new Vector3(0.925f, 2.125f, -11.83f);
        private static readonly Vector3 RedPos4 = new Vector3(-0.925f, 2.125f, -11.83f);
        private static readonly Quaternion Flipped = Quaternion.Euler(0, 180, 0);

        private static Transform? s_redGlare;
        private static Transform RedGlare
        {
            get
            {
                if (s_redGlare == null)
                {
                    s_redGlare = TrainCarType.LocoShunter.ToV2().prefab.transform.Find(
                        "[headlights_de2]/FrontSide/HeadlightTop/Glare");
                }

                return s_redGlare;
            }
        }

        [HarmonyPatch(nameof(CarSpawner.Awake))]
        [HarmonyPostfix]
        public static void AwakePostfix(CarSpawner __instance)
        {
            __instance.CarSpawned += AddLightsToCar;
        }

        private static void AddLightsToCar(TrainCar car)
        {
            if ((car.carType != TrainCarType.PassengerRed) && (car.carType != TrainCarType.PassengerGreen) && (car.carType != TrainCarType.PassengerBlue))
            {
                return;
            }

            AddEmissionToFixtures(car.gameObject);

            var lightHolder = new GameObject("[PJ Lights]");
            lightHolder.transform.SetParent(car.transform, false);
            lightHolder.transform.localPosition = new Vector3(0, 3.8f, 0);

            AddLightAtOffset(lightHolder.transform, new Vector3(0, 0, 6f));
            AddLightAtOffset(lightHolder.transform, new Vector3(0, 0, 0));
            AddLightAtOffset(lightHolder.transform, new Vector3(0, 0, -6f));

            var controller = car.gameObject.AddComponent<CoachLightController>();

            AddRedLights(car.transform, controller);
        }

        private static void AddEmissionToFixtures(GameObject carRoot)
        {
            foreach (var renderer in carRoot.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (!material.HasProperty("_t1")) continue;

                    if (material.GetTexture("_t1").name == "TT_MetalTrim_01d")
                    {
                        material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                    }
                }
            }
        }

        private static void AddLightAtOffset(Transform parent, Vector3 offset)
        {
            var holder = new GameObject("[PJ light source]");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = offset;

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(240, 230, 209, 255);
            light.intensity = 3;
            light.range = 6f;
        }

        private static void AddRedLights(Transform carRoot, CoachLightController controller)
        {
            var t = new GameObject("[PJ Red Lights]").transform;
            t.parent = carRoot;

            var l1 = Object.Instantiate(RedGlare, t);
            var l2 = Object.Instantiate(RedGlare, t);
            var l3 = Object.Instantiate(RedGlare, t);
            var l4 = Object.Instantiate(RedGlare, t);

            l1.localPosition = RedPos1;
            l2.localPosition = RedPos2;
            l3.localPosition = RedPos3;
            l4.localPosition = RedPos4;

            l1.localRotation = Quaternion.identity;
            l2.localRotation = Quaternion.identity;
            l3.localRotation = Flipped;
            l4.localRotation = Flipped;

            controller.FeedRedLights(new[] { l1, l2 }, new[] { l3, l4 });
        }
    }
}
