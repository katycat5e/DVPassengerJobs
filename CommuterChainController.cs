using DV.Logic.Job;
using Harmony12;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
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
                    var jobConsist = new TrainCarsPerLogicTrack(previousJob.destinationTrack, trainCarsForJobChain);
                    if( generator.GenerateNewCommuterRun(jobConsist) == null )
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"Failed to create new chain with cars from {jobChainGO?.name}");
                    }
                    else
                    {
                        trainCarsForJobChain.Clear();
                    }
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
}
