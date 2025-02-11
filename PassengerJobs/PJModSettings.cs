using PassengerJobs.Generation;
using System;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;

namespace PassengerJobs
{
    public class PJModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Use custom wage scaling for (new) passenger haul jobs")]
        public bool UseCustomWages = true;
        [Draw("Disable passenger coach interior lights")]
        public bool DisableCoachLights = false;
        [Draw("Use custom coach light colour", VisibleOn = "DisableCoachLights|False")]
        public bool UseCustomCoachLightColour = false;
        [Draw("Light colour", VisibleOn = "UseCustomCoachLightColour|True")]
        public Color CustomCoachLightColour = Color.white;

#if DEBUG
        [Draw("Reload rural stations config")]
        public bool ReloadStations = false;
#endif

        [XmlIgnore]
        public Action<PJModSettings>? OnSettingsSaved;

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
