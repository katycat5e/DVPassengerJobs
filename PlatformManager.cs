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

    static class PlatformManager
    {
        public static readonly Dictionary<string, PlatformDefinition[]> PlatformDefs = new Dictionary<string, PlatformDefinition[]>()
        {
            { "CSW",new PlatformDefinition[]
                {
                    new PlatformDefinition("CSW-B-6-LP", "CSW Platform 6"),
                    new PlatformDefinition("CSW-B-3-LP", "CSW Platform 3")
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
                    new PlatformDefinition("FF-B-2-LP", "FF Platform 2"),
                    new PlatformDefinition("FF-B-1-LP", "FF Platform 2")
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

        public static readonly Dictionary<string, float[][]> SignLocations = new Dictionary<string, float[][]>()
        {
            { "GF-C-3-LP", new float[][]
                {
                    new float[] { 734.7f, 146.5f, 437.84f, -0.88f, 0, -0.47f }, // loop side
                    new float[] { 651.86f, 146.5f, 393.50f, -0.88f, 0, -0.47f } // entry side
                }
            }
        };

        private static GameObject SignPrefab = null;
        private static bool SignLoadFailed = false;

        private static readonly Dictionary<string, PlatformDefinition> TrackToPlatformMap = new Dictionary<string, PlatformDefinition>();

        public static IEnumerable<PlatformDefinition> GetAvailablePlatforms( string yard )
        {
            if( PlatformDefs.TryGetValue(yard, out var defList) )
            {
                return defList;
            }
            return null;
        }

        public static PlatformDefinition PickPlatform( string yard )
        {
            PlatformDefinition loadingPlatform = null;
            var platformList = GetAvailablePlatforms(yard);
            if( platformList != null )
            {
                foreach( PlatformDefinition def in platformList )
                {
                    if( def.Initialized && !YardTracksOrganizer.Instance.IsTrackReserved(def.PlatformTrack) )
                    {
                        loadingPlatform = def;
                        break;
                    }
                }

                // no un-reserved available
                if( loadingPlatform == null )
                {
                    loadingPlatform = platformList.FirstOrDefault(d => d.Initialized);
                }
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

        public static void CreateMachines( StationController station )
        {
            if( PlatformDefs.TryGetValue(station.stationInfo.YardID, out var defList) )
            {
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
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Error("Failed to load asset bundle");
                SignLoadFailed = true;
                return false;
            }

            PassengerJobs.ModEntry.Logger.Log("Loaded sign prefab");
            return true;
        }

        private static void CreatePlatformSigns( string trackId, PlatformController controller )
        {
            if( SignLoadFailed ) return;
            if( (SignPrefab == null) && !TryLoadSignPrefab() ) return; // failed to load

            if( SignLocations.TryGetValue(trackId, out float[][] signList) )
            {
                foreach( float[] props in signList )
                {
                    Vector3 position = new Vector3(props[0], props[1], props[2]);
                    Vector3 normal = new Vector3(props[3], props[4], props[5]);
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);

                    var newObj = GameObject.Instantiate(SignPrefab, position, rotation);
                    if( newObj != null )
                    {
                        controller.AddSign(newObj);
                    }
                }
            }
        }
    }
}
