using PassengerJobs.Generation;
using System;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;

namespace PassengerJobs
{
    public class PJModSettings : UnityModManager.ModSettings, IDrawable
    {
        public enum CoachLightMode
        {
            NoLights,
            Improved,
            Old
        }

        [Draw("Use custom wage scaling for (new) passenger haul jobs")]
        public bool UseCustomWages = true;
        [Draw("Change the look of passenger coach interior lights", Tooltip = "Requires reloading the session to change the layout")]
        public CoachLightMode CoachLights = CoachLightMode.Improved;
        [Draw("Use custom coach light colour", InvisibleOn = "CoachLights|0")]
        public bool UseCustomCoachLightColour = false;
        [Draw("Light colour", VisibleOn = "UseCustomCoachLightColour|True")]
        public Color CustomCoachLightColour = Color.white;

#if DEBUG
        [Draw("Reload rural stations config")]
        public bool ReloadStations = false;
#endif

        [XmlIgnore]
        public Action<PJModSettings>? OnSettingsSaved;

        public bool DisableCoachLights => CoachLights == CoachLightMode.NoLights;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
            OnSettingsSaved?.Invoke(this);
        }

        public void OnChange()
        {
#if DEBUG
            if (ReloadStations)
            {
                RouteManager.ReloadStations();
                ReloadStations = false;
            }
#endif
        }
    }
}
