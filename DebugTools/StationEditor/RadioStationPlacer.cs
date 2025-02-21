using CommsRadioAPI;
using DV;
using HarmonyLib;
using UnityEngine;

namespace PassengerJobs.DebugTools.StationEditor
{
    [HarmonyPatch(typeof(CommsRadioController))]
    public static class RadioStationPlacer
    {
        public static CommsRadioMode Instance;
        private static Color _laserColor = new Color32(151, 121, 210, 255);

        private static void Initialize()
        {
            Instance = CommsRadioMode.Create(new SelectStationState(), _laserColor);
        }

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AfterRadioAwake()
        {
            Initialize();
        }


    }
}
