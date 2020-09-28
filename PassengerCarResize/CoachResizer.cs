using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony12;
using UnityEngine;
using UnityModManagerNet;

namespace CoachResizeMod
{
    public static class CoachResizer
    {
        internal static UnityModManager.ModEntry ModEntry;
        private static readonly CRModSettings Settings = new CRModSettings();

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            ModEntry.OnGUI = Settings.Draw;

            var harmony = HarmonyInstance.Create("cc.foxden.coach_resizer");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            return true;
        }

        public const float SizeMultiplier = 0.8f;

        public static bool IsPassengerCar( TrainCar car )
        {
            return car.carType == TrainCarType.PassengerBlue || car.carType == TrainCarType.PassengerGreen || car.carType == TrainCarType.PassengerRed;
        }

        public static void AdjustTransforms( TrainCar car )
        {
            car.transform.localScale = new Vector3(1, 1, SizeMultiplier);
            //ModEntry.Logger.Log($"start AdjustTransforms: {car.gameObject.DumpHierarchy()}");

            // DFS
            var stack = new List<Transform>();
            stack.AddRange(car.transform.OfType<Transform>());
            while( stack.Count > 0 )
            {
                var transform = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                switch( transform.name )
                {
                    case "car_passenger_lod":
                    case "[colliders]":
                        continue;
                }
                if( transform.localPosition == Vector3.zero )
                {
                    ModEntry.Logger.Log($"treating {transform.GetPath()} as parent: sqrMagnitude = {transform.localPosition.sqrMagnitude}");
                    stack.AddRange(transform.OfType<Transform>());
                }
                else
                {
                    ModEntry.Logger.Log($"Adjusting transform {transform.GetPath()}: sqrMagnitude = {transform.localPosition.sqrMagnitude}");
                    transform.localScale = new Vector3(1, 1, 1f / SizeMultiplier);
                }
            }
        }

        private static void GetPathRec( Transform transform, StringBuilder sb )
        {
            if( transform.parent != null ) GetPathRec(transform.parent, sb);

            sb.Append($"/{transform.name}");
        }

        private static string GetPath( this Transform transform )
        {
            var sb = new StringBuilder();
            GetPathRec(transform, sb);
            return sb.ToString();
        }
    }
}
