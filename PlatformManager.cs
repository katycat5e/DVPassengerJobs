using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.Logic.Job;
using UnityEngine;

namespace PassengerJobsMod
{
    class PlatformDefinition
    {
        public readonly string TrackId;
        public readonly string Name;

        public bool Initialized = false;
        public PlatformController Controller = null;
        public Track PlatformTrack = null;

        public PlatformDefinition( string tId, string name )
        {
            TrackId = tId;
            Name = name;
        }
    }

    enum StationSignType
    {
        Flatscreen,
        Small,
        Lillys
    }

    class SignDefinition
    {
        public readonly StationSignType SignType;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public SignDefinition( StationSignType type, float x, float y, float z, float normX, float normZ )
        {
            SignType = type;
            Position = new Vector3(x, y, z);
            Vector3 forward = new Vector3(normX, 0, normZ); // y comp always 0
            Rotation = Quaternion.FromToRotation(Vector3.forward, forward);
        }
    }

    static class PlatformManager
    {
        public static readonly Dictionary<string, PlatformDefinition[]> PlatformDefs = new Dictionary<string, PlatformDefinition[]>()
        {
            { "CSW",new PlatformDefinition[]
                {
                    new PlatformDefinition("CSW-B3LP", "CSW Platform 3"),
                    new PlatformDefinition("CSW-B4LP", "CSW Platform 4"),
                    new PlatformDefinition("CSW-B5LP", "CSW Platform 5"),
                    new PlatformDefinition("CSW-B6LP", "CSW Platform 6")
                }
            },
            { "MF", new PlatformDefinition[]
                {
                    new PlatformDefinition("MF-D1LP", "MF Platform 1"),
                    new PlatformDefinition("MF-D2LP", "MF Platform 2")
                }
            },
            { "FF", new PlatformDefinition[]
                {
                    new PlatformDefinition("FF-B1LP", "FF Platform 1"),
                    new PlatformDefinition("FF-B2LP", "FF Platform 2")
                }
            },
            { "HB", new PlatformDefinition[]
                {
                    new PlatformDefinition("HB-F1LP", "HB Platform 1")
                } // not enough clearance: "HB-F-2-LP"
            },
            { "GF", new PlatformDefinition[]
                {
                    new PlatformDefinition("GF-C3LP", "GF Platform 3")
                }
            }
        };

        private static readonly Dictionary<string, int> NextPlatformChoice = new Dictionary<string, int>();

        public static readonly Dictionary<string, List<SignDefinition>> SignLocations = new Dictionary<string, List<SignDefinition>>();
        /*
        {
            { "CSW-B6LP", new SignDefinition[]
                {
                    new SignDefinition( StationSignType.Flatscreen, 1685.22f, 129.07f, 5340.07f, 0.686f, 0.728f), // building side
                    new SignDefinition( StationSignType.Flatscreen, 1793.59f, 129.07f, 5455.05f, 0.686f, 0.728f) // exit side
                }
            },
            { "CSW-B5LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 1688.43f, 129.07f, 5337.05f, -0.686f, -0.728f), // building side
                    new SignDefinition(StationSignType.Flatscreen, 1796.80f, 129.07f, 5452.03f, -0.686f, -0.728f) // exit side
                }
            },
            { "CSW-B4LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Small, 1693.69f, 127.06f, 5326.60f, 0.686f, 0.728f), // building side
                    new SignDefinition(StationSignType.Small, 1802.05f, 127.06f, 5441.58f, 0.686f, 0.728f) // exit side
                }
            },
            { "CSW-B3LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Small, 1696.31f, 127.06f, 5324.13f, -0.686f, -0.728f), // building side
                    new SignDefinition(StationSignType.Small, 1804.67f, 127.06f, 5439.11f, -0.686f, -0.728f) // exit side
                }
            },
            // MF
            { "MF-D1LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 2291.08f, 165.63f, 10931.79f, 0.028f, 1.0f), // north
                    new SignDefinition(StationSignType.Flatscreen, 2286.80f, 165.63f, 10781.85f, 0.028f, 1.0f) // south
                }
            },
            { "MF-D2LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 2295.48f, 165.63f, 10931.67f, -0.028f, -1.0f), // north
                    new SignDefinition(StationSignType.Flatscreen, 2291.20f, 165.63f, 10781.73f, -0.028f, -1.0f) // south
                }
            },
            // FF
            { "FF-B1LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 9485.47f, 125.65f, 13413.54f, -0.359f, -0.934f), // south
                    new SignDefinition(StationSignType.Flatscreen, 9496.94f, 125.65f, 13443.41f, -0.359f, -0.934f) // north
                }
            },
            { "FF-B2LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 9481.36f, 125.65f, 13415.12f, 0.359f, 0.934f), // south
                    new SignDefinition(StationSignType.Flatscreen, 9492.83f, 125.65f, 13444.99f, 0.359f, 0.934f) // north
                }
            },
            // HB
            { "HB-F1LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 13624.38f, 119.89f, 3554.24f, 0.999f, -0.05f), // west
                    new SignDefinition(StationSignType.Flatscreen, 13490.56f, 119.89f, 3561.14f, 0.999f, -0.05f) // east
                }
            },
            { "GF-C3LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Flatscreen, 13079.73f, 146.55f, 11136.85f, 0.88f, 0.47f), // loop side
                    new SignDefinition(StationSignType.Flatscreen, 12996.85f, 146.55f, 11092.48f, 0.88f, 0.47f) // entry side
                }
            }
        };
        */

