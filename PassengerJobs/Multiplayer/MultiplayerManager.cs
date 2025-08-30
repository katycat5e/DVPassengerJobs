using DVLangHelper.Data;
using MPAPI;
using MPAPI.Interfaces;
using MPAPI.Types;
using PassengerJobs.Generation;
using PassengerJobs.Multiplayer.Packets;
using PassengerJobs.Multiplayer.Serializers;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PassengerJobs.Multiplayer;

public static class MultiplayerManager
{
    private static IServer? _server;
    private static IClient? _client;

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
        PJMain.Log("Server created");
        _server = server;
        _server.OnPlayerConnected += OnPlayerConnected;
    }

    private static void OnServerStopped()
    {
        PJMain.Log("Server stopped");
        _server = null;
    }

    private static void OnPlayerConnected(IPlayer player)
    {
        PJMain.Log($"Player {player.Username} connected");

        if (player.IsHost)
            return;

        PJMain.Log($"Sending {player.Username} Settings");
        SendPJSettings(player);

        PJMain.Log($"Sending {player.Username} Stations");
        SendPJStationData(player);
    }

    public static void SendPJSettings(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Warning("Tried to send PJ settings to player but server is null");
            return;
        }

    }

    public static void SendPJStationData(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Warning("Tried to send PJ station data but server is null");
            return;
        }

        PJMain.Log($"Preparing station data packet. CityStations: {RouteManager.CityStations?.Length ?? 0}, RuralStations: {RouteManager.RuralStations?.Length ?? 0}, SignLocations: {SignManager.SignLocations?.Count ?? 0}, ");

        var packet = new ClientBoundPJStationDataPacket
        {
            CityStations = RouteManager.CityStations,
            RuralStations = RouteManager.RuralStations,
            RuralStationTranslations = GetStationTranslations(),
            SignLocations = SignManager.SignLocations,
        };

        if (player == null)
            _server.SendSerializablePacketToAll(packet);
        else
            _server.SendSerializablePacketToPlayer(packet, player);
    }
    #endregion

    #region Client
    private static void OnClientCreated(IClient client)
    {
        PJMain.Log("Client created");
        _client = client;

        // Register for incoming packets
        _client.RegisterPacket<ClientBoundPJSettingsPacket>(OnClientBoundPJSettingsPacket);
        _client.RegisterSerializablePacket<ClientBoundPJStationDataPacket>(OnClientBoundPJStationDataPacket);
    }

    private static void OnClientStopped()
    {
        PJMain.Log("Client stopped");
        _client = null;

        if (!UnloadWatcher.isQuitting)
            PJMain.Translations.Reload();

        SignManager.TryLoadSignLocations();
    }

    private static void OnClientBoundPJSettingsPacket(ClientBoundPJSettingsPacket packet)
    {
        PJMain.Log($"OnClientBoundPJSettingsPacket() UseCustomWages: {packet.UseCustomWages}, CoachLightMode: {packet.CoachLights}, UseCustomCoachLightColour: {packet.UseCustomCoachLightColour}, CustomCoachLightColour: {packet.CustomCoachLightColour}, CoachLightsRequirePower: {packet.CoachLightsRequirePower}");
    }

    private static void OnClientBoundPJStationDataPacket(ClientBoundPJStationDataPacket packet)
    {
#if DEBUG
        StringBuilder sb = new("OnClientBoundPJStationDataPacket()\r\n");

        if (packet.CityStations != null)
            foreach (var city in packet.CityStations)
                sb.AppendLine($"CityStation: {city.yardId}, Platforms: {city.platforms?.Length ?? 0}, TerminusTracks: {city.terminusTracks?.Length ?? 0}, Storage: {city.storage?.Length ?? 0}");

        if (packet.RuralStations != null)
            foreach (var city in packet.RuralStations)
                sb.AppendLine($"RuralStation: {city.id}, Location: {city.location}, SwapSides: {city.swapSides}, HideConcrete: {city.hideConcrete}, HideLamps: {city.hideLamps}, ExtraHeight: {city.extraHeight}, MarkerAngle: {city.markerAngle}");

        if (packet.RuralStationTranslations != null)
            foreach (var kvp in packet.RuralStationTranslations)
                sb.AppendLine($"RuralStationTranslation: {kvp.Key}, Translations: {string.Join("\r\n\t", kvp.Value)}");

        if (packet.SignLocations != null)
            foreach (var kvp in packet.SignLocations)
                sb.AppendLine($"SignLocation: {kvp.Key}, Signs: {kvp.Value.Count}");

        PJMain.Log(sb.ToString());
#endif


    }
    #endregion


    #region helpers
    public static TrainCar GetTrainCarFromID(string carId)
    {
        return TrainCarRegistry.Instance.logicCarToTrainCar.FirstOrDefault(kvp => kvp.Value.ID == carId).Value;
    }

    private static Dictionary<string, string[]>? GetStationTranslations()
    {
        if (RouteManager.RuralStations == null || RouteManager.RuralStations.Length == 0)
            return null;

        var ret = new Dictionary<string, string[]>(RouteManager.RuralStations.Length);

        foreach (var station in RouteManager.RuralStations)
        {
            var key = $"{LocalizationKeyExtensions.STATION_NAME_KEY}{station.id.ToLower()}";
            var translations = PJMain.Translations.Terms.Where(t => t.Term.Equals(key.ToLower())).FirstOrDefault();

            if (translations == null || translations.Languages == null || translations.Languages.Length == 0)
                continue;

            ret[station.id] = translations.Languages;
        }

        return ret;
    }

    private static void SetStationTranslations(Dictionary<string, string[]>? translations)
    {
        var languages = PJMain.Translations.Languages.Select(l => l.Code).ToArray();

        if (translations == null)
        {
            PJMain.Warning("Tried to set station translations, but translations are null");
            return;
        }

        foreach (var kvp in translations) 
        {
            var key = $"{LocalizationKeyExtensions.STATION_NAME_KEY}{kvp.Key.ToLower()}";

            for (int i = 0; i < kvp.Value.Length; i++) 
            {
                var translation = kvp.Value[i];

                if(!Enum.IsDefined(typeof(DVLanguage), i))
                {
                    PJMain.Error($"Failed to add translation \"{translation}\" for station {kvp.Key}, {i} is not a valid DVLanguage enum value");
                    break;
                }

                var dvLang = (DVLanguage)i;

#if DEBUG
                PJMain.Log($"SetStationTranslations() i: {i}, dvLang {dvLang}, translation: {translation}");
#endif
                PJMain.Translations.AddTranslation(key, dvLang, translation, true);

                _resetTranslations = true;
            }
        }
    }
    #endregion
}
