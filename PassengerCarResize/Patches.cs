using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony12;
using UnityEngine;

namespace CoachResizeMod
{
    [HarmonyPatch(typeof(TrainCar), "Awake")]
    [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.AwakeForPooledCar))]
    public static class AwakePatch
    {
        public static void Prefix( TrainCar __instance )
        {
            if( CoachResizer.IsPassengerCar(__instance) )
                CoachResizer.AdjustTransforms(__instance);
        }
    }

    [HarmonyPatch(typeof(CarTypes), nameof(CarTypes.GetCarPrefab))]
    public static class GetCarPrefabPatch
    {
        public static void Postfix( ref GameObject __result )
        {
            if( __result == null )
                return;
            var car = __result.GetComponent<TrainCar>();
            if( CoachResizer.IsPassengerCar(car) )
            {
                CoachResizer.AdjustTransforms(car);
                //PassengerCarResizer.ModEntry.Logger.Log($"From GetCarPrefab: {__result.DumpHierarchy()}");
            }
        }
    }

    [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.Bounds), MethodType.Getter)]
    public static class BoundsPatch
    {
        public static bool Prefix( TrainCar __instance, ref Bounds __result, ref Bounds ____bounds )
        {
            if( ____bounds.size.z == 0.0f || !Application.isPlaying )
            {
                ____bounds = TrainCarColliders.GetCollisionBounds(__instance);
                Debug.Log($"Computed collision bounds for {__instance.carType}: {____bounds}");
                ____bounds.Encapsulate(Vector3.Scale(__instance.FrontCouplerAnchor.localPosition, __instance.FrontCouplerAnchor.parent.lossyScale));
                ____bounds.Encapsulate(Vector3.Scale(__instance.RearCouplerAnchor.localPosition, __instance.RearCouplerAnchor.parent.lossyScale));
                Debug.Log($"Computed bounds for {__instance.carType}: {____bounds}");
            }
            __result = ____bounds;
            return false;
        }
    }
}