        private static GameObject SignPrefab = null;
        private static GameObject SmallSignPrefab = null;
        private static GameObject LillySignPrefab = null;

        private static bool SignLoadAttempted = false;
        private static bool SignLoadFailed = false;

        private static readonly Dictionary<string, PlatformDefinition> TrackToPlatformMap = new Dictionary<string, PlatformDefinition>();

        public static void TryLoadSignLocations()
        {
            string configFilePath = Path.Combine(PassengerJobs.ModEntry.Path, "platform_signs.csv");
            if( File.Exists(configFilePath) )
            {
                try
                {
                    using( var configFile = new StreamReader(configFilePath) )
                    {
                        int lineNum = 1;

                        while( !configFile.EndOfStream )
                        {
                            string line = configFile.ReadLine().Trim();
                            
                            // check for blank or comment
                            if( string.IsNullOrWhiteSpace(line) || line.StartsWith("#") ) continue;

                            string[] columns = line.Split(',');
                            if( string.IsNullOrWhiteSpace(columns[0]) ) continue;
                            if( columns.Length < 7 )
                            {
                                PassengerJobs.ModEntry.Logger.Error($"Missing columns on line {lineNum} of platform_signs.csv");
                                continue;
                            }

                            if( !Enum.TryParse(columns[1], out StationSignType signType) )
                            {
                                PassengerJobs.ModEntry.Logger.Error($"Invalid sign type \"{columns[1]}\" on line {lineNum} of platform_signs.csv");
                                continue;
                            }

                            float[] fVals = new float[5];
                            for( int colIdx = 0; colIdx < 5; colIdx++ )
                            {
                                if( float.TryParse(columns[colIdx + 2], out float fVal) )
                                {
                                    fVals[colIdx] = fVal;
                                }
                                else
                                {
                                    PassengerJobs.ModEntry.Logger.Error($"Invalid coordinate on line {lineNum} of platform_signs.csv");
                                    continue;
                                }
                            }

                            var newSign = new SignDefinition(signType,
                                fVals[0], fVals[1], fVals[2],
                                fVals[3], fVals[4]);

                            if( !SignLocations.TryGetValue(columns[0], out List<SignDefinition> signList) )
                            {
                                signList = new List<SignDefinition>();
                                SignLocations.Add(columns[0], signList);
                            }
                            signList.Add(newSign);

                            lineNum += 1;
                        }
                    }
                }
                catch( Exception ex )
                {
                    PassengerJobs.ModEntry.Logger.Error("Failed to open sign locations file: " + ex.Message);
                }
            }
        }

        public static List<PlatformDefinition> GetAvailablePlatforms( string yard )
        {
            if( PlatformDefs.TryGetValue(yard, out var defList) )
            {
                return new List<PlatformDefinition>(defList.Where(p => p.Initialized));
            }
            return null;
        }

        public static PlatformDefinition PickPlatform( string yard )
        {
            PlatformDefinition loadingPlatform = null;
            var platformList = GetAvailablePlatforms(yard);
            if( platformList != null )
            {
                int nextIdx = NextPlatformChoice[yard];
                loadingPlatform = platformList[nextIdx];

                nextIdx += 1;
                if( nextIdx >= platformList.Count ) nextIdx = 0;

                NextPlatformChoice[yard] = nextIdx;
            }
            return loadingPlatform;
        }

        public static PlatformController GetController( string yard, string trackId )
        {
            if( PlatformDefs.TryGetValue(yard, out var defList) )
            {
                foreach( var def in defList )
                {
                    if( string.Equals(trackId, def.TrackId) ) return def.Controller;
                }
            }
            return null;
        }

        public static PlatformDefinition GetPlatformByTrackId( string fullId )
        {
            if( TrackToPlatformMap.TryGetValue(fullId, out PlatformDefinition p) )
            {
                return p;
            }
            return null;
        }

