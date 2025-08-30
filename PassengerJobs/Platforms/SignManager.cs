using DV.Signs;
using DV.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public enum StationSignType
    {
        Flatscreen,
        Small,
        Lillys,
    }

    public class SignDefinition
    {
        public readonly StationSignType SignType;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public SignDefinition(StationSignType type, float x, float y, float z, float normX, float normZ)
        {
            SignType = type;
            Position = new Vector3(x, y, z);
            Vector3 forward = new(normX, 0, normZ); // y comp always 0
            Rotation = Quaternion.FromToRotation(Vector3.forward, forward);
        }
    }

    public static class SignManager
    {
        private static readonly Dictionary<string, List<SignDefinition>> _signLocations = new();
        public static Dictionary<string, List<SignDefinition>> SignLocations => _signLocations.ToDictionary(kvp=> kvp.Key, kvp => kvp.Value);

        public static void TryLoadSignLocations()
        {
            string configFilePath = Path.Combine(PJMain.ModEntry.Path, "platform_signs.csv");
            if (!File.Exists(configFilePath))
            {
                PJMain.Error("Couldn't open platform_signs.csv config file");
            }

            try
            {
                using var configFile = new StreamReader(configFilePath);
                int lineNum = 1;

                while (!configFile.EndOfStream)
                {
                    string line = configFile.ReadLine().Trim();

                    // check for blank or comment
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] columns = line.Split(',');
                    if (string.IsNullOrWhiteSpace(columns[0])) continue;
                    if (columns.Length < 7)
                    {
                        PJMain.Error($"Missing columns on line {lineNum} of platform_signs.csv");
                        continue;
                    }

                    if (!Enum.TryParse(columns[1], out StationSignType signType))
                    {
                        PJMain.Error($"Invalid sign type \"{columns[1]}\" on line {lineNum} of platform_signs.csv");
                        continue;
                    }

                    float[] fVals = new float[5];
                    for (int colIdx = 0; colIdx < 5; colIdx++)
                    {
                        if (float.TryParse(columns[colIdx + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal))
                        {
                            fVals[colIdx] = fVal;
                        }
                        else
                        {
                            PJMain.Error($"Invalid coordinate on line {lineNum} of platform_signs.csv");
                            continue;
                        }
                    }

                    var newSign = new SignDefinition(signType,
                        fVals[0], fVals[1], fVals[2],
                        fVals[3], fVals[4]);

                    if (!_signLocations.TryGetValue(columns[0], out List<SignDefinition> signList))
                    {
                        signList = new List<SignDefinition>();
                        _signLocations.Add(columns[0], signList);
                    }
                    signList.Add(newSign);

                    lineNum += 1;
                }
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to open sign locations file platform_signs.csv", ex);
            }
        }

        public static IEnumerable<SignPrinter> CreatePlatformSigns(string trackId)
        {
            BundleLoader.EnsureInitialized();
            if (BundleLoader.SignLoadFailed || !_signLocations.TryGetValue(trackId, out var definitions))
            {
                yield break;
            }

            foreach (var sign in definitions)
            {
                GameObject prefab = sign.SignType switch
                {
                    StationSignType.Small => BundleLoader.SmallSignPrefab,
                    StationSignType.Lillys => BundleLoader.LillySignPrefab,
                    StationSignType.Flatscreen => BundleLoader.SignPrefab,
                    _ => throw new NotImplementedException(),
                };

                Vector3 relativePos = sign.Position + WorldMover.currentMove;

                var signInstance = UnityEngine.Object.Instantiate(prefab, relativePos, sign.Rotation);
                if (signInstance != null)
                {
                    SingletonBehaviour<WorldMover>.Instance.AddObjectToMove(signInstance.transform);
                    var newDisplay = signInstance.AddComponent<SignPrinter>();
                    newDisplay.SignType = sign.SignType;

                    PJMain.Log($"Created sign {sign.SignType} at {trackId}");
                    yield return newDisplay;
                }
                else
                {
                    PJMain.Warning($"Failed to create sign {sign.SignType} at {trackId}");
                }
            }
        }
    }
}
