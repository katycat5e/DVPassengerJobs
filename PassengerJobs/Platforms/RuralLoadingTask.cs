using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using System;
using System.Collections.Generic;

namespace PassengerJobs.Platforms
{
    public class RuralLoadingTask : Task
    {
        public const TaskType TaskType = (TaskType)42;
        public override TaskType InstanceTaskType => TaskType;

        public readonly List<Car> Cars;
        public readonly RuralLoadingMachine LoadingMachine;
        public readonly float CargoAmount;
        public readonly bool IsLoading;

        public bool readyForMachine;

        public RuralLoadingTask(List<Car> cars, RuralLoadingMachine loadingMachine, float cargoAmount, bool isLoading, bool isLastTask) :
            base(isLastTask: isLastTask)
        {
            Cars = cars;
            LoadingMachine = loadingMachine;
            CargoAmount = cargoAmount;
            IsLoading = isLoading;
        }

        public override float GetTaskPrice() => 0;

        public override TaskState UpdateTaskState()
        {
            readyForMachine = true;

            foreach (var car in Cars)
            {
                if (IsLoading)
                {
                    if (car.LoadedCargoAmount == 0)
                    {
                        SetState(TaskState.InProgress);
                        return state;
                    }
                }
                else
                {
                    if (car.LoadedCargoAmount > 0)
                    {
                        SetState(TaskState.InProgress);
                        return state;
                    }
                }
            }

            SetState(TaskState.Done);
            return state;
        }

        public override TaskData GetTaskData()
        {
            return new RuralTaskData(this);
        }

        public override void OverrideTaskState(TaskSaveData data)
        {
            base.OverrideTaskState(data);
            if (data.state == TaskState.Done)
            {
                LoadingMachine.RemoveTask(this);
            }
        }

        public override void SetJobBelonging(Job job)
        {
            base.SetJobBelonging(job);
            job.JobAbandoned += OnJobAbandoned;
            job.JobTaken += OnJobTaken;
        }

        private void OnJobTaken(Job takenJob, bool _)
        {
            takenJob.JobTaken -= OnJobTaken;
            LoadingMachine.AddTask(this);
        }

        private void OnJobAbandoned(Job abandonedJob)
        {
            abandonedJob.JobAbandoned -= OnJobAbandoned;
            LoadingMachine.RemoveTask(this);
        }
    }

    public class RuralTaskData : TaskData
    {
        public string stationId;
        public bool isLoading;

        public RuralTaskData(RuralLoadingTask task) : base(
            RuralLoadingTask.TaskType,
            task.state,
            task.taskStartTime,
            task.taskFinishTime,
            task.Cars,
            null,
            task.LoadingMachine.Track,    
            WarehouseTaskType.None,
            null,
            task.CargoAmount, 
            null,
            false,
            false)
        {
            stationId = task.LoadingMachine.Id;
            isLoading = task.IsLoading;
        }
    }

    public class RuralTask_data : Task_data
    {
        public string stationId;
        public bool isLoading;

        public RuralTask_data(RuralLoadingTask task) : base(task)
        {
            stationId = task.LoadingMachine.Id;
            isLoading = task.IsLoading;
        }
    }
}