        public static void CreateMachines( StationController station, bool signsEnabled )
        {
            if( PlatformDefs.TryGetValue(station.stationInfo.YardID, out var defList) )
            {
                NextPlatformChoice[station.stationInfo.YardID] = 0;

                foreach( PlatformDefinition def in defList )
                {
                    //PassengerJobs.ModEntry.Logger.Log("Creating platform controller for " + def.Name);

                    if( PassengerJobGenerator.FindRTWithId(def.TrackId) is RailTrack pTrack )
                    {
                        def.PlatformTrack = pTrack.logicTrack;
                        //PassengerJobs.ModEntry.Logger.Log("Set track");
                    }
                    else
                    {
                        PassengerJobs.ModEntry.Logger.Warning("Couldn't find platform track for " + def.Name);
                        return;
                    }

                    // create dummy object to hold controller
                    var dummyObj = new GameObject($"PlatformController_{def.TrackId}");
                    dummyObj.transform.SetParent(station.gameObject.transform, false);

                    def.Controller = dummyObj.AddComponent<PlatformController>();
                    def.Controller.Initialize(pTrack, def.Name, station.stationInfo.YardID);

                    TrackToPlatformMap.Add(def.PlatformTrack.ID.FullID, def);

                    def.Controller.SignsActive = signsEnabled;
                    CreatePlatformSigns(def.TrackId, def.Controller);

                    PassengerJobs.ModEntry.Logger.Log("Successfully created platform controller for track " + def.TrackId);
                    
                    def.Initialized = true;
                }
            }
        }

        private static bool TryLoadSignPrefabs()
        {
            string bundlePath = Path.Combine(PassengerJobs.ModEntry.Path, "passengerjobs");
            PassengerJobs.ModEntry.Logger.Log("Attempting to load platform sign prefab");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if( bundle != null )
            {
                SignPrefab = bundle.LoadAsset<GameObject>("Assets/FlatscreenSign.prefab");
                if( SignPrefab == null )
                {
                    PassengerJobs.ModEntry.Logger.Error("Failed to load platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                    return false;
                }

                SmallSignPrefab = bundle.LoadAsset<GameObject>("Assets/SmolStationSign.prefab");
                if( SmallSignPrefab == null )
                {
                    PassengerJobs.ModEntry.Logger.Error("Failed to load small platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                    return false;
                }

                LillySignPrefab = bundle.LoadAsset<GameObject>("Assets/LillySign.prefab");
                if( LillySignPrefab == null )
                {
                    PassengerJobs.ModEntry.Logger.Error("Failed to load small platform sign prefab from asset bundle");
                    SignLoadFailed = true;
                    return false;
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Error("Failed to load asset bundle");
                SignLoadFailed = true;
                return false;
            }

            PassengerJobs.ModEntry.Logger.Log("Loaded sign prefab");
            SignLoadAttempted = true;
            return true;
        }

        private static void CreatePlatformSigns( string trackId, PlatformController controller )
        {
            if( SignLoadFailed ) return;
            if( !SignLoadAttempted && !TryLoadSignPrefabs() ) return; // failed to load

            if( SignLocations.TryGetValue(trackId, out List<SignDefinition> signList) )
            {
                foreach( SignDefinition sign in signList )
                {
                    GameObject proto;
                    switch( sign.SignType )
                    {
                        case StationSignType.Small:
                            proto = SmallSignPrefab;
                            break;

                        case StationSignType.Lillys:
                            proto = LillySignPrefab;
                            break;

                        default:
                        case StationSignType.Flatscreen:
                            proto = SignPrefab;
                            break;
                    }

                    Vector3 relativePos = sign.Position + WorldMover.currentMove;

                    var newObj = GameObject.Instantiate(proto, relativePos, sign.Rotation);
                    if( newObj != null )
                    {
                        SingletonBehaviour<WorldMover>.Instance.AddObjectToMove(newObj.transform);
                        controller.AddSign(newObj);

                        PassengerJobs.ModEntry.Logger.Log($"Created sign {sign.SignType} at {trackId}");
                    }
                    else
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"Failed to create sign {sign.SignType} at {trackId}");
                    }
                }
            }
        }

        public static void SetSignStates( string yardId, bool newState )
        {
            if( PlatformDefs.TryGetValue(yardId, out var platforms) )
            {
                foreach( PlatformDefinition p in platforms )
                {
                    if( p.Initialized && (p.Controller.SignsActive != newState) )
                    {
                        string stateStr = newState ? "On" : "Off";
                        PassengerJobs.ModEntry.Logger.Log($"Setting signs at {p.Name} to {stateStr}");
                        p.Controller.SetSignState(newState);
                    }
                }
            }
        }
    }
}
