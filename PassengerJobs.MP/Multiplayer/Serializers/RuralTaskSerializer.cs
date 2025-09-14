using DV.Logic.Job;
using MPAPI.Types;
using MPAPI.Util;
using MPAPI;
using PassengerJobs.Platforms;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace PassengerJobs.MP.Multiplayer.Serializers;

public class RuralLoadingTaskData : TaskNetworkData<RuralLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public string RuralLoadingMachineId { get; set; } = string.Empty;
    public float CargoAmount { get; set; }
    public bool IsLoading { get; set; }
    public bool ReadyForMachine { get; set; }

    public override void Serialize(BinaryWriter writer)
    {
        SerializeCommon(writer);

        writer.WriteUShortArray(CarNetIDs);
        writer.Write(RuralLoadingMachineId ?? string.Empty);
        writer.Write(CargoAmount);
        writer.Write(IsLoading);
        writer.Write(ReadyForMachine);
    }

    public override void Deserialize(BinaryReader reader)
    {
        DeserializeCommon(reader);
        
        CarNetIDs = reader.ReadUShortArray();
        RuralLoadingMachineId = reader.ReadString();
        CargoAmount = reader.ReadSingle();
        IsLoading = reader.ReadBoolean();
        ReadyForMachine = reader.ReadBoolean();
    }

    public override RuralLoadingTaskData FromTask(Task task)
    {
        if (task is not RuralLoadingTask ruralLoadingTask)
            throw new ArgumentException("Task is not a RuralLoadingTask");

        FromTaskCommon(task);

        CarNetIDs = ruralLoadingTask.Cars.Select(
            car =>
                {
                    var trainCar = MultiplayerManager.GetTrainCarFromID(car.ID);
                    if (trainCar == null || !MultiplayerAPI.Instance.TryGetNetId(trainCar, out var netId))
                        return (ushort)0;

                    return netId;
                }
            )
            .ToArray();

        RuralLoadingMachineId = ruralLoadingTask.LoadingMachine.Id;
        CargoAmount = ruralLoadingTask.CargoAmount;
        IsLoading = ruralLoadingTask.IsLoading;
        ReadyForMachine = ruralLoadingTask.readyForMachine;

        return this;
    }

    public override Task ToTask(ref Dictionary<ushort, Task> netIdToTask)
    {
        List<Car> cars = CarNetIDs
            .Select(netId => MultiplayerAPI.Instance.TryGetObjectFromNetId(netId, out TrainCar trainCar) ? trainCar : null)
            .Where(car => car != null)
            .Select(car => car!.logicCar)
            .ToList();


        if (!RuralLoadingMachine.TryGetById(RuralLoadingMachineId, out var ruralLoadingMachine))
            throw new ArgumentException($"Invalid RuralLoadingMachineId: {RuralLoadingMachineId}");

        RuralLoadingTask newRuralLoadingTask = new
            (
               cars,
               ruralLoadingMachine!,
               CargoAmount,
               IsLoading,
               IsLastTask
            );

        ToTaskCommon(newRuralLoadingTask);

        newRuralLoadingTask.readyForMachine = ReadyForMachine;

        netIdToTask.Add(TaskNetId, newRuralLoadingTask);

        return newRuralLoadingTask;
    }

    public override List<ushort> GetCars()
    {
        return CarNetIDs.ToList();
    }
}