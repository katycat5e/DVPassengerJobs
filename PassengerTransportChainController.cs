using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
    public class PassengerTransportChainController : JobChainController
    {
        public PassengerTransportChainController( GameObject jobChainGO ) : base(jobChainGO)
        {

        }

        protected override void OnLastJobInChainCompleted( Job lastJobInChain )
        {
            if( (jobChain.Last() is StaticPassDissasembleJobDefinition previousJob) && (previousJob.job == lastJobInChain) )
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;
                StationController currentStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[currentYardId];
                
                if( PassengerJobGenerator.LinkedGenerators.TryGetValue(currentStation, out var generator) )
                {
                    foreach( CarsPerTrack subConsist in previousJob.carsPerDestinationTrack )
                    {
                        List<TrainCar> trainCars = trainCarsForJobChain.Where(tc => subConsist.cars.Contains(tc.logicCar)).ToList();
                        generator.GenerateNewCommuterRun(new Tuple<Track, List<TrainCar>>(subConsist.track, trainCars));
                    }
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Log($"{jobChainGO?.name} ended without child job");
            }

            // handle destruction of chain
            trainCarsForJobChain.Clear();
            base.OnLastJobInChainCompleted(lastJobInChain);
        }
    }

    public class CommuterChainController : JobChainController
    {
        public CommuterChainController( GameObject jobChainGO ) : base(jobChainGO)
        {

        }

        protected override void OnLastJobInChainCompleted( Job lastJobInChain )
        {
            if( (jobChain.Last() is StaticTransportJobDefinition previousJob) && (previousJob.job == lastJobInChain) )
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;
                StationController currentStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[currentYardId];

                if( PassengerJobGenerator.LinkedGenerators.TryGetValue(currentStation, out var generator) )
                {
                    generator.GenerateNewTransportJob(new Tuple<Track, List<TrainCar>>(previousJob.destinationTrack, trainCarsForJobChain));
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Log($"{jobChainGO?.name} ended without child job");
            }

            // handle destruction of chain
            trainCarsForJobChain.Clear();
            base.OnLastJobInChainCompleted(lastJobInChain);
        }
    }

    public class PassengerChainSaveData : JobChainSaveData
    {
        public enum PassChainType
        {
            Transport, Commuter
        }

        public PassChainType ChainType;

        public PassengerChainSaveData( PassChainType type, JobChainSaveData baseData ) :
            base(baseData.jobChainData, baseData.trainCarGuids, baseData.jobTaken, baseData.currentJobTaskData, baseData.firstJobId)
        {
            ChainType = type;
        }
    }
}
