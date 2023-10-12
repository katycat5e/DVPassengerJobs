using DV.Booklets;
using DV.Localization;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs
{
    internal static class BookletUtility
    {
        private static readonly Color _expressColor = LicenseData.Color;
        private static readonly Color _localColor = new Color32(147, 112, 219, 255);

        public static PassengerJobData ExtractPassengerJobData(Job_data job)
        {
            Task_data? sequence = job.tasksData.FirstOrDefault();
            if ((sequence?.type != TaskType.Sequential) || (sequence.nestedTasks.Length < 1))
            {
                throw new Exception($"Wrong format of Passenger Job. Job id: {job.ID}");
            }

            var destinations = new List<PassStopInfo>();
            TrackID? startTrackId = null;
            TrackID? endTrackId = null;
            List<Car_data>? cars = null;

            foreach (var task in sequence.nestedTasks)
            {
                if ((task is RuralTask_data ruralTask) && !ruralTask.isLoading)
                {
                    destinations.Add(new PassStopInfo(ruralTask.stationId));
                }
                else if ((task.instanceTaskType == TaskType.Warehouse) && (task.warehouseTaskType == WarehouseTaskType.Unloading))
                {
                    var stationInfo = StationController.GetStationByYardID(task.destinationTrackID.yardId).stationInfo;
                    destinations.Add(new PassStopInfo(task.destinationTrackID, stationInfo));
                    endTrackId = task.destinationTrackID;

                    startTrackId ??= task.destinationTrackID;
                    cars ??= task.cars;
                }
            }

            return new PassengerJobData(job, startTrackId!, endTrackId!, destinations.ToArray(), cars!);
        }

        public static TemplatePaperData CreateCoverPage(PassengerJobData jobData, int pageNum, int totalPages)
        {
            string coverText = LocalizationKey.JOB_EXPRESS_COVER.L();
            return new CoverPageTemplatePaperData(jobData.job.ID.ToString(), coverText, pageNum.ToString(), totalPages.ToString());
        }

        public static TemplatePaperData CreatePassengerOverviewPage(PassengerJobData jobData, int pageNum = 0, int totalPages = 0)
        {
            Color jobColor;
            string jobTitle;
            string description;

            string destYard = jobData.job.chainDestinationStationInfo.YardID;
            var viaStations = jobData.destinationStops.Take(jobData.destinationStops.Length - 1);

            string bonusTime = $"{Mathf.FloorToInt(jobData.job.timeLimit / 60f)} min";
            float payment = jobData.job.basePayment;
            float bonusPayment = Mathf.Round(PassengerJobGenerator.GetBonusPayment(payment) / 1_000);

            if (jobData.job.type == PassJobType.Express)
            {
                jobColor = _expressColor;
                jobTitle = LocalizationKey.JOB_EXPRESS_NAME.L();

                if (viaStations.Any())
                {
                    string viaYards = string.Join(", ", viaStations.Select(s => s.YardID));
                    description = LocalizationKey.JOB_EXPRESS_DESCRIPTION.L(destYard, viaYards, bonusPayment.ToString());
                }
                else
                {
                    description = LocalizationKey.JOB_EXPRESS_DIRECT_DESC.L(destYard, bonusPayment.ToString());
                }
            }
            else
            {
                jobColor = _localColor;
                jobTitle = LocalizationKey.JOB_LOCAL_NAME.L();

                string viaYards = string.Join(", ", viaStations.Select(s => s.YardID));
                description = LocalizationKey.JOB_LOCAL_DESCRIPTION.L(destYard, viaYards, bonusPayment.ToString());
            }
            
            string trainLength = $"{C.GetCarsTotalLength(jobData.transportingCars):F} m";
            string trainMass = $"{C.GetCarsTotalMass(jobData.transportingCars, jobData.transportedCargoPerCar) * 0.001f:F} t";
            string trainValue = $"${C.GetTrainValue(jobData.transportingCars, jobData.transportedCargoPerCar) / 1_000_000:F}m";

            var startStation = jobData.job.chainOriginStationInfo;
            var endStation = jobData.job.chainDestinationStationInfo;

            return new FrontPageTemplatePaperData(
                jobTitle, string.Empty, jobData.job.ID, jobColor,
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

        //public static TemplatePaperData CreateHaulTaskPage(PassengerJobData jobData, StationInfo destStation, int taskNum, int pageNum, int totalPages)
        //{
        //    string taskType = LocalizationAPI.L("job/task_type_haul");
        //    string taskDescription = LocalizationAPI.L("job/task_desc_haul");
        //    string destStationName = LocalizationAPI.L(destStation.LocalizationKey);

        //    return new TaskTemplatePaperData(
        //        taskNum.ToString(), taskType, taskDescription,
        //        string.Empty, TemplatePaperData.NOT_USED_COLOR, string.Empty, TemplatePaperData.NOT_USED_COLOR,
        //        destStationName, destStation.Type, destStation.StationColor,
        //        jobData.transportingCars, jobData.transportedCargoPerCar,
        //        pageNum.ToString(), totalPages.ToString());
        //}

        public static TemplatePaperData CreateLoadTaskPage(PassengerJobData jobData, PassStopInfo stopInfo, int taskNum, int pageNum, int totalPages)
        {
            string taskType = LocalizationAPI.L("job/task_type_load");
            string taskDescription = LocalizationAPI.L("job/task_desc_load");

            return new TaskTemplatePaperData(
                taskNum.ToString(), taskType, taskDescription,
                stopInfo.YardID, stopInfo.StationColor, stopInfo.TrackDisplayId, C.TRACK_COLOR,
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
            const int PAGES_PER_DESTINATION = 1;

            int totalPages = BASE_PAGES + (jobData.destinationStops.Length * PAGES_PER_DESTINATION);

            int pageNum = 1;
            yield return CreateCoverPage(jobData, pageNum++, totalPages);
            yield return CreatePassengerOverviewPage(jobData, pageNum++, totalPages);

            int taskNum = 1;
            yield return CreateCoupleTaskPage(jobData, taskNum++, pageNum++, totalPages);
            
            for (int i = 0; i < jobData.destinationStops.Length; i++)
            {
                //yield return CreateHaulTaskPage(jobData, jobData.destinationStations[i], taskNum++, pageNum++, totalPages);

                if (i == jobData.destinationStops.Length - 1)
                {
                    yield return CreateUncoupleTaskPage(jobData, taskNum++, pageNum++, totalPages);
                }
                else
                {
                    yield return CreateLoadTaskPage(jobData, jobData.destinationStops[i], taskNum++, pageNum++, totalPages);
                }
            }

            yield return BookletCreator_Job.CreateValidateJobTaskPaperData(taskNum, pageNum, totalPages);
        }
    }

    internal class PassengerJobData : TransportJobData
    {
        public readonly PassStopInfo[] destinationStops;

        public PassengerJobData(Job_data job, TrackID startingTrack, TrackID destinationTrack, PassStopInfo[] viaStops, List<Car_data> transportingCars) 
            : base(job, startingTrack, destinationTrack, transportingCars, 
                  Enumerable.Repeat(CargoInjector.PassengerCargo.v1, transportingCars.Count).ToList())
        {
            destinationStops = viaStops;
        }

        
    }

    internal class PassStopInfo
    {
        public readonly string YardID;
        public readonly Color StationColor;
        public readonly string TrackDisplayId;

        public PassStopInfo(TrackID trackId, StationInfo station)
        {
            YardID = station.YardID;
            StationColor = station.StationColor;
            TrackDisplayId = trackId.TrackPartOnly;
        }

        public PassStopInfo(string stopID)
        {
            YardID = stopID;
            StationColor = LicenseData.Color;
            TrackDisplayId = "1LP";
        }
    }
}
