using DV.ThingTypes;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;

namespace PassengerJobs
{
    internal class CCLIntegration
    {
        private static bool _loaded = false;
        private static UnityModManager.ModEntry? s_mod;
        private static MethodInfo? s_getTrainset;
        private static MethodInfo? s_trainsetEnabled;
        private static MethodInfo? s_liveryEnabled;
        private static MethodInfo? s_getExtendedPaymentData;

        public static bool Loaded
        {
            get
            {
                if (_loaded && s_mod != null) return s_mod.Active;
                TryLoad();
                return _loaded && s_mod != null && s_mod.Active;
            }
        }

        private static void TryLoad()
        {
            if (_loaded && s_mod != null) return;

            _loaded = false;
            s_mod = UnityModManager.FindMod("DVCustomCarLoader");

            if (s_mod == null || s_mod.Assembly == null) return;

            var manager = s_mod.Assembly.GetType("CCL.Importer.CarManager");

            if (manager == null) return;
            
            s_getTrainset = manager.GetMethod("GetTrainsetForLivery");
            s_trainsetEnabled = manager.GetMethod("IsTrainsetEnabled");
            s_liveryEnabled = manager.GetMethod("IsCarLiveryEnabled");

            var extendedPaymentData = s_mod.Assembly.GetType("CCL.Importer.ExtendedPaymentData");

            if (extendedPaymentData == null) return;

            s_getExtendedPaymentData = extendedPaymentData.GetMethod("ForSingleCargoType");

            _loaded = true;
        }

        private static TrainCarLivery[] GetTrainset(TrainCarLivery livery)
        {
            if (s_getTrainset == null)
            {
                return Array.Empty<TrainCarLivery>();
            }

            return (TrainCarLivery[])s_getTrainset.Invoke(null, new object[] { livery });
        }

        public static bool TryGetTrainset(TrainCarLivery livery, out TrainCarLivery[] result)
        {
            if (!Loaded)
            {
                result = Array.Empty<TrainCarLivery>();
                return false;
            }

            result = GetTrainset(livery);
            return result.Length > 0;
        }

        public static bool IsTrainsetEnabled(TrainCarLivery[] trainset)
        {
            if (!Loaded || s_trainsetEnabled == null) return true;

            return (bool)s_trainsetEnabled.Invoke(null, new object[] { trainset, true });
        }

        public static bool IsLiveryEnabled(TrainCarLivery livery, RouteType route)
        {
            if (!Loaded || s_liveryEnabled == null) return true;

            var enabled = (bool)s_liveryEnabled.Invoke(null, new object[] { livery });

            var t = livery.GetType();
            var f = t.GetField(route == RouteType.Express ? "AllowOnExpressRoutes" : "AllowOnRegionalRoutes");

            if (f != null) return enabled && (bool)f.GetValue(livery);

            return enabled;
        }

        public static bool IsCCLPrefered()
        {
            return PJMain.Settings.PreferCCL;
        }

        public static int GetMaxRepeatedSpawn(TrainCarLivery livery)
        {
            if (!Loaded) return int.MaxValue;

            var t = livery.GetType();
            var f = t.GetField("MaxRepeatedSpawn");

            if (f == null) return int.MaxValue;

            var value = (int)f.GetValue(livery);

            return value > 0 ? value : int.MaxValue;
        }

        public static PaymentCalculationData GetModifiedData(PaymentCalculationData original, List<TrainCarLivery> jobCarTypes)
        {
            if (!Loaded || s_getExtendedPaymentData == null) return original;

            return (PaymentCalculationData)s_getExtendedPaymentData.Invoke(null, new object[] { original, jobCarTypes, CargoInjector.PassengerCargo.v1 });
        }
    }
}
