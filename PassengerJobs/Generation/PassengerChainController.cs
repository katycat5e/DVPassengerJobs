﻿using DV.Logic.Job;
using DV.ThingTypes;
using DV.Utils;
using Newtonsoft.Json;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Generation
{
    public static class PassJobType
    {
        public const JobType Express = (JobType)101;
        public const JobType Local = (JobType)102;

        public static bool IsPJType(JobType type)
        {
            return (type == Express) || (type == Local);
        }

        public static RouteType GetRouteType(this JobType type)
        {
            return type switch
            {
                Express => RouteType.Express,
                Local => RouteType.Local,
                _ => throw new System.ArgumentException("Not a passenger job type, can't get route type")
            };
        }
    }

    public class PassengerChainController : JobChainController
    {
        public PassengerChainController(GameObject jobChainGO) : base(jobChainGO)
        {
        }

        public override void OnLastJobInChainCompleted(Job lastJobInChain)
        {
            if ((jobChain.Last() is PassengerHaulJobDefinition previousJob) && (previousJob.job == lastJobInChain))
            {
                string currentYardId = previousJob.chainData.chainDestinationYardId;

                if (PassengerJobGenerator.TryGetInstance(currentYardId, out var generator))
                {
                    var newChain = generator.GenerateJob(
                        lastJobInChain.jobType,
                        new PassConsistInfo(previousJob.DestinationTracks.Last(), trainCarsForJobChain.Select(c => c.logicCar).ToList()));

                    if (newChain == null)
                    {
                        PJMain.Warning($"Failed to create new chain with cars from job {lastJobInChain.ID}");
                        SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(trainCarsForJobChain, true);
                    }

                    trainCarsForJobChain.Clear();
                }
            }
            else
            {
                PJMain.Log($"{jobChainGO?.name} ended without child job");
            }

            // handle destruction of chain
            base.OnLastJobInChainCompleted(lastJobInChain);
        }
    }

    public class PassengerChainSaveData : JobChainSaveData
    {
        [JsonConstructor]
        public PassengerChainSaveData(JobDefinitionDataBase[] jobChainData, string[] trainCarGuids, bool jobTaken, TaskSaveData[] currentJobTaskData, string firstJobId) :
            base(jobChainData, trainCarGuids, jobTaken, currentJobTaskData, firstJobId)
        {
        }

        public PassengerChainSaveData(JobChainSaveData baseData) :
            base(baseData.jobChainData, baseData.trainCarGuids, baseData.jobTaken, baseData.currentJobTaskData, baseData.firstJobId)
        {
        }
    }
}
