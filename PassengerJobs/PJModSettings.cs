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

        [Draw("Use custom wage scaling for (new) passenger haul jobs", InvisibleOn = "MPActive|true")]
        public bool UseCustomWages = true;
        [Draw("Change the look of passenger coach interior lights", Tooltip = "Requires reloading the session to change the layout", InvisibleOn = "MPActive|true")]
        public CoachLightMode CoachLights = CoachLightMode.Improved;
        [Draw("Use custom coach light colour", VisibleOn = "MPActive|false", InvisibleOn = "CoachLights|0")]
        public bool UseCustomCoachLightColour = false;
        [Draw("Light colour", VisibleOn = "UseCustomCoachLightColour|True", InvisibleOn = "MPActive|true")]
        public Color CustomCoachLightColour = Color.white;

        [Draw("Coach lights require loco power", Tooltip = "Main fuse on or dynamo running", InvisibleOn = "MPActive|true")]
        public bool CoachLightsRequirePower = true;

#if DEBUG
        [Draw("Reload rural stations config")]
        public bool ReloadStations = false;

        [Draw("MP Active")]
#endif
        [XmlIgnore]
        public bool MPActive = false;

        [XmlIgnore]
        public Action<PJModSettings>? OnSettingsSaved;

        [XmlIgnore]
        public Action? OnSettingsChanged;

        public bool DisableCoachLights => CoachLights == CoachLightMode.NoLights;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            if (!MPActive)
            {
                Save(this, modEntry);
                OnSettingsSaved?.Invoke(this);
            }
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
            OnSettingsChanged?.Invoke();
        }
    }
}
