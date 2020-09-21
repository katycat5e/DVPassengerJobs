using DV.Logic.Job;
using Newtonsoft.Json;
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
            if( (jobChain.Last() is StaticPassengerJobDefinition previousJob) && (previousJob.job == lastJobInChain) )
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;
                StationController currentStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[currentYardId];
                
                if( PassengerJobGenerator.LinkedGenerators.TryGetValue(currentStation, out var generator) )
                {
                    if( generator.GenerateNewTransportJob(new TrainCarsPerLogicTrack(previousJob.destinationTrack, trainCarsForJobChain)) == null )
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"Failed to create new chain with cars from job {lastJobInChain.ID}");
                        SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(trainCarsForJobChain, true);
                    }

                    trainCarsForJobChain.Clear();
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Log($"{jobChainGO?.name} ended without child job");
            }

            // handle destruction of chain
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

        [JsonConstructor]
        public PassengerChainSaveData( PassChainType type, JobDefinitionDataBase[] jobChainData, string[] trainCarGuids, bool jobTaken, TaskSaveData[] currentJobTaskData, string firstJobId ) :
            base(jobChainData, trainCarGuids, jobTaken, currentJobTaskData, firstJobId)
        {
            ChainType = type;
        }

        public PassengerChainSaveData( PassChainType type, JobChainSaveData baseData ) :
            base(baseData.jobChainData, baseData.trainCarGuids, baseData.jobTaken, baseData.currentJobTaskData, baseData.firstJobId)
        {
            ChainType = type;
        }
    }
}
