using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    public static class PassengerJobs
    {
        internal static UnityModManager.ModEntry ModEntry;

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            var harmony = Harmony12.HarmonyInstance.Create("com.foxden.passenger_jobs");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            return true;
        }

        //private static bool firstLoaded = true;
        //private static void OnSceneLoaded( Scene arg0, LoadSceneMode arg1 )
        //{
        //    if( firstLoaded )
        //    {
        //        firstLoaded = false;

        //        var stations = UnityEngine.Object.FindObjectsOfType<StationController>();
        //        foreach( var station in stations )
        //        {
        //            var generator = station.gameObject.AddComponent<PassengerJobGenerator>();
        //            generator.Initialize();
        //        }
        //    }
        //}
    }
}
