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
            if( (jobChain.Last() is StaticPassengerJobDefinition previousJob) && (previousJob.job == lastJobInChain) )
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;
                StationController currentStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[currentYardId];
                var jobConsist = new TrainCarsPerLogicTrack(previousJob.destinationTrack, trainCarsForJobChain);

                if( PassengerJobGenerator.LinkedGenerators.TryGetValue(currentStation, out var generator) )
                {
                    // outbound from city
                    if( generator.GenerateNewCommuterRun(jobConsist) == null )
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"Failed to create new chain with cars from {jobChainGO?.name}");
                        SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(trainCarsForJobChain);
                    }

                    trainCarsForJobChain.Clear();
                }
                else
                {
                    // inbound to city
                    string destYardId = previousJob.chainData.chainOriginYardId;
                    StationController townStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[destYardId];

                    if( PassengerJobGenerator.LinkedGenerators.TryGetValue(townStation, out generator) )
                    {
                        if( generator.GenerateCommuterReturnTrip(jobConsist, currentStation) == null )
                        {
                            PassengerJobs.ModEntry.Logger.Warning($"Failed to create return trip for cars from {jobChainGO?.name}");
                            SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(trainCarsForJobChain);
                        }

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
