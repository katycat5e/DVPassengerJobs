using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PassengerJobsMod
{
    //[HarmonyPatch(typeof(StationController), "Update")]
    //class StationController_Update_Patch
    //{
    //    static void Postfix( StationController __instance )
    //    {
    //        if( PassengerJobGenerator.LinkedGenerators.TryGetValue(__instance, out var generator) )
    //        {
    //            generator.OnStationUpdate();
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(StationProceduralJobsController), "Awake")]
    class StationController_Start_Patch
    {
        private static HashSet<string> ManagedYards = new HashSet<string>() { "CSW", "MF", "FF", "HB", "GF" };

        static void Postfix( StationProceduralJobsController __instance )
        {
            string yardId = __instance.stationController.stationInfo.YardID;
            if( !ManagedYards.Contains(yardId) ) return;

            var gen = __instance.GetComponent<PassengerJobGenerator>();
            if( gen == null )
            {
                gen = __instance.gameObject.AddComponent<PassengerJobGenerator>();
                gen.Initialize();
            }
        }
    }

    //[HarmonyPatch(typeof(JobValidator), "ProcessJobOverview")]
    //class Validator_Process_Patch
    //{
    //    static void Prefix( JobOverview jobOverview, List<StationController> ___allStations )
    //    {
    //        //var allStationsField = typeof(JobValidator).GetField("allStations", BindingFlags.NonPublic | BindingFlags.Static);
    //        //var stations = allStationsField.GetValue(null) as List<StationController>;

    //        var sb = new StringBuilder();
    //        foreach( var st in ___allStations )
    //        {
    //            sb.Clear();
    //            sb.AppendLine($"Station {st.stationInfo.Name}:");
    //            sb.AppendLine($"\tLogicstation: {st.logicStation}");
    //            sb.Append($"\t\tavailableJobs: {st.logicStation?.availableJobs}");
    //            PassengerJobs.ModEntry.Logger.Log(sb.ToString());
    //        }
    //    }
    //}
}
