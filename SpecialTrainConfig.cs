using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PassengerJobsMod
{
    public class SpecialTrainConfig
    {
        [XmlElement(ElementName = "Train")]
        public SpecialTrain[] Trains;
    }

    public class SpecialTrain
    {
        public string Name = null;
        public string Skin = null;

        [XmlAttribute("CarType")]
        public string CarTypeString = null;

        [XmlIgnore]
        public TrainCarType CarType;

        [XmlAttribute("Routes")]
        public string RouteString = null;

        [XmlIgnore]
        public bool AllowAnyRoute = false;

        [XmlIgnore]
        public StationsChainData[] Routes = null;

        public bool IsAllowedOnRoute( string start, string end )
        {
            if( AllowAnyRoute ) return true;
            
            foreach( var route in Routes )
            {
                if( route.chainOriginYardId.Equals(start) && route.chainDestinationYardId.Equals(end) ) return true;
            }

            return false;
        }

        private bool ParseRoutes()
        {
            if( string.IsNullOrWhiteSpace(RouteString) || "*".Equals(RouteString) )
            {
                AllowAnyRoute = true;
                return true;
            }

            string[] routePairs = RouteString.Split(',');
            Routes = new StationsChainData[routePairs.Length];

            for( int i = 0; i < routePairs.Length; i++ )
            {
                string[] stations = routePairs[i].ToUpper().Split('-');

                if( (stations.Length == 2) &&
                    SingletonBehaviour<LogicController>.Instance.YardIdToStationController.TryGetValue(stations[0], out _) &&
                    SingletonBehaviour<LogicController>.Instance.YardIdToStationController.TryGetValue(stations[1], out _) )
                {
                    Routes[i] = new StationsChainData(stations[0], stations[1]);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public bool CheckValidity( out string message )
        {
            const StringComparison comp = StringComparison.CurrentCultureIgnoreCase;
            message = null;

            // cartype must be red, green, or blue
            if( "red".Equals(CarTypeString, comp) )
            {
                CarType = TrainCarType.PassengerRed;
            }
            else if( "green".Equals(CarTypeString, comp) )
            {
                CarType = TrainCarType.PassengerGreen;
            }
            else if( "blue".Equals(CarTypeString, comp) )
            {
                CarType = TrainCarType.PassengerBlue;
            }
            else
            {
                message = $"Invalid car type {CarType}";
                return false;
            }

            // check that a skin is given
            if( string.IsNullOrWhiteSpace(Skin) )
            {
                message = "No skin specified";
                return false;
            }

            // check that the routes are okay
            if( !ParseRoutes() )
            {
                message = "Invalid route specification";
                return false;
            }

            return true;
        }
    }
}
