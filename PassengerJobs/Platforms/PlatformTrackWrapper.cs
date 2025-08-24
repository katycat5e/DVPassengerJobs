using DV.Logic.Job;
using DV.PointSet;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public interface IPlatformWrapper
    {
        string Id { get; }
        string DisplayId { get; }
        string TrackId { get; }

        Task GenerateBoardingTask(List<Car> cars, bool loading, float totalCapacity, bool isFinal);
        List<PlatformTask> GetLoadableTasks(bool loading);
        void RemoveTask(PlatformTask task);

        Car? TransferOneCarOfTask(PlatformTask task, bool loading);
        bool IsAnyTrainPresent();
        bool IsAnyTrainPresent(bool loading);
        bool AreCarsStoppedAtPlatform(List<Car> cars);

    }

    public static class PlatformWrapperUtil
    {
        public static bool IsConsistAttachedToLoco(IEnumerable<Car> cars)
        {
            if (TrainCarRegistry.Instance.logicCarToTrainCar.TryGetValue(cars.First(), out var trainCar))
            {
                return trainCar.trainset.locoIndices.Any();
            }
            PJMain.Error($"Couldn't get physical car for logic car {cars.First().ID}");
            return false;
        }
    }

    public sealed class StationPlatformWrapper : IPlatformWrapper
    {
        public readonly Track Track;
        public readonly WarehouseMachine Warehouse;

        public StationPlatformWrapper(Track track)
        {
            Track = track;
            Warehouse = new WarehouseMachine(Track, new() { CargoInjector.PassengerCargo.v1 });
        }

        public string Id => Track.ID.ToString();
        public string DisplayId => Track.ID.TrackPartOnly;
        public string TrackId => Track.ID.ToString();

        public Task GenerateBoardingTask(List<Car> cars, bool loading, float totalCapacity, bool isFinal)
        {
            WarehouseTaskType taskType = loading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;
            return new WarehouseTask(cars, taskType, Warehouse, CargoInjector.PassengerCargo.v1, totalCapacity, isLastTask: isFinal);
        }

        public List<PlatformTask> GetLoadableTasks(bool loading)
        {
            var taskType = loading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;
            var loadData = Warehouse.GetCurrentLoadUnloadData(taskType);

            var result = new List<PlatformTask>();
            foreach (var item in loadData)
            {
                if (item.state == WarehouseMachine.WarehouseLoadUnloadDataPerJob.State.FullLoadUnloadPossible)
                {
                    foreach (var task in item.tasksAvailableToProcess)
                    {
                        if (AreCarsStoppedAtPlatform(task.cars))
                        {
                            result.Add(new CityLoadTaskWrapper((CityLoadingTask)task));
                        }
                    }
                }
            }

            return result;
        }

        public void RemoveTask(PlatformTask task)
        {
            if (task is CityLoadTaskWrapper wrapper)
            {
                Warehouse.RemoveWarehouseTask(wrapper.TypedTask);
            }
            else
            {
                PJMain.Warning("Tried to remove wrong type of task from warehouse");
            }
        }

        public Car? TransferOneCarOfTask(PlatformTask task, bool loading)
        {
            if (task is CityLoadTaskWrapper wrapper)
            {
                return loading ?
                    Warehouse.LoadOneCarOfTask(wrapper.TypedTask) :
                    Warehouse.UnloadOneCarOfTask(wrapper.TypedTask);
            }
            else
            {
                PJMain.Warning("Tried to transfer wrong type of task via warehouse");
                return null;
            }
        }

        public bool IsAnyTrainPresent()
        {
            foreach (WarehouseTask warehouseTask in Warehouse.currentTasks)
            {
                if (warehouseTask.readyForMachine && AreCarsStoppedAtPlatform(warehouseTask.cars))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsAnyTrainPresent(bool loading)
        {
            WarehouseTaskType taskType = loading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;

            foreach (WarehouseTask warehouseTask in Warehouse.currentTasks)
            {
                if (warehouseTask.readyForMachine && (warehouseTask.warehouseTaskType == taskType) && AreCarsStoppedAtPlatform(warehouseTask.cars))
                {
                    return true;
                }
            }
            return false;
        }

        public bool AreCarsStoppedAtPlatform(List<Car> cars)
        {
            // do it in two stages to exit early before speed lookup
            if (!Warehouse.CarsPresentOnWarehouseTrack(cars))
            {
                return false;
            }

            if (!PlatformWrapperUtil.IsConsistAttachedToLoco(cars))
            {
                return false;
            }

            foreach (var car in cars)
            {
                if (!TrainCarRegistry.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) ||
                    Mathf.Abs(trainCar.GetForwardSpeed()) > 0.05f)
                {
                    // couldn't find physical car or car was moving
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class RuralPlatformWrapper : IPlatformWrapper
    {
        public readonly RuralLoadingMachine LoadingMachine;

        public RuralPlatformWrapper(RuralLoadingMachine machine)
        {
            LoadingMachine = machine;
        }

        public string Id => LoadingMachine.Id;
        public string DisplayId => LoadingMachine.Id;
        public string TrackId => LoadingMachine.IsYardTrack ? LoadingMachine.Track.ID.ToString() : LoadingMachine.Id;

        public Task GenerateBoardingTask(List<Car> cars, bool loading, float totalCapacity, bool isFinal)
        {
            return new RuralLoadingTask(cars, LoadingMachine, totalCapacity, loading, isFinal);
        }

        public List<PlatformTask> GetLoadableTasks(bool loading)
        {
            return LoadingMachine.GetLoadableTasks(loading).ToList();
        }

        public void RemoveTask(PlatformTask task)
        {
            if (task is RuralLoadTaskWrapper ruralTask)
            {
                LoadingMachine.RemoveTask(ruralTask.TypedTask);
            }
            else
            {
                PJMain.Warning("Tried to remove wrong type of task from platform");
            }
        }

        public Car? TransferOneCarOfTask(PlatformTask task, bool loading)
        {
            if (task is RuralLoadTaskWrapper ruralTask)
            {
                return LoadingMachine.TransferOneCarOfTask(ruralTask.TypedTask, loading);
            }
            else
            {
                PJMain.Warning("Tried to transfer wrong type of task for platform");
                return null;
            }
        }

        public bool AreCarsStoppedAtPlatform(List<Car> cars)
        {
            return LoadingMachine.AreCarsStoppedAtPlatform(cars);
        }

        public bool IsAnyTrainPresent()
        {
            return LoadingMachine.AnyLoadableTrainPresent();
        }

        public bool IsAnyTrainPresent(bool loading)
        {
            return LoadingMachine.AnyLoadableTrainPresent(loading);
        }
    }
}
