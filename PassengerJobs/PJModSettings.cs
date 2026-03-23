using System;
using System.Xml.Serialization;
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

        [Draw("Coach lights require loco power", Tooltip = "Main fuse on or dynamo running")]
        public bool CoachLightsRequirePower = true;

        [Draw("Allow CCL coaches to spawn alone if any part of the trainset is disabled", Tooltip = "In case a part of a trainset is disabled by CCL, \n" +
            "whether to allow spawning parts of it by themselves or require the whole trainset to be enabled")]
        public bool AllowCCLTrainsetAlone = false;
        [Draw("Prefer CCL in jobs", Tooltip = "Use CCL coaches instead of base game ones if available")]
        public bool PreferCCL = false;

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
