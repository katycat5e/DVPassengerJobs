using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    public class PJModSettings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Use custom wage scaling for (new) passenger haul jobs")]
        public bool UseCustomWages = true;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save(this, modEntry);
        }

        public void OnChange() { }
    }
}
