using DV.Logic.Job;
using DV.ThingTypes;
using MPAPI;
using MPAPI.Types;
using MPAPI.Util;
using PassengerJobs.MP.Multiplayer;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PassengerJobs.Multiplayer.Serializers;

public class CityLoadingTaskData : TaskNetworkData<CityLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public WarehouseTaskType WarehouseTaskType { get; set; }
    public WarehouseMachine? WarehouseMachine { get; set; }
    public CargoType CargoType { get; set; }
    public float CargoAmount { get; set; }
    public bool ReadyForMachine { get; set; }

    public override void Serialize(BinaryWriter writer)
    {
        //PJMain.Log($"Serializing CityLoadingTaskData.\r\n\tCarNetIds: [{string.Join(", ", CarNetIDs)}]\r\n\tWarehouseTaskType: {WarehouseTaskType}\r\n\tWarehouseMachineTrack: {WarehouseMachine?.WarehouseTrack?.ID}\r\n\tCargoType: {CargoType}\r\n\tCargoAmount: {CargoAmount}\r\n\tReadyForMachine: {ReadyForMachine}");

        SerializeCommon(writer);

        writer.WriteUShortArray(CarNetIDs);
        writer.Write((byte)WarehouseTaskType);

        var track = WarehouseMachine?.WarehouseTrack?.RailTrack() ?? null;
        MultiplayerAPI.Instance.TryGetNetId(track, out ushort trackNetId);

        writer.Write(trackNetId);
        writer.Write((int)CargoType);
        writer.Write(CargoAmount);
        writer.Write(ReadyForMachine);
    }

    public override void Deserialize(BinaryReader reader)
    {
        DeserializeCommon(reader);

        CarNetIDs = reader.ReadUShortArray();
        WarehouseTaskType = (WarehouseTaskType)reader.ReadByte();

        ushort trackNetId = reader.ReadUInt16();
        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId(trackNetId, out RailTrack? WarehouseMachineTrack) || !WarehouseMachineTrack)
            throw new Exception($"Failed to deserialise CityLoadingTaskData for trackNetId {trackNetId}, RailTrack was not found");

        var track = WarehouseMachineTrack.LogicTrack()?.ID?.FullDisplayID;
        if (string.IsNullOrEmpty(track))
            throw new Exception($"Failed to deserialise CityLoadingTaskData for trackNetId {trackNetId}, LogicTrack was not found");

        if (!PlatformController.TryGetControllerForTrack(track, out var controller) || !controller)
            throw new Exception($"Failed to deserialise CityLoadingTaskData for trackNetId {trackNetId}, PlatformController was not found");

        if (controller!.Platform is not StationPlatformWrapper stationPlatform)
            throw new Exception($"Failed to deserialise CityLoadingTaskData for trackNetId {trackNetId}, Platform is not a StationPlatform");

        WarehouseMachine = stationPlatform.Warehouse;
        
        CargoType = (CargoType)reader.ReadInt32();
        CargoAmount = reader.ReadSingle();
        ReadyForMachine = reader.ReadBoolean();

        //PJMain.Log($"Deserializing CityLoadingTaskData.\r\n\tCarNetIds: [{string.Join(", ", CarNetIDs)}]\r\n\tWarehouseTaskType: {WarehouseTaskType}\r\n\tWarehouseMachineTrack: {WarehouseMachineTrack?.name}\r\n\tCargoType: {CargoType}\r\n\tCargoAmount: {CargoAmount}\r\n\tReadyForMachine: {ReadyForMachine}");
    }

    public override CityLoadingTaskData FromTask(Task task)
    {
        //PJMain.Log($"CityLoadingTaskData.FromTask({task.Job.ID})");

        if (task is not CityLoadingTask cityLoadingTask)
            throw new ArgumentException("Task is not a CityLoadingTask");

        CarNetIDs = cityLoadingTask.Cars.Select(
            car =>
                {
                    var trainCar = MultiplayerManager.GetTrainCarFromID(car.ID);
                    if (trainCar == null || !MultiplayerAPI.Instance.TryGetNetId(trainCar, out var netId))
                        return (ushort)0;

                    return netId;
                }
            )
            .ToArray();

        //PJMain.Log($"CityLoadingTaskData.FromTask({task.Job.ID}) WarehouseMachine.WarehouseTrack: {cityLoadingTask.warehouseMachine.WarehouseTrack.ID}, WarehouseMachine.ID: {cityLoadingTask.warehouseMachine.ID}");

        WarehouseTaskType = cityLoadingTask.warehouseTaskType;
        WarehouseMachine = cityLoadingTask.warehouseMachine;
        CargoType = cityLoadingTask.cargoType;
        CargoAmount = cityLoadingTask.cargoAmount;
        ReadyForMachine = cityLoadingTask.readyForMachine;

        return this;
    }

    public override Task ToTask()
    {
        List<Car> cars = CarNetIDs
            .Select(netId => MultiplayerAPI.Instance.TryGetObjectFromNetId(netId, out TrainCar trainCar) ? trainCar : null)
            .Where(car => car != null)
            .Select(car => car!.logicCar)
            .ToList();

        if (WarehouseMachine == null)
            throw new Exception($"Failed to convert CityLoadingTaskData to Task, WarehouseMachine is null!");

        CityLoadingTask newTask = new
        (
           cars,
           WarehouseTaskType,
           WarehouseMachine,
           CargoType,
           CargoAmount
        )
        {
            readyForMachine = ReadyForMachine
        };

        return newTask;
    }

    public override List<ushort> GetCars()
    {
        return CarNetIDs.ToList();
    }
}