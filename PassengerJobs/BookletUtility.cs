using DV.Booklets;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BookletUtility
    {
        public static PassengerJobData ExtractPassengerJobData(Job_data job)
        {
            Task_data? sequence = job.tasksData.FirstOrDefault();
            if ((sequence?.type != TaskType.Sequential) || (sequence.nestedTasks.Length < 1))
            {
                throw new Exception($"Wrong format of Passenger Job. Job id: {job.ID}");
            }

            var startTask = sequence.nestedTasks.First();
            var transportTasks = sequence.nestedTasks.Where(t => t.instanceTaskType == TaskType.Transport);
            var destTracks = transportTasks.Select(t => t.destinationTrackID).ToArray();
            return new PassengerJobData(job, startTask.startTrackID, destTracks, startTask.cars);
        }

        public static TemplatePaperData CreateCoverPage(PassengerJobData jobData, int pageNum, int totalPages)
        {
            string coverText = LocalizationKey.JOB_EXPRESS_COVER.L();
            return new CoverPageTemplatePaperData(jobData.job.ID.ToString(), coverText, pageNum.ToString(), totalPages.ToString());
        }

        public static TemplatePaperData CreatePassengerOverviewPage(PassengerJobData jobData, int pageNum = 0, int totalPages = 0)
        {
            string jobTitle = LocalizationKey.JOB_EXPRESS_NAME.L();
            string trainLength = $"{C.GetCarsTotalLength(jobData.transportingCars):F} m";
            string trainMass = $"{C.GetCarsTotalMass(jobData.transportingCars, jobData.transportedCargoPerCar) * 0.001f:F} t";
            string trainValue = $"${C.GetTrainValue(jobData.transportingCars, jobData.transportedCargoPerCar) / 1_000_000:F}m";
            
            string bonusTime = $"{Mathf.FloorToInt(jobData.job.timeLimit / 60f)} min";
            float payment = jobData.job.basePayment;
            float bonusPayment = Mathf.Round(PassengerJobGenerator.GetBonusPayment(payment) / 1_000);

            string destYard = jobData.job.chainDestinationStationInfo.YardID;
            var viaStations = jobData.destinationStations.Take(jobData.destinationStations.Length - 1);

            string description;
            if (viaStations.Any())
            {
                string viaYards = string.Join(", ", viaStations.Select(s => s.YardID));
                description = LocalizationKey.JOB_EXPRESS_DESCRIPTION.L(destYard, viaYards, bonusPayment.ToString());
            }
            else
            {
                description = LocalizationKey.JOB_EXPRESS_DIRECT_DESC.L(destYard, bonusPayment.ToString());
            }

            var startStation = jobData.job.chainOriginStationInfo;
            var endStation = jobData.job.chainDestinationStationInfo;

            return new FrontPageTemplatePaperData(
                jobTitle, string.Empty, jobData.job.ID, LicenseData.Color,
                description, jobData.job.requiredLicenses,
                new() { CargoInjector.PassengerCargo.v1 }, jobData.transportedCargoPerCar,
                string.Empty, string.Empty, TemplatePaperData.NOT_USED_COLOR,
                LocalizationAPI.L(startStation.LocalizationKey), startStation.Type, startStation.StationColor,
                LocalizationAPI.L(endStation.LocalizationKey), endStation.Type, endStation.StationColor,
                jobData.transportingCars,
                trainLength, trainMass, trainValue, bonusTime, payment.ToString(),
                (pageNum > 0) ? pageNum.ToString() : string.Empty,
                (totalPages > 0) ? totalPages.ToString() : string.Empty
            );
        }

        public static TemplatePaperData CreateCoupleTaskPage(PassengerJobData jobData, int taskNum, int pageNum, int totalPages)
        {
            var station = jobData.job.chainOriginStationInfo;
            return BookletCreator_Job.CreateCoupleTaskPaperData(
                taskNum, station.YardID, station.StationColor, jobData.startingTrack.TrackPartOnly,
                jobData.transportingCars, jobData.transportedCargoPerCar, pageNum, totalPages);
        }

        public static TemplatePaperData CreateHaulTaskPage(PassengerJobData jobData, StationInfo destStation, int taskNum, int pageNum, int totalPages)
        {
            string taskType = LocalizationAPI.L("job/task_type_haul");
            string taskDescription = LocalizationAPI.L("job/task_desc_haul");
            string destStationName = LocalizationAPI.L(destStation.LocalizationKey);

            return new TaskTemplatePaperData(
                taskNum.ToString(), taskType, taskDescription,
                string.Empty, TemplatePaperData.NOT_USED_COLOR, string.Empty, TemplatePaperData.NOT_USED_COLOR,
                destStationName, destStation.Type, destStation.StationColor,
                jobData.transportingCars, jobData.transportedCargoPerCar,
                pageNum.ToString(), totalPages.ToString());
        }

        public static TemplatePaperData CreateLoadTaskPage(PassengerJobData jobData, StationInfo station, TrackID track, int taskNum, int pageNum, int totalPages)
        {
            string taskType = LocalizationAPI.L("job/task_type_load");
            string taskDescription = LocalizationAPI.L("job/task_desc_load");

            return new TaskTemplatePaperData(
                taskNum.ToString(), taskType, taskDescription,
                station.YardID, station.StationColor, track.TrackPartOnly, C.TRACK_COLOR,
                string.Empty, string.Empty, TemplatePaperData.NOT_USED_COLOR,
                jobData.transportingCars, jobData.transportedCargoPerCar,
                pageNum.ToString(), totalPages.ToString());
        }

        public static TemplatePaperData CreateUncoupleTaskPage(PassengerJobData jobData, int taskNum, int pageNum, int totalPages)
        {
            var station = jobData.job.chainDestinationStationInfo;
            return BookletCreator_Job.CreateUncoupleTaskPaperData(
                taskNum, station.YardID, station.StationColor, jobData.destinationTrack.TrackPartOnly,
                jobData.transportingCars, jobData.transportedCargoPerCar, pageNum, totalPages);
        }

        public static IEnumerable<TemplatePaperData> CreateExpressJobBooklet(PassengerJobData jobData)
        {
            const int BASE_PAGES = 4;
            const int PAGES_PER_DESTINATION = 2;

            int totalPages = BASE_PAGES + (jobData.destinationStations.Length * PAGES_PER_DESTINATION);

            int pageNum = 1;
            yield return CreateCoverPage(jobData, pageNum++, totalPages);
            yield return CreatePassengerOverviewPage(jobData, pageNum++, totalPages);

            int taskNum = 1;
            yield return CreateCoupleTaskPage(jobData, taskNum++, pageNum++, totalPages);
            
            for (int i = 0; i < jobData.destinationStations.Length; i++)
            {
                yield return CreateHaulTaskPage(jobData, jobData.destinationStations[i], taskNum++, pageNum++, totalPages);

                if (i == jobData.destinationStations.Length - 1)
                {
                    yield return CreateUncoupleTaskPage(jobData, taskNum++, pageNum++, totalPages);
                }
                else
                {
                    yield return CreateLoadTaskPage(jobData, jobData.destinationStations[i], jobData.destinationTrackIds[i], taskNum++, pageNum++, totalPages);
                }
            }

            yield return BookletCreator_Job.CreateValidateJobTaskPaperData(taskNum, pageNum, totalPages);
        }
    }

    internal class PassengerJobData : TransportJobData
    {
        public readonly TrackID[] destinationTrackIds;
        public readonly StationInfo[] destinationStations;

        public PassengerJobData(Job_data job, TrackID startingTrack, TrackID[] destinationTracks, List<Car_data> transportingCars) 
            : base(job, startingTrack, destinationTracks.Last(), transportingCars, 
                  Enumerable.Repeat(CargoInjector.PassengerCargo.v1, transportingCars.Count).ToList())
        {
            destinationStations = destinationTracks.Select(t => StationController.GetStationByYardID(t.yardId).stationInfo).ToArray();
            destinationTrackIds = destinationTracks;
        }
    }
}
