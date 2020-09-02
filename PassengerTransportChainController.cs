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
            if( (jobChain.Last() is StaticPassengerJobDefinition previousJob) && (previousJob.job == lastJobInChain) )
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;
                StationController currentStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[currentYardId];
                
                if( PassengerJobGenerator.LinkedGenerators.TryGetValue(currentStation, out var generator) )
                {
                    var prevJobData = new Tuple<StaticPassengerJobDefinition, List<TrainCar>>(previousJob, new List<TrainCar>(trainCarsForJobChain));
                    generator.GenerateNewTransportJob(prevJobData);
                    trainCarsForJobChain.Clear();

                    // handle destruction of chain
                    base.OnLastJobInChainCompleted(lastJobInChain);
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Log($"{jobChainGO?.name} ended without child job");
            }
        }
    }
}
