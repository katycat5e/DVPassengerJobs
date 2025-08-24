using DV.Logic.Job;
using DV.ThingTypes;
using MPAPI;
using MPAPI.Types;
using MPAPI.Util;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PassengerJobs.Multiplayer.Serialisers;

public class CityLoadingTaskData : TaskNetworkData<CityLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public WarehouseTaskType WarehouseTaskType { get; set; }
    public string WarehouseMachineId { get; set; }
    public CargoType CargoType { get; set; }
    public float CargoAmount { get; set; }
    public bool ReadyForMachine { get; set; }

    public override void Serialize(BinaryWriter writer)
    {
        SerializeCommon(writer);

        writer.WriteUShortArray(CarNetIDs);
        writer.Write((byte)WarehouseTaskType);
        writer.Write(WarehouseMachineId ?? string.Empty);
        writer.Write((int)CargoType);
        writer.Write(CargoAmount);
        writer.Write(ReadyForMachine);
    }

    public override void Deserialize(BinaryReader reader)
    {
        DeserializeCommon(reader);
        
        CarNetIDs = reader.ReadUShortArray();
        WarehouseTaskType = (WarehouseTaskType)reader.ReadByte();
        WarehouseMachineId = reader.ReadString();
        CargoType = (CargoType)reader.ReadInt32();
        CargoAmount = reader.ReadSingle();
        ReadyForMachine = reader.ReadBoolean();
    }

    public override CityLoadingTaskData FromTask(Task task)
    {
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

        WarehouseTaskType = cityLoadingTask.warehouseTaskType;
        WarehouseMachineId = cityLoadingTask.warehouseMachine.ID;
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


        CityLoadingTask newTask = new CityLoadingTask(
           cars,
           WarehouseTaskType,
           JobSaveManager.Instance.GetWarehouseMachineWithId(WarehouseMachineId),
           CargoType,
           CargoAmount
       );

        newTask.readyForMachine = ReadyForMachine;

        return newTask;
    }

    public override List<ushort> GetCars()
    {
        return CarNetIDs.ToList();
    }
}