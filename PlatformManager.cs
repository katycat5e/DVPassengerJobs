using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DV.Logic.Job;
using Harmony12;
using TMPro;
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
        Normal,
        Small
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
                    new PlatformDefinition("CSW-B-3-LP", "CSW Platform 3"),
                    new PlatformDefinition("CSW-B-6-LP", "CSW Platform 6")
                } // not enough clearance: "CSW-B-4-LP", "CSW-B-5-LP"
            },
            { "MF", new PlatformDefinition[]
                {
                    new PlatformDefinition("MF-D-1-LP", "MF Platform 1"),
                    new PlatformDefinition("MF-D-2-LP", "MF Platform 2")
                }
            },
            { "FF", new PlatformDefinition[]
                {
                    new PlatformDefinition("FF-B-1-LP", "FF Platform 1"),
                    new PlatformDefinition("FF-B-2-LP", "FF Platform 2")
                }
            },
            { "HB", new PlatformDefinition[]
                {
                    new PlatformDefinition("HB-F-1-LP", "HB Platform 1")
                } // not enough clearance: "HB-F-2-LP"
            },
            { "GF", new PlatformDefinition[]
                {
                    new PlatformDefinition("GF-C-3-LP", "GF Platform 3")
                }
            }
        };

        private static readonly Dictionary<string, int> NextPlatformChoice = new Dictionary<string, int>();

        public static readonly Dictionary<string, SignDefinition[]> SignLocations = new Dictionary<string, SignDefinition[]>()
        {
            { "CSW-B-6-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 39.23f, 129.07f, 402.07f, 0.686f, 0.728f), // building side
                    new SignDefinition(StationSignType.Normal, 147.6f, 129.07f, 517.05f, 0.686f, 0.728f) // exit side
                }
            },
            { "CSW-B-3-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Small, 50.31f, 127.06f, 386.13f, -0.686f, -0.728f), // building side
                    new SignDefinition(StationSignType.Small, 158.67f, 127.06f, 501.11f, -0.686f, -0.728f) // exit side
                }
            },
            // MF
            { "MF-D-1-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 645.07f, 165.63f, 232.8f, 0.028f, 1.0f), // north
                    new SignDefinition(StationSignType.Normal, 640.8f, 165.63f, 82.86f, 0.028f, 1.0f) // south
                }
            },
            { "MF-D-2-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 649.47f, 165.63f, 232.67f, -0.028f, -1.0f), // north
                    new SignDefinition(StationSignType.Normal, 645.2f, 165.63f, 82.73f, -0.028f, -1.0f) // south
                }
            },
            // FF
            { "FF-B-1-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 432.47f, 125.65f, 245.54f, -0.359f, -0.934f), // south
                    new SignDefinition(StationSignType.Normal, 443.92f, 125.65f, 275.42f, -0.359f, -0.934f) // north
                }
            },
            { "FF-B-2-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 428.36f, 125.65f, 247.12f, 0.359f, 0.934f), // south
                    new SignDefinition(StationSignType.Normal, 439.82f, 125.65f, 277.0f, 0.359f, 0.934f) // north
                }
            },
            // HB
            { "HB-F-1-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 322.57f, 119.89f, 269.16f, 0.999f, -0.05f), // west
                    new SignDefinition(StationSignType.Normal, 456.39f, 119.89f, 262.24f, 0.999f, -0.05f) // east
                }
            },
            { "GF-C-3-LP", new SignDefinition[]
                {
                    new SignDefinition(StationSignType.Normal, 734.72f, 146.55f, 437.86f, 0.88f, 0.47f), // loop side
                    new SignDefinition(StationSignType.Normal, 651.85f, 146.55f, 393.49f, 0.88f, 0.47f) // entry side
                }
            }
        };

        private static GameObject SignPrefab = null;
        private static GameObject SmallSignPrefab = null;
        private static bool SignLoadAttempted = false;
        private static bool SignLoadFailed = false;

        private static readonly Dictionary<string, PlatformDefinition> TrackToPlatformMap = new Dictionary<string, PlatformDefinition>();

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
                    def.Controller.Initialize(pTrack, def.Name);

                    TrackToPlatformMap.Add(def.PlatformTrack.ID.FullID, def);

                    def.Controller.SignsActive = signsEnabled;
                    CreatePlatformSigns(def.TrackId, def.Controller);

                    PassengerJobs.ModEntry.Logger.Log("Successfully created platform controller for track " + def.TrackId);
                    
                    def.Initialized = true;
                }
            }
        }

        private static bool TryLoadSignPrefab()
        {
            string bundlePath = Path.Combine(PassengerJobs.ModEntry.Path, "passengerjobs");
            PassengerJobs.ModEntry.Logger.Log("Attempting to load platform sign prefab");

            var bytes = File.ReadAllBytes(bundlePath);
            var bundle = AssetBundle.LoadFromMemory(bytes);

            if( bundle != null )
            {
                SignPrefab = bundle.LoadAsset<GameObject>("Assets/StationSign.prefab");
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
            if( !SignLoadAttempted && !TryLoadSignPrefab() ) return; // failed to load

            if( SignLocations.TryGetValue(trackId, out SignDefinition[] signList) )
            {
                foreach( SignDefinition sign in signList )
                {
                    GameObject proto = (sign.SignType == StationSignType.Small) ? SmallSignPrefab : SignPrefab;

                    var newObj = GameObject.Instantiate(proto, sign.Position, sign.Rotation);
                    if( newObj != null )
                    {
                        controller.AddSign(newObj);
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
