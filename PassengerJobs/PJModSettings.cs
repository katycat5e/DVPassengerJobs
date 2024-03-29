﻿using PassengerJobs.Generation;
using UnityModManagerNet;

namespace PassengerJobs
{
    public class PJModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Use custom wage scaling for (new) passenger haul jobs")]
        public bool UseCustomWages = true;

        [Draw("Disable passenger coach interior lights")]
        public bool DisableCoachLights = false;

#if DEBUG
        [Draw("Reload rural stations config")]
        public bool ReloadStations = false;
#endif

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save(this, modEntry);
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
