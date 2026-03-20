using DV.Logic.Job;
using DV.ThingTypes;
using PassengerJobs.Injectors;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class RuralLoadingMachine : WarehouseMachine
    {
        public readonly int LowerBound;
        public readonly int UpperBound;

        public bool IsYardTrack { get; }

        public float? MarkerAngle;

        public List<RuralLoadingTask> Tasks => currentTasks.OfType<RuralLoadingTask>().ToList();

        public RuralLoadingMachine(string id, Track track, int lowerBound, int upperBound, float? markerAngle, bool isYardTrack)
            : base(track, new List<CargoType> { CargoInjector.PassengerCargo.v1 })
        {
            ID = id;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            IsYardTrack = isYardTrack;
            MarkerAngle = markerAngle;

        }

        public IEnumerable<PlatformTask> GetLoadableTasks(bool loading)
        {
            foreach (var task in Tasks.Where(t => t.IsLoading == loading))
            {
                if (AreCarsStoppedAtPlatform(task.Cars))
                {
                    yield return new RuralLoadTaskWrapper(task);
                }
            }
        }

        public bool AnyLoadableTrainPresent()
        {
            foreach (var task in Tasks)
            {
                if (task.readyForMachine && AreCarsStoppedAtPlatform(task.cars))
                {
                    return true;
                }
            }
            return false;
        }

        public bool AnyLoadableTrainPresent(bool loading)
        {
            foreach (var task in Tasks)
            {
                if (task.readyForMachine && (task.IsLoading == loading) && AreCarsStoppedAtPlatform(task.cars))
                {
                    return true;
                }
            }
            return false;
        }

        public bool AreCarsStoppedAtPlatform(IEnumerable<Car> cars)
        {
            return cars.All(IsCarStoppedOnTrack) && PlatformWrapperUtil.IsConsistAttachedToLoco(cars);
        }

        private bool IsBetween(int test)
        {
            return (test >= LowerBound) && (test <= UpperBound);
        }

        private bool IsCarStoppedOnTrack(Car car)
        {
            if (car.CurrentTrack != WarehouseTrack) return false;

            if (TrainCarRegistry.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar))
            {
                if (Mathf.Abs(trainCar.GetForwardSpeed()) > 0.3f) return false;
                
                return IsYardTrack ||
                    (IsBetween(trainCar.Bogies[1].point1.index) && IsBetween(trainCar.Bogies[0].point1.index));
            }

            // couldn't find physical car or car was moving
            return false;
        }
    }
}
