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
        MultiplayerAPI.Instance.RegisterTaskType<RuralLoadingTask, RuralLoadingTaskData>(RuralLoadingTask.TaskType);
        MultiplayerAPI.Instance.RegisterTaskType<CityLoadingTask, CityLoadingTaskData>(CityLoadingTask.TaskType);
    
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
    #endregion
}
