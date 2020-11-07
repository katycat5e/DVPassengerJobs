using System.Collections.Generic;
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

                    PassengerJobs.ModEntry.Logger.Log("Successfully created platform controller for track " + def.TrackId);
                    
                    def.Initialized = true;
                }
            }
        }
    }
}
