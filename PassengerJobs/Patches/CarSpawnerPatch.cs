using DV.ThingTypes;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CarSpawner))]
    internal class CarSpawnerPatch
    {
        private static readonly Quaternion LightRotation = Quaternion.Euler(90, 0, 0);
        private static readonly Quaternion LightRotationFlip = Quaternion.Euler(90, 0, 0);
        private static readonly Quaternion DirLightRotationL = Quaternion.Euler(45, -90, 0);
        private static readonly Quaternion DirLightRotationR = Quaternion.Euler(45, 90, 0);

        #region Red Lights

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

        #endregion

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

            var lightHolder = new GameObject("[PJ Lights]").transform;
            lightHolder.SetParent(car.transform, false);

            switch (PJMain.Settings.CoachLights)
            {
                case PJModSettings.CoachLightMode.Improved:
                    ImprovedLightPositions(lightHolder);
                    break;
                case PJModSettings.CoachLightMode.Old:
                    OldLightPositions(lightHolder);
                    break;
                default:
                    break;
            }

            var controller = car.gameObject.AddComponent<CoachLightController>();

            AddRedLights(car.transform, controller);
        }

        private static void ImprovedLightPositions(Transform parent)
        {
            parent.localPosition = new Vector3(0, 3.75f, 0);

            AddLightAtOffset(parent, new Vector3(0, 0, 10.6f));
            AddLightAtOffset(parent, new Vector3(0, 0, 8));
            AddLightAtOffset(parent, new Vector3(0, 0, 6));
            AddLightAtOffset(parent, new Vector3(0, 0, 4));
            AddLightAtOffset(parent, new Vector3(0, 0, 2));
            AddLightAtOffset(parent, new Vector3(0, 0, 0));
            AddLightAtOffset(parent, new Vector3(0, 0, -2));
            AddLightAtOffset(parent, new Vector3(0, 0, -4));
            AddLightAtOffset(parent, new Vector3(0, 0, -6));
            AddLightAtOffset(parent, new Vector3(0, 0, -8));
            AddLightAtOffset(parent, new Vector3(0, 0, -10.6f));
            //AddDirectionalLight(lightHolder.transform, new Vector3(-1.5f, -1.5f, 0), DirLightRotationL);
            //AddDirectionalLight(lightHolder.transform, new Vector3(1.5f, -1.5f, 0), DirLightRotationR);
        }

        private static void OldLightPositions(Transform parent)
        {
            parent.localPosition = new Vector3(0, 3.8f, 0);

            AddLightAtOffsetOld(parent, new Vector3(0, 0, 6));
            AddLightAtOffsetOld(parent, new Vector3(0, 0, 0));
            AddLightAtOffsetOld(parent, new Vector3(0, 0, -6));
        }

        private static void AddLightAtOffset(Transform parent, Vector3 offset)
        {
            var holder = new GameObject("[PJ light source]");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = offset;
            holder.transform.localRotation = LightRotation;

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.spotAngle = 125.0f;
            light.color = LampHelper.LitColour;
            light.intensity = 2.1f;
            light.range = 2.8f;

            //light.shadows = LightShadows.Soft;
            //light.shadowResolution = LightShadowResolution.Low;
        }

        private static void AddLightAtOffsetOld(Transform parent, Vector3 offset)
        {
            var holder = new GameObject("[PJ light source]");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = offset;

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LampHelper.LitColour;
            light.intensity = 3.0f;
            light.range = 6.0f;

            //light.shadows = LightShadows.Soft;
            //light.shadowResolution = LightShadowResolution.Low;
        }

        //private static void AddDirectionalLight(Transform parent, Vector3 offset, Quaternion rotation)
        //{
        //    var holder = new GameObject("[PJ directional light source]");
        //    holder.transform.SetParent(parent, false);
        //    holder.transform.localPosition = offset;
        //    holder.transform.localRotation = rotation;

        //    var light = holder.AddComponent<Light>();
        //    light.type = LightType.Directional;
        //    light.color = LampHelper.LitColour;
        //    light.intensity = 0.1f;
        //    light.cookie = BundleLoader.CoachCookie;
        //    light.cookieSize = 24.0f;
        //}

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
                new[] { l1, l2 }, new[] { l3, l4 });
        }

        private static void CreateLightSet(Transform holder, Vector3 glarePos, Vector3 lampPos, bool direction, out GameObject glare, out MeshRenderer lamp)
        {
            // Creates a copy of the DE2's frontal red light.
            // First the glare object, then the renderer with the lamp.
            glare = Object.Instantiate(LampHelper.RedGlare.glare, holder);
            glare.transform.localPosition = glarePos;
            glare.transform.localRotation = direction ? Quaternion.identity : Flipped;
            glare.transform.localScale = RedGlareScale;
            glare.AddComponent<SortingGroup>().sortingOrder = 10;

            lamp = Object.Instantiate(LampHelper.RedLamp, holder);
            lamp.transform.localPosition = lampPos;
            lamp.transform.localRotation = direction ? Quaternion.identity : Flipped;
            lamp.transform.localScale = RedMeshScale;
        }
    }
}
