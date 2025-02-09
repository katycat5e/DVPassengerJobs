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
        private static readonly Vector3 RedPos1 = new(0.925f, 2.125f, 11.77f);
        private static readonly Vector3 RedPos2 = new(-0.925f, 2.125f, 11.77f);
        private static readonly Vector3 RedPos3 = new(0.925f, 2.125f, -11.77f);
        private static readonly Vector3 RedPos4 = new(-0.925f, 2.125f, -11.77f);
        private static readonly Quaternion Flipped = Quaternion.Euler(0, 180, 0);
        private static readonly Vector3 RedGlareScale = new(1.2f, 1.2f, 1.2f);

        private static readonly Vector3 RedPosMesh1 = new(0.925f, 0.588f, 10.00f);
        private static readonly Vector3 RedPosMesh2 = new(-0.925f, 0.588f, 10.00f);
        private static readonly Vector3 RedPosMesh3 = new(0.925f, 0.588f, -10.00f);
        private static readonly Vector3 RedPosMesh4 = new(-0.925f, 0.588f, -10.00f);
        private static readonly Vector3 RedMeshScale = new(0.55f, 0.55f, 0.55f);

        private static TrainCarLivery? _de2;
        private static Headlight? s_redGlare;
        private static MeshRenderer? s_redLamp;

        private static TrainCarLivery DE2
        {
            get
            {
                if (_de2 == null)
                {
                    DV.Globals.G.Types.TryGetLivery("LocoDE2", out _de2);
                }

                return _de2;
            }
        }
        private static Headlight RedGlare
        {
            get
            {
                if (s_redGlare == null)
                {
                    s_redGlare = DE2.prefab.transform.Find(
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
                    s_redLamp = DE2.prefab.transform.Find(
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
            var holder = new GameObject("[PJ Red Lights]").transform;
            holder.parent = carRoot;
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;

            // Create a light for all 4 positions.
            CreateLightSet(holder, RedPos1, RedPosMesh1, true, out var g1, out var l1);
            CreateLightSet(holder, RedPos2, RedPosMesh2, true, out var g2, out var l2);
            CreateLightSet(holder, RedPos3, RedPosMesh3, false, out var g3, out var l3);
            CreateLightSet(holder, RedPos4, RedPosMesh4, false, out var g4, out var l4);

            controller.FeedRedLights(holder.gameObject, new[] { g1, g2 }, new[] { g3, g4 },
                new[] { l1, l2 }, new[] { l3, l4 }, RedGlare.emissionMaterialLit, RedGlare.emissionMaterialUnlit);
        }

        private static void CreateLightSet(Transform holder, Vector3 glarePos, Vector3 lampPos, bool direction, out GameObject glare, out MeshRenderer lamp)
        {
            // Creates a copy of the DE2's frontal red light.
            // First the glare object, then the renderer with the lamp.
            glare = Object.Instantiate(RedGlare.glare, holder);
            glare.transform.localPosition = glarePos;
            glare.transform.localRotation = direction ? Quaternion.identity : Flipped;
            glare.transform.localScale = RedGlareScale;
            glare.AddComponent<SortingGroup>().sortingOrder = 10;

            lamp = Object.Instantiate(RedLamp, holder);
            lamp.transform.localPosition = lampPos;
            lamp.transform.localRotation = direction ? Quaternion.identity : Flipped;
            lamp.transform.localScale = RedMeshScale;
        }
    }
}
