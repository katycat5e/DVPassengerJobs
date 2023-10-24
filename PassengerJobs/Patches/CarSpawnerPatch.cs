using DV.ThingTypes;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CarSpawner))]
    internal class CarSpawnerPatch
    {
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

            car.gameObject.AddComponent<CoachLightController>();
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
    }
}
