using DVCustomCarLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    internal static class CCL_Patch
    {
        public static bool Enabled { get; private set; }
        public static UnityModManager.ModEntry ModEntry { get; private set; } = null;

        public static void Initialize()
        {
            Enabled = false;

            ModEntry = UnityModManager.FindMod("DVCustomCarLoader");
            if (ModEntry == null)
            {
                PassengerJobs.Log("Custom Car Loader not found, skipping integration");
                return;
            }

            PassengerJobs.Log("Custom Car Loader integration enabled");
            Enabled = true;
        }

        public static bool TryGetCarTypeById(string id, out TrainCarType carType)
        {
            return CarTypeInjector.TryGetCarTypeById(id, out carType);
        }
    }
}
