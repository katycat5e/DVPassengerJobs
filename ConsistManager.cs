using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PassengerJobsMod
{
    public static class ConsistManager
    {
        private static TrainCarType[] _passCarTypes = null;
        public static TrainCarType[] PassCarTypes
        {
            get
            {
                // late initialization of all passenger-capable cars
                if (_passCarTypes == null)
                {
                    _passCarTypes = CargoTypes.GetTrainCarTypesThatAreSpecificContainerType(CargoContainerType.Passengers).ToArray();
                    string carTypes = string.Join(", ", PassCarTypes.Select(CarTypes.DisplayName));
                    PassengerJobs.Log("Found available coach types: " + carTypes);
                }
                return _passCarTypes;
            }
        }

        public static readonly List<SpecialTrain> TrainDefinitions = new List<SpecialTrain>();

        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(SpecialTrainConfig));

        public static readonly Dictionary<string, SpecialTrain> JobToSpecialMap = new Dictionary<string, SpecialTrain>();

        public static void LoadConfig( string path )
        {
            try
            {
                using( var fileStream = new FileStream(path, FileMode.Open) )
                {
                    if( serializer.Deserialize(fileStream) is SpecialTrainConfig fileData )
                    {
                        foreach( var train in fileData.Trains )
                        {
                            if( train.CheckValidity(out string result) )
                            {
                                TrainDefinitions.Add(train);
                                PassengerJobs.ModEntry.Logger.Log($"Found named train definition {train.Name}");
                            }
                            else
                            {
                                PassengerJobs.ModEntry.Logger.Warning($"Error in special train config: {result}, in {path}");
                            }
                        }
                    }
                    else
                    {
                        PassengerJobs.ModEntry.Logger.Warning("Failed to load special train config " + path);
                    }
                }
            }
            catch( Exception ex )
            {
                PassengerJobs.ModEntry.Logger.Warning("Failed to load special train config " + path);
                PassengerJobs.ModEntry.Logger.Warning(ex.Message);
            }
        }

        public static SpecialTrain GetTrainForRoute( string startId, string destId )
        {
            List<SpecialTrain> matches = TrainDefinitions.Where(train => train.IsAllowedOnRoute(startId, destId)).ToList();

            return (matches.Count > 0) ? matches.ChooseOne() : null;
        }
    }
}
