using MPAPI;
using MPAPI.Types;
using PassengerJobs.Multiplayer.Serialisers;
using PassengerJobs.Platforms;

using System.Linq;

namespace PassengerJobs.Multiplayer;

public static class MultiplayerManager
{
    public static void Init()
    {
        if (!MultiplayerAPI.IsMultiplayerLoaded)
            return;

        // Loaded API Version is the version of MultiplayerAPI.dll loaded in memory.
        // Supported API Version is the version of MultiplayerAPI.dll that Multiplayer mod was built against.
        // Supported API version must be equal to or greater than Loaded API Version.
        PJMain.Log($"Multiplayer Mod is loaded. Loaded Multiplayer API Version: {MultiplayerAPI.LoadedApiVersion}, Multiplayer's API Version: {MultiplayerAPI.SupportedApiVersion}");

        // All players are required to have Passenger Jobs mod installed.
        MultiplayerAPI.Instance.SetModCompatibility(PJMain.ModEntry.Info.Id, MultiplayerCompatibility.All);

        // Register custom task types for Multiplayer serialisation and deserialisation.
        MultiplayerAPI.Instance.RegisterTaskType<RuralLoadingTask>
        (
            RuralLoadingTask.TaskType,
            task => new RuralLoadingTaskData { TaskType = RuralLoadingTask.TaskType }.FromTask(task),
            type => new RuralLoadingTaskData { TaskType = type }
        );

        MultiplayerAPI.Instance.RegisterTaskType<CityLoadingTask>
        (
            CityLoadingTask.TaskType,
            task => new CityLoadingTaskData { TaskType = CityLoadingTask.TaskType }.FromTask(task),
            type => new CityLoadingTaskData { TaskType = type }
        );
    }

    #region helpers
    public static TrainCar GetTrainCarFromID(string carId)
    {
        return TrainCarRegistry.Instance.logicCarToTrainCar.FirstOrDefault(kvp => kvp.Value.ID == carId).Value;
    }
    #endregion
}
