using CommsRadioAPI;
using DV;
using HarmonyLib;
using PassengerJobs.DebugTools.StationEditor;
using UnityEngine;

namespace PassengerJobs.DebugTools
{
    [HarmonyPatch(typeof(CommsRadioController))]
    public static class RadioSetup
    {
        public static CommsRadioMode StationPlacerMode;
        private static Color _stationPlacerLaserColor = new Color32(151, 121, 210, 255);
        public const string STATION_PLACER_TITLE = "PJ Station";

        //public static CommsRadioMode SpawnZoneMode;
        //private static Color _spawnZoneLaserColor = new Color32(0, 255, 128, 255);
        //public const string SPAWN_ZONE_TITLE = "PJ Peep Zone";

        private static void Initialize()
        {
            StationPlacerMode = CommsRadioMode.Create(new SelectStationState(), _stationPlacerLaserColor);
        }

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AfterRadioAwake()
        {
            Initialize();
        }


    }
}
