using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    public class PJModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Use custom wage scaling for (new) passenger haul jobs")]
        public bool UseCustomWages = true;

        [Draw("Generate passenger trains with uniform car type")]
        public bool UniformConsists = true;

        [Draw("Special/named train generation probability", Min = 0, Max = 1)]
        public float NamedTrainProbability = 0.6f;

        [Draw("Perform data purge for uninstall (see log for results)")]
        public bool DoPurge = false;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            if( DoPurge )
            {
                DoPurge = false;
                PurgeData();
            }
        }

        public void PurgeData()
        {
            PassengerLicenseUtil.RefundLicenses();
            PassengerLicenseUtil.DestroySpawnedLicenses();
            PassengerJobGenerator.PurgePassengerJobChains();
        }
    }
}
