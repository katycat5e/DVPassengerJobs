using DV.ThingTypes;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Generation
{
    public static class ConsistManager
    {
        public static IEnumerable<TrainCarLivery> GetPassengerCars()
        {
            return CargoInjector.PassengerCargo.loadableCarTypes.SelectMany(info => info.carType.liveries);
        }
    }
}
