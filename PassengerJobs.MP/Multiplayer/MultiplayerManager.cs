using MPAPI.Interfaces;
using MPAPI;
using PassengerJobs.MP.Multiplayer.Serializers;
using PassengerJobs.Platforms;
using System.Linq;

namespace PassengerJobs.MP.Multiplayer;

public static class MultiplayerManager
{
    private static PJServer? _server;
    public static PJClient? Client { get; private set; }
    
    public static void Init()
    {
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

        // Listen for server creation
        MultiplayerAPI.ServerStarted += OnServerCreated;
        MultiplayerAPI.ServerStopped += OnServerStopped;

        // Listen for client creation
        MultiplayerAPI.ClientStarted += OnClientCreated;
        MultiplayerAPI.ClientStopped += OnClientStopped;
    }

    #region Server
    private static void OnServerCreated(IServer server)
    {
        _server = new PJServer(server);
    }

    private static void OnServerStopped()
    {
        _server?.Dispose();
        _server = null;
    }
    #endregion

    #region Client
    private static void OnClientCreated(IClient client)
    {
        Client = new PJClient(client);
    }

    private static void OnClientStopped()
    {
        Client?.Dispose();
        Client = null;
    }
    #endregion

    #region Utilities
    public static TrainCar GetTrainCarFromID(string carId)
    {
        return TrainCarRegistry.Instance.logicCarToTrainCar.FirstOrDefault(kvp => kvp.Value.ID == carId).Value;
    }
    #endregion
}
