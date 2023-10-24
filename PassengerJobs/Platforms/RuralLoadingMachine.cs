using DV.Logic.Job;
using DV.PointSet;
using DV.ThingTypes;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class RuralLoadingMachine
    {
        private static readonly WarehouseMachine _dummyWarehouse = new(null, null);

        public readonly string Id;
        public readonly Track Track;
        public readonly int LowerBound;
        public readonly int UpperBound;

        public Vector3? PlatformOffset;
        public Vector3? PlatformRotation;
        public float? MarkerAngle;

        public readonly List<RuralLoadingTask> Tasks = new();

        public RuralLoadingMachine(StationConfig.RuralStation stationData, Track track)
        {
            Id = stationData.id;
            Track = track;
            LowerBound = stationData.lowIdx;
            UpperBound = stationData.highIdx;

            PlatformOffset = stationData.platformOffset;
            PlatformRotation = stationData.platformRotation;
            MarkerAngle = stationData.markerAngle;
        }

        public void AddTask(RuralLoadingTask task)
        {
            Tasks.Add(task);
        }

        public void RemoveTask(RuralLoadingTask task)
        {
            if (!Tasks.Remove(task))
            {
                PJMain.Error("Tried to remove task that wasn't assigned to this platform");
            }
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

        public bool AnyLoadableTrainPresent(bool loading)
        {
            foreach (var task in Tasks)
            {
                if (task.readyForMachine && (task.IsLoading == loading) && AreCarsStoppedAtPlatform(task.Cars))
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
            if (car.CurrentTrack != Track) return false;

            if (IdGenerator.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar))
            {
                return (Mathf.Abs(trainCar.GetForwardSpeed()) <= 0.3f) &&
                    IsBetween(trainCar.Bogies[1].point1.index) &&
                    IsBetween(trainCar.Bogies[0].point1.index);
            }

            // couldn't find physical car or car was moving
            return false;
        }

        public Car? TransferOneCarOfTask(RuralLoadingTask task, bool loading)
        {
            if (!Tasks.Contains(task))
            {
                PJMain.Warning($"task is not assigned to platform {Id}! Either loading was interrupted by game quit or something is bad!");
                return null;
            }

            float remainingTransfer = task.CargoAmount;

            Car? transferredCar;
            if (loading)
            {
                transferredCar = LoadOneCar(ref remainingTransfer, task.Cars);
            }
            else
            {
                transferredCar = UnloadOneCar(ref remainingTransfer, task.Cars);
            }

            if (remainingTransfer == 0)
            {
                RemoveTask(task);
            }

            return transferredCar;
        }

        private static Car? LoadOneCar(ref float amountToTransfer, IEnumerable<Car> cars)
        {
            foreach (var car in cars)
            {
                if ((car.CurrentCargoTypeInCar == CargoInjector.PassengerCargo.v1) && (car.LoadedCargoAmount > 0))
                {
                    amountToTransfer -= car.LoadedCargoAmount;
                    continue;
                }

                if ((car.CurrentCargoTypeInCar == CargoType.None) && (car.LoadedCargoAmount == 0))
                {
                    float toLoad = Mathf.Min(car.capacity, amountToTransfer);
                    car.LoadCargo(toLoad, CargoInjector.PassengerCargo.v1, _dummyWarehouse);
                    amountToTransfer -= toLoad;

                    PJMain.Log($"Loaded {toLoad} passengers to car {car.ID}");
                    return car;
                }
            }

            PJMain.Warning("Failed to load any car D:");
            return null;
        }

        private static Car? UnloadOneCar(ref float amountToTransfer, IEnumerable<Car> cars)
        {
            foreach (var car in cars)
            {
                if ((car.CurrentCargoTypeInCar == CargoType.None) && (car.LoadedCargoAmount == 0))
                {
                    amountToTransfer -= car.capacity;
                    continue;
                }

                if ((car.CurrentCargoTypeInCar == CargoInjector.PassengerCargo.v1) && (car.LoadedCargoAmount > 0))
                {
                    float toLoad = car.LoadedCargoAmount;
                    car.UnloadCargo(toLoad, CargoInjector.PassengerCargo.v1, _dummyWarehouse);
                    amountToTransfer -= toLoad;

                    PJMain.Log($"Unloaded {toLoad} passengers from car {car.ID}");
                    return car;
                }
            }

            PJMain.Warning("Failed to unload any car D:");
            return null;
        }
    }
}
