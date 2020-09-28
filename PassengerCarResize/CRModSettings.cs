using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;

namespace CoachResizeMod
{
    class CRModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Delete All Passenger Cars")]
        public bool DeleteCars = false;

        public void OnChange()
        {
            if( DeleteCars )
            {
                DeleteCars = false;
                DeleteAllCoaches();
            }
        }

        static void DeleteAllCoaches()
        {
            var allCars = UnityEngine.Object.FindObjectsOfType<TrainCar>();
            var toDelete = allCars.Where(CoachResizer.IsPassengerCar).ToList();
            SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(toDelete);
        }
    }
}
