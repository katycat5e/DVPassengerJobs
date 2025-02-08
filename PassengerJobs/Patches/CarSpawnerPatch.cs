using DV.Simulation.Cars;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CarSpawner))]
    internal class CarSpawnerPatch
    {
        private static readonly Vector3 RedPos1 = new(0.925f, 2.125f, 11.83f);
        private static readonly Vector3 RedPos2 = new(-0.925f, 2.125f, 11.83f);
        private static readonly Vector3 RedPos3 = new(0.925f, 2.125f, -11.83f);
        private static readonly Vector3 RedPos4 = new(-0.925f, 2.125f, -11.83f);
        private static readonly Quaternion Flipped = Quaternion.Euler(0, 180, 0);

        private static readonly Vector3 RedPosMesh1 = new(0.925f, 0.588f, 10.00f);
        private static readonly Vector3 RedPosMesh2 = new(-0.925f, 0.588f, 10.00f);
        private static readonly Vector3 RedPosMesh3 = new(0.925f, 0.588f, -10.00f);
        private static readonly Vector3 RedPosMesh4 = new(-0.925f, 0.588f, -10.00f);
        private static readonly Vector3 RedMeshScale = new(0.55f, 0.55f, 0.55f);

        private static Headlight? s_redGlare;
        private static MeshRenderer? s_redLamp;
        private static Headlight RedGlare
        {
            get
            {
                if (s_redGlare == null)
                {
                    s_redGlare = TrainCarType.LocoShunter.ToV2().prefab.transform.Find(
                        "[headlights_de2]/FrontSide/HeadlightTop").GetComponent<Headlight>();
                }

                return s_redGlare;
            }
        }
        private static MeshRenderer RedLamp
        {
            get
            {
                if (s_redLamp == null)
                {
                    s_redLamp = TrainCarType.LocoShunter.ToV2().prefab.transform.Find(
                        "[headlights_de2]/FrontSide/ext headlights_glass_red_F").GetComponent<MeshRenderer>();
                }

                return s_redLamp;
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
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            var g1 = Object.Instantiate(RedGlare.glare, t);
            var g2 = Object.Instantiate(RedGlare.glare, t);
            var g3 = Object.Instantiate(RedGlare.glare, t);
            var g4 = Object.Instantiate(RedGlare.glare, t);

            g1.transform.localPosition = RedPos1;
            g2.transform.localPosition = RedPos2;
            g3.transform.localPosition = RedPos3;
            g4.transform.localPosition = RedPos4;

            g1.transform.localRotation = Quaternion.identity;
            g2.transform.localRotation = Quaternion.identity;
            g3.transform.localRotation = Flipped;
            g4.transform.localRotation = Flipped;

            g1.gameObject.AddComponent<SortingGroup>().sortingOrder = 10;
            g2.gameObject.AddComponent<SortingGroup>().sortingOrder = 10;
            g3.gameObject.AddComponent<SortingGroup>().sortingOrder = 10;
            g4.gameObject.AddComponent<SortingGroup>().sortingOrder = 10;

            var l1 = Object.Instantiate(RedLamp, t);
            var l2 = Object.Instantiate(RedLamp, t);
            var l3 = Object.Instantiate(RedLamp, t);
            var l4 = Object.Instantiate(RedLamp, t);

            l1.transform.localPosition = RedPosMesh1;
            l2.transform.localPosition = RedPosMesh2;
            l3.transform.localPosition = RedPosMesh3;
            l4.transform.localPosition = RedPosMesh4;

            l1.transform.localRotation = Quaternion.identity;
            l2.transform.localRotation = Quaternion.identity;
            l3.transform.localRotation = Flipped;
            l4.transform.localRotation = Flipped;

            l1.transform.localScale = RedMeshScale;
            l2.transform.localScale = RedMeshScale;
            l3.transform.localScale = RedMeshScale;
            l4.transform.localScale = RedMeshScale;

            controller.FeedRedLights(t.gameObject, new[] { g1, g2 }, new[] { g3, g4 },
                new[] { l1, l2 }, new[] { l3, l4 }, RedGlare.emissionMaterialLit, RedGlare.emissionMaterialUnlit);
        }
    }
}
