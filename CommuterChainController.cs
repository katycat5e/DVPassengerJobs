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
                    generator.GenerateNewTransportJob(new List<TrainCarsPerLogicTrack>() { jobConsist });
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

    [HarmonyPatch(typeof(JobChainController), "OnJobGenerated")]
    static class CCC_OnJobGenerated_Patch
    {
        static void Postfix( StaticJobDefinition jobDefinition, Job generatedJob, CommuterChainController __instance, List<StaticJobDefinition> ___jobChain )
        {
            if( (generatedJob.jobType == PassJobType.Commuter) && (jobDefinition == ___jobChain.LastOrDefault()) )
            {
                PassengerJobGenerator.CommuterJobDict[generatedJob] = __instance;
            }
        }
    }

    [HarmonyPatch(typeof(JobChainController), "OnJobCompleted")]
    static class CCC_OnJobCompleted_Patch
    {
        static void Postfix( Job completedJob )
        {
            if( completedJob.jobType == PassJobType.Commuter ) PassengerJobGenerator.CommuterJobDict.Remove(completedJob);
        }
    }

    [HarmonyPatch(typeof(JobChainController), "OnAnyJobFromChainAbandoned")]
    static class CCC_OnAnyJobAbandoned_Patch
    {
        static void Postfix( Job abandonedJob )
        {
            if( abandonedJob.jobType == PassJobType.Commuter ) PassengerJobGenerator.CommuterJobDict.Remove(abandonedJob);
        }
    }
}
