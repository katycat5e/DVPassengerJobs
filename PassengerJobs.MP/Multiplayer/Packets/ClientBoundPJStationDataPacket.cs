using MPAPI.Interfaces.Packets;
using MPAPI.Util;
using PassengerJobs.Platforms;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static PassengerJobs.Config.StationConfig;

namespace PassengerJobs.MP.Multiplayer.Packets;

public class ClientBoundPJStationDataPacket : ISerializablePacket
{
    public CityStation[]? CityStations { get; set; }
    public RuralStation[]? RuralStations { get; set; }
    public Dictionary<string, string[]>? RuralStationTranslations { get; set; }
    public Dictionary<string, List<SignDefinition>>? SignLocations { get; set; }

    #region Serialisation
    void ISerializablePacket.Serialize(BinaryWriter writer)
    {
        // Serialise CityStations
        writer.Write(CityStations?.Length ?? 0);
        if (CityStations != null)
            foreach (var station in CityStations)
                SerializeCityStation(writer, station);

        // Serialise RuralStations
        writer.Write(RuralStations?.Length ?? 0);
        if (RuralStations != null)
            foreach (var station in RuralStations!)
                SerializeRuralStation(writer, station);

        // Serialise RuralStationTranslations
        SerializeRuralStationTranslations(writer);

        // Serialise SignLocations
        writer.Write(SignLocations?.Count ?? 0);
        if (SignLocations != null)
            foreach (var kvp in SignLocations)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Count);
                foreach (var sign in kvp.Value)
                    SerializeSignDefinition(writer, sign);
            }
    }

    private void SerializeCityStation(BinaryWriter writer, CityStation station)
    {
        //PJMain.Log($"SerializeCityStation({station.yardId})");

        writer.Write(station.yardId);

        writer.Write(station.platforms?.Length ?? 0);
        if (station.platforms != null)
            foreach (var platform in station.platforms)
                SerializeCityPlatform(writer, platform);

        writer.Write(station.terminusTracks?.Length ?? 0);
        if (station.terminusTracks != null)
            foreach (var track in station.terminusTracks)
                writer.Write(track);

        writer.Write(station.storage?.Length ?? 0);
        if (station.storage != null)
            foreach (var storage in station.storage)
                writer.Write(storage);
    }

    private void SerializeCityPlatform(BinaryWriter writer, CityPlatform platform)
    {
        //PJMain.Log($"SerializeCityStation({platform.id})");

        writer.Write(platform.id);

        writer.Write(platform.spawnZoneA.HasValue);
        if (platform.spawnZoneA.HasValue)
            writer.WriteVector3((Vector3)platform.spawnZoneA);

        writer.Write(platform.spawnZoneB.HasValue);
        if (platform.spawnZoneB.HasValue)
            writer.WriteVector3((Vector3)platform.spawnZoneB);

        writer.Write(platform.spawnZoneDepth.HasValue);
        if (platform.spawnZoneDepth.HasValue)
            writer.Write(platform.spawnZoneDepth.Value);

        writer.Write(platform.spacing.HasValue);
        if (platform.spacing.HasValue)
            writer.Write(platform.spacing.Value);
    }

    private void SerializeRuralStation(BinaryWriter writer, RuralStation station)
    {
        //PJMain.Log($"SerializeRuralStation({station.id})");

        writer.Write(station.id);

        writer.WriteVector3(station.location);

        writer.Write(station.swapSides);

        writer.Write(station.hideConcrete);
        writer.Write(station.hideLamps);
        writer.Write(station.extraHeight);

        writer.Write(station.markerAngle.HasValue);
        if (station.markerAngle.HasValue)
            writer.Write(station.markerAngle.Value);
    }

    private void SerializeRuralStationTranslations(BinaryWriter writer)
    {
        writer.Write(RuralStationTranslations?.Count ?? 0);

        if (RuralStationTranslations != null)
            foreach (var kvp in RuralStationTranslations)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Length);
                foreach (var translation in kvp.Value)
                    writer.Write(translation);
            }
    }

    private void SerializeSignDefinition(BinaryWriter writer, SignDefinition sign)
    {
        //PJMain.Log($"SerializeSignDefinition({sign.SignType} @ {sign.Position})");

        writer.Write((byte)sign.SignType);
        writer.WriteVector3(sign.Position);

        Vector3 forward = sign.Rotation * Vector3.forward;
        writer.Write(forward.x);
        writer.Write(forward.z);
    }

    #endregion

    #region Deserialisation

    void ISerializablePacket.Deserialize(BinaryReader reader)
    {
        // Deserialise CityStations
        var stationCount = reader.ReadInt32();
        CityStations = new CityStation[stationCount];

        //PJMain.Log($"Deserializing {stationCount} CityStations");

        for (var i = 0; i < stationCount; i++)
            CityStations[i] = DeserializeCityStation(reader);

        // Deserialise RuralStations
        stationCount = reader.ReadInt32();
        RuralStations = new RuralStation[stationCount];

        //PJMain.Log($"Deserializing {stationCount} RuralStations");

        for (var i = 0; i < stationCount; i++)
            RuralStations[i] = DeserializeRuralStation(reader);

        // Deserialise RuralStationTranslations
        DeserializeRuralStationTranslations(reader);

        // Deserialise SignLocations
        var signLocationCount = reader.ReadInt32();
        SignLocations = new Dictionary<string, List<SignDefinition>>(signLocationCount);

        //PJMain.Log($"Deserializing {signLocationCount} SignLocations");

        for (var i = 0; i < signLocationCount; i++)
        {
            var platformId = reader.ReadString();

            var signCount = reader.ReadInt32();
            var signs = new List<SignDefinition>(signCount);
            for (var j = 0; j < signCount; j++)
                signs.Add(DeserializeSignDefinition(reader));

            SignLocations[platformId] = signs;
        }
    }

    private CityStation DeserializeCityStation(BinaryReader reader)
    {
        string yardId = reader.ReadString();

        int platformCount = reader.ReadInt32();
        var platforms = new CityPlatform[platformCount];
        for (var i = 0; i < platformCount; i++)
            platforms[i] = DeserializeCityPlatform(reader);

        int terminusTrackCount = reader.ReadInt32();
        var terminusTracks = new string[terminusTrackCount];
        for (var i = 0; i < terminusTrackCount; i++)
            terminusTracks[i] = reader.ReadString();

        int storageCount = reader.ReadInt32();
        var storage = new string[storageCount];
        for (var i = 0; i < storageCount; i++)
            storage[i] = reader.ReadString();

        return new CityStation()
        {
            yardId = yardId,
            platforms = platforms,
            terminusTracks = terminusTracks,
            storage = storage
        };
    }

    private CityPlatform DeserializeCityPlatform(BinaryReader reader)
    {
        string id;
        Vector3? spawnZoneA = null;
        Vector3? spawnZoneB = null;
        float? spawnZoneDepth = null;
        float? spacing = null;

        id = reader.ReadString();

        if (reader.ReadBoolean())
            spawnZoneA = reader.ReadVector3();

        if (reader.ReadBoolean())
            spawnZoneB = reader.ReadVector3();

        if (reader.ReadBoolean())
            spawnZoneDepth = reader.ReadSingle();

        if (reader.ReadBoolean())
            spacing = reader.ReadSingle();

        return new CityPlatform()
        {
            id = id,
            spawnZoneA = spawnZoneA,
            spawnZoneB = spawnZoneB,
            spawnZoneDepth = spawnZoneDepth,
            spacing = spacing
        };
    }

    private RuralStation DeserializeRuralStation(BinaryReader reader)
    {
        string id = reader.ReadString();

        // Read localisation data
        Vector3 location = reader.ReadVector3();

        bool swapSides = reader.ReadBoolean();

        bool hideConcrete = reader.ReadBoolean();
        bool hideLamps = reader.ReadBoolean();
        float extraHeight = reader.ReadSingle();

        float? markerAngle = null;
        if (reader.ReadBoolean())
            markerAngle = reader.ReadSingle();

        return new RuralStation()
        {
            id = id,
            location = location,
            swapSides = swapSides,
            hideConcrete = hideConcrete,
            hideLamps = hideLamps,
            extraHeight = extraHeight,
            markerAngle = markerAngle
        };
    }

    private void DeserializeRuralStationTranslations(BinaryReader reader)
    {
        var translationCount = reader.ReadInt32();

        //PJMain.Log($"Deserializing {translationCount} RuralStationTranslations");

        if (translationCount > 0)
        {
            RuralStationTranslations = new Dictionary<string, string[]>(translationCount);
            for (int i = 0; i < translationCount; i++)
            {
                var stationId = reader.ReadString();
                var langCount = reader.ReadInt32();
                var translations = new string[langCount];

                for (int j = 0; j < langCount; j++)
                    translations[j] = reader.ReadString();

                RuralStationTranslations[stationId] = translations;
            }
        }
    }

    private SignDefinition DeserializeSignDefinition(BinaryReader reader)
    {
        StationSignType signType = (StationSignType)reader.ReadByte();

        Vector3 position = reader.ReadVector3();
        float normX = reader.ReadSingle();
        float normZ = reader.ReadSingle();

        return new SignDefinition(signType, position.x, position.y, position.z, normX, normZ);
    }

    #endregion
}