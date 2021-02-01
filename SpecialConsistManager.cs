using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PassengerJobsMod
{
    public static class SpecialConsistManager
    {
        public static readonly List<SpecialTrain> TrainDefinitions = new List<SpecialTrain>();

        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(SpecialTrainConfig));

        public static readonly Dictionary<string, SpecialTrain> JobToSpecialMap = new Dictionary<string, SpecialTrain>();

        public static void LoadConfig( string path )
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

        private static readonly Random rand = new Random();
        public static SpecialTrain GetTrainForRoute( string startId, string destId )
        {
            List<SpecialTrain> matches = TrainDefinitions.Where(train => train.IsAllowedOnRoute(startId, destId)).ToList();

            return (matches.Count > 0) ? matches.ChooseOne(rand) : null;
        }
    }
}
