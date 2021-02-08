using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DV.RenderTextureSystem.BookletRender;
using DV.Logic.Job;
using UnityEngine.UI;
using UnityEngine;
using System.Reflection;

namespace PassengerJobsMod
{
    // BookletCreator.GetJobLicenseTemplateData
    [HarmonyPatch(typeof(BookletCreator), "GetJobLicenseTemplateData")]
    class BC_GetLicenseTemplate_Patch
    {
        static bool Prefix( JobLicenses jobLicense, ref LicenseTemplatePaperData __result )
        {
            if( jobLicense == PassLicenses.Passengers1 )
            {
                // override the BookletCreator method
                __result = PassengerLicenseUtil.GetPassengerLicenseTemplate();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BookletCreator), "GetMissingLicenseTemplateData")]
    static class BC_GetMissingLicenseData_Patch
    {
        static bool Prefix( Job job, ref List<TemplatePaperData> __result )
        {
            if( (job.jobType == PassJobType.Commuter) || (job.jobType == PassJobType.Express) )
            {
                var licensePrintData = new MissingLicensesPageTemplatePaperData.LicensePrintData(
                    PassengerLicenseUtil.PASS1_LICENSE_NAME, PassengerLicenseUtil.Pass1Sprite, false);

                var printDataList = new List<MissingLicensesPageTemplatePaperData.LicensePrintData>() { licensePrintData };

                const string JOB_TYPE = "PASSENGER";
                string subType = (job.jobType == PassJobType.Commuter) ? "COMMUTER" : "EXPRESS";

                var paperData = new MissingLicensesPageTemplatePaperData(
                    JOB_TYPE, subType, job.ID, PassBookletUtil.PASS_JOB_COLOR, printDataList);

                __result = new List<TemplatePaperData>() { paperData };

                return false;
            }

            return true;
        }
    }

    // FrontPageTemplatePaper.DisplayRequiredLicenses
    [HarmonyPatch(typeof(FrontPageTemplatePaper), "DisplayRequiredLicenses")]
    class FPTP_DisplayLicenses_Patch
    {
        static void Postfix( JobLicenses requiredLicenses, Image[] ___requiredLicenseSlots )
        {
            if( requiredLicenses.HasFlag(PassLicenses.Passengers1) )
            {
                // get first non-active slot
                Image slot = ___requiredLicenseSlots.FirstOrDefault(img => !img.gameObject.activeSelf);
                if( slot == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Can't fit Passengers 1 license on job overview");
                    return;
                }

                if( PassengerLicenseUtil.Pass1Sprite == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Missing icon for {PassengerLicenseUtil.PASS1_LICENSE_NAME}");
                    return;
                }

                slot.sprite = PassengerLicenseUtil.Pass1Sprite;
                slot.gameObject.SetActive(true);
            }
        }
    }

    // BookletCreator.CreateLicense()
    [HarmonyPatch(typeof(BookletCreator), nameof(BookletCreator.CreateLicense))]
    [HarmonyPatch(new Type[] { typeof(JobLicenses), typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    static class BC_CreateLicense_Patch
    {
        public const string COPIED_PREFAB_NAME = "LicenseHazmat1";
        private static readonly MethodInfo spawnLicenseMethod = AccessTools.Method(typeof(BookletCreator), "SpawnLicenseRelatedPrefab");

        static bool Prefix( JobLicenses license, Vector3 position, Quaternion rotation, Transform parent )
        {
            if( license != PassLicenses.Passengers1 ) return true;

            // we'll try to copy from the Hazmat 1 license prefab
            GameObject licenseObj = spawnLicenseMethod.Invoke(null,
                new object[] { COPIED_PREFAB_NAME, position, rotation, true, parent }) as GameObject;

            PassengerLicenseUtil.SetLicenseObjectProperties(licenseObj, PassBookletType.Passengers1License);

            PassengerJobs.ModEntry.Logger.Log("Created Passengers 1 license");
            return false;
        }
    }

    // BookletCreator.CreateLicenseInfo()
    [HarmonyPatch(typeof(BookletCreator), nameof(BookletCreator.CreateLicenseInfo))]
    [HarmonyPatch(new Type[] { typeof(JobLicenses), typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    static class BC_CreateLicenseInfo_Patch
    {
        public const string COPIED_PREFAB_NAME = "LicenseHazmat1Info";
        private static readonly MethodInfo spawnLicenseMethod = AccessTools.Method(typeof(BookletCreator), "SpawnLicenseRelatedPrefab");

        static bool Prefix( JobLicenses license, Vector3 position, Quaternion rotation, Transform parent )
        {
            if( license != PassLicenses.Passengers1 ) return true;

            // we'll try to copy the Hazmat 1 info prefab
            GameObject infoObj = spawnLicenseMethod.Invoke(null,
                new object[] { COPIED_PREFAB_NAME, position, rotation, false, parent }) as GameObject;

            PassengerLicenseUtil.SetLicenseObjectProperties(infoObj, PassBookletType.Passengers1Info);

            PassengerJobs.ModEntry.Logger.Log("Created Passengers 1 info page");
            return false;
        }
    }

    public static class PassBookletUtil
    {
        public const string EXPRESS_JOB_TITLE = "EXPRESS PAX";
        public const string COMMUTE_JOB_TITLE = "BRANCH PAX";
        public static readonly Color PASS_JOB_COLOR = new Color32(91, 148, 190, 255);

        public static float GetLoadedConsistMass( List<Car> cars )
        {
            float cargoMass = CargoTypes.GetCargoUnitMass(CargoType.Passengers);
            float totalMass = cars.Sum(c => c.carOnlyMass);
            totalMass += cars.Sum(c => c.capacity * cargoMass);
            return totalMass;
        }

        public static float GetTrainValue( List<Car> cars )
        {
            float totalValue = cars.Sum(c => ResourceTypes.GetFullDamagePriceForCar(c.carType));
            totalValue += cars.Count * ResourceTypes.GetFullDamagePriceForCargo(CargoType.Passengers);
            // totalValue += cars.Count * ResourceTypes.GetFullEnvironmentDamagePriceForCargo(CargoType.Passengers);
            return totalValue;
        }

        public static List<Tuple<TrainCarType, string>> GetCarsInfo( List<Car> cars )
        {
            return cars.Select(c => new Tuple<TrainCarType, string>(c.carType, c.ID)).ToList();
        }

        public delegate StationInfo ExtractStationDelegate( string id );
        public static readonly ExtractStationDelegate ExtractStationFromId =
            AccessTools.Method(typeof(BookletCreator), "ExtractStationInfoWithYardID")?.CreateDelegate(typeof(ExtractStationDelegate)) as ExtractStationDelegate;

        public delegate TaskTemplatePaperData CreateTaskDataDelegate(
            int step, string yardID, Color yardColor, string trackId, List<Tuple<TrainCarType, string>> carsInfo, List<CargoType> cargoTypePerCar, int pageNum, int totalPages );

        public static readonly CreateTaskDataDelegate CreateCoupleTaskData =
            AccessTools.Method("BookletCreator:CreateCoupleTaskPaperData")?.CreateDelegate(typeof(CreateTaskDataDelegate)) as CreateTaskDataDelegate;
        public static readonly CreateTaskDataDelegate CreateUncoupleTaskData =
            AccessTools.Method("BookletCreator:CreateUncoupleTaskPaperData")?.CreateDelegate(typeof(CreateTaskDataDelegate)) as CreateTaskDataDelegate;

        public delegate string GetShuntingInfoDelegate( int nTracks );

        public static readonly GetShuntingInfoDelegate GetShuntingPickupsText =
            AccessTools.Method("BookletCreator:GetShuntingPickUpsText")?.CreateDelegate(typeof(GetShuntingInfoDelegate)) as GetShuntingInfoDelegate;
        public static readonly GetShuntingInfoDelegate GetShuntingDropOffsText =
            AccessTools.Method("BookletCreator:GetShuntingDropOffsText")?.CreateDelegate(typeof(GetShuntingInfoDelegate)) as GetShuntingInfoDelegate;

        public static TemplatePaperData CreateTransportDescriptionData( Job job, string trainName, List<Car> jobCars, List<Tuple<TrainCarType, string>> carsInfo, int pageNum = 0, int totalPages = 0 )
        {
            string jobTitle = (job.jobType == PassJobType.Express) ? EXPRESS_JOB_TITLE : COMMUTE_JOB_TITLE;

            string description;
            if( job.jobType == PassJobType.Express )
            {
                description = $"Transport {trainName} to {job.chainData.chainDestinationYardId}";

                if( PassengerJobs.Settings.UseCustomWages )
                {
                    float bonusAmount = Mathf.Round(PassengerJobGenerator.BONUS_TO_BASE_WAGE_RATIO * job.GetBasePaymentForTheJob() / 1000f);
                    description += $" (${bonusAmount}k bonus with on-time completion)";
                }
            }
            else description = $"Transport a commuter train to {job.chainData.chainDestinationYardId}";

            string trainLength = jobCars.Sum(c => c.length).ToString("F") + " m";
            string trainMass = (GetLoadedConsistMass(jobCars) * 0.001f).ToString("F") + " t";
            string trainValue = "$" + (GetTrainValue(jobCars) / 1000000f).ToString("F") + "m";
            string timeLimit = (job.TimeLimit > 0) ? (Mathf.FloorToInt(job.TimeLimit / 60f) + " min") : "No bonus";
            string payment = job.GetBasePaymentForTheJob().ToString();

            StationInfo startStation = ExtractStationFromId(job.chainData.chainOriginYardId);
            StationInfo endStation = ExtractStationFromId(job.chainData.chainDestinationYardId);

            return new FrontPageTemplatePaperData(
                jobTitle, "", job.ID, PASS_JOB_COLOR, description,
                job.requiredLicenses, new List<CargoType>() { CargoType.Passengers }, 
                Enumerable.Repeat(CargoType.Passengers, jobCars.Count).ToList(),
                "", "", TemplatePaperData.NOT_USED_COLOR,
                startStation.Name, startStation.Type, startStation.StationColor,
                endStation.Name, endStation.Type, endStation.StationColor,
                carsInfo, trainLength, trainMass, trainValue, timeLimit, payment,
                (pageNum > 0) ? pageNum.ToString() : "",
                (totalPages > 0) ? totalPages.ToString() : "");
        }
    }

    // BookletCreator.GetBookletTemplateData
    [HarmonyPatch(typeof(BookletCreator), "GetBookletTemplateData")]
    static class BC_GetBookletTemplateData_Patch
    {
        static Color? _TRACK_COLOR = null;
        static Color TRACK_COLOR
        {
            get
            {
                if( !_TRACK_COLOR.HasValue )
                {
                    _TRACK_COLOR = AccessTools.Field(typeof(BookletCreator), "TRACK_COLOR")?.GetValue(null) as Color?;
                    if( !_TRACK_COLOR.HasValue )
                    {
                        PassengerJobs.ModEntry.Logger.Error("Failed to get track color from BookletCreator");
                        return Color.white;
                    }
                }
                return _TRACK_COLOR.Value;
            }
        }

        static bool Prefix( Job job, ref List<TemplatePaperData> __result )
        {
            bool borked = 
                (PassBookletUtil.ExtractStationFromId == null) ||
                (PassBookletUtil.CreateCoupleTaskData == null) ||
                (PassBookletUtil.CreateUncoupleTaskData == null) ||
                (PassBookletUtil.GetShuntingPickupsText == null) ||
                (PassBookletUtil.GetShuntingDropOffsText == null);

            Func<Job, List<TemplatePaperData>> bookletMethod;

            switch( job.jobType )
            {
                case PassJobType.Express:
                    bookletMethod = GetTransportBookletData;
                    break;

                case PassJobType.Commuter:
                    bookletMethod = GetCommuterBookletData;
                    break;

                default:
                    // not a job type we handle
                    return true;
            }

            if( borked )
            {
                __result = null;
                PassengerJobs.ModEntry.Logger.Error("Couldn't connect to BookletCreator methods!");
            }
            else
            {
                __result = bookletMethod(job);
            }

            return false;
        }

        static List<TemplatePaperData> GetTransportBookletData( Job job )
        {
            var pages = new List<TemplatePaperData>();
            int pageNum = 1;
            int totalPages = 6;

            TaskData superTask = job.GetJobData().FirstOrDefault();
            if( superTask == null ) return pages;

            TaskData stageTask = null;
            TaskData loadTask = null;
            TaskData transportTask = null;
            TaskData unloadTask = null;
            TaskData storeTask = null;

            if( superTask.type == TaskType.Sequential )
            {
                TaskData[] tasks = superTask.nestedTasks.Select(t => t.GetTaskData()).ToArray();
                loadTask = tasks.FirstOrDefault(td => td.warehouseTaskType == WarehouseTaskType.Loading);
                unloadTask = tasks.FirstOrDefault(td => td.warehouseTaskType == WarehouseTaskType.Unloading);

                int offset = 0;
                if( loadTask != null )
                {
                    stageTask = tasks[0];
                    offset = 2;
                    totalPages += 1; // add load page
                }

                transportTask = tasks[offset];

                if( unloadTask != null )
                {
                    storeTask = tasks[2 + offset];
                    totalPages += 1; // add unload page
                }
            }
            else if( superTask.type == TaskType.Transport )
            {
                transportTask = superTask;
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning($"Invalid tasks in job {job.ID}");
                return pages;
            }

            // check for speciality
            string coverTitle;
            string trainName;
            if( SpecialConsistManager.JobToSpecialMap.TryGetValue(job.ID, out SpecialTrain special) )
            {
                coverTitle = special.Name.ToUpper();

                int lastDashIdx = job.ID.LastIndexOf('-');
                string trainNum = job.ID.Substring(lastDashIdx + 1);
                trainName = $"{special.Name} #{trainNum}";
            }
            else
            {
                coverTitle = PassBookletUtil.EXPRESS_JOB_TITLE;
                trainName = "a regional express train";
            }

            List<Tuple<TrainCarType, string>> carsInfo;
            carsInfo = PassBookletUtil.GetCarsInfo(transportTask.cars);

            // Cover page
            var coverPage = new CoverPageTemplatePaperData(job.ID, coverTitle, pageNum.ToString(), totalPages.ToString());
            pages.Add(coverPage);
            pageNum += 1;

            // Job description page
            StationInfo startStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainOriginYardId);
            StationInfo endStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainDestinationYardId);

            var descriptionPage = PassBookletUtil.CreateTransportDescriptionData(job, trainName, transportTask.cars, carsInfo, pageNum, totalPages);
            pages.Add(descriptionPage);
            pageNum += 1;

            // Task pages
            int taskNum = 1;

            // initial coupling
            Track pickupTrack = stageTask?.startTrack ?? transportTask.startTrack;

            var couplePage = PassBookletUtil.CreateCoupleTaskData(
                taskNum, startStation.YardID, startStation.StationColor, pickupTrack.ID.TrackPartOnly,
                carsInfo, null, pageNum, totalPages);

            pages.Add(couplePage);
            pageNum += 1;
            taskNum += 1;

            // passenger loading
            if( loadTask != null )
            {
                const string LOAD_TASK_TYPE = "LOAD";
                const string LOAD_TASK_DESC = "Load train at station platform:";

                string platformName = loadTask.destinationTrack.ID.TrackPartOnly;

                var loadPage = new TaskTemplatePaperData(
                    taskNum.ToString(), LOAD_TASK_TYPE, LOAD_TASK_DESC,
                    startStation.YardID, startStation.StationColor, platformName, TRACK_COLOR,
                    "", "", TemplatePaperData.NOT_USED_COLOR,
                    carsInfo, null, pageNum.ToString(), totalPages.ToString());

                pages.Add(loadPage);
                pageNum += 1;
                taskNum += 1;
            }

            // transport leg
            var destYard = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[job.chainData.chainDestinationYardId];
            string destTrackName = transportTask.destinationTrack.ID.TrackPartOnly;

            const string HAUL_TASK_TYPE = "HAUL";
            const string HAUL_TASK_DESC = "Haul train to the following station/track:";

            var taskPage = new TaskTemplatePaperData(
                taskNum.ToString(), HAUL_TASK_TYPE, HAUL_TASK_DESC,
                destYard.stationInfo.YardID, destYard.stationInfo.StationColor, destTrackName, TRACK_COLOR,
                "", "", TemplatePaperData.NOT_USED_COLOR,
                carsInfo, null,
                pageNum.ToString(), totalPages.ToString());

            pages.Add(taskPage);
            pageNum += 1;
            taskNum += 1;

            // unload at destination
            if( unloadTask != null )
            {
                const string UNLOAD_TASK_TYPE = "UNLOAD";
                const string UNLOAD_TASK_DESC = "Unload train at station platform:";

                string platformName = unloadTask.destinationTrack.ID.TrackPartOnly;

                var unloadPage = new TaskTemplatePaperData(
                    taskNum.ToString(), UNLOAD_TASK_TYPE, UNLOAD_TASK_DESC,
                    endStation.YardID, endStation.StationColor, platformName, TRACK_COLOR,
                    "", "", TemplatePaperData.NOT_USED_COLOR,
                    carsInfo, null, pageNum.ToString(), totalPages.ToString());

                pages.Add(unloadPage);
                pageNum += 1;
                taskNum += 1;
            }

            // final uncoupling
            Track dropoffTrack = storeTask?.destinationTrack ?? transportTask.destinationTrack;

            var uncouplePage = PassBookletUtil.CreateUncoupleTaskData(
                taskNum, endStation.YardID, endStation.StationColor, dropoffTrack.ID.TrackPartOnly,
                carsInfo, null,
                pageNum, totalPages);

            pages.Add(uncouplePage);
            pageNum += 1;
            taskNum += 1;

            pages.Add(new ValidateJobTaskTemplatePaperData(taskNum.ToString(), pageNum.ToString(), totalPages.ToString()));

            return pages;
        }

        static List<TemplatePaperData> GetCommuterBookletData( Job job )
        {
            var pages = new List<TemplatePaperData>();
            int pageNum = 1;
            int totalPages = 6;

            TaskData superTask = job.GetJobData().FirstOrDefault();
            if( superTask == null ) return pages;

            TaskData stageTask = null;
            TaskData loadTask = null;
            TaskData transportTask = null;
            TaskData unloadTask = null;
            TaskData storeTask = null;

            if( superTask.type == TaskType.Sequential )
            {
                TaskData[] tasks = superTask.nestedTasks.Select(t => t.GetTaskData()).ToArray();
                loadTask = tasks.FirstOrDefault(td => td.warehouseTaskType == WarehouseTaskType.Loading);
                unloadTask = tasks.FirstOrDefault(td => td.warehouseTaskType == WarehouseTaskType.Unloading);

                int offset = 0;
                if( loadTask != null )
                {
                    stageTask = tasks[0];
                    offset = 2;
                    totalPages += 1; // add load page
                }

                transportTask = tasks[offset];

                if( unloadTask != null )
                {
                    storeTask = tasks[2 + offset];
                    totalPages += 1; // add unload page
                }
            }
            else if( superTask.type == TaskType.Transport )
            {
                transportTask = superTask;
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning($"Invalid tasks in job {job.ID}");
                return pages;
            }

            var carsInfo = PassBookletUtil.GetCarsInfo(transportTask.cars);

            StationInfo startStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainOriginYardId);
            StationInfo endStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainDestinationYardId);

            // Cover page
            var coverPage = new CoverPageTemplatePaperData(job.ID, PassBookletUtil.COMMUTE_JOB_TITLE, pageNum.ToString(), totalPages.ToString());
            pages.Add(coverPage);
            pageNum += 1;

            // Description page
            var descriptionPage = PassBookletUtil.CreateTransportDescriptionData(job, PassBookletUtil.COMMUTE_JOB_TITLE, transportTask.cars, carsInfo, pageNum, totalPages);
            pages.Add(descriptionPage);
            pageNum += 1;

            // Task pages
            int taskNum = 1;

            // initial coupling
            Track pickupTrack = stageTask?.startTrack ?? transportTask.startTrack;

            var couplePage = PassBookletUtil.CreateCoupleTaskData(
                taskNum, startStation.YardID, startStation.StationColor, pickupTrack.ID.TrackPartOnly,
                carsInfo, null,
                pageNum, totalPages);

            pages.Add(couplePage);
            pageNum += 1;
            taskNum += 1;

            // passenger loading
            if( loadTask != null )
            {
                const string LOAD_TASK_TYPE = "LOAD";
                const string LOAD_TASK_DESC = "Load train at station platform:";

                string platformName = loadTask.destinationTrack.ID.TrackPartOnly;

                var loadPage = new TaskTemplatePaperData(
                    taskNum.ToString(), LOAD_TASK_TYPE, LOAD_TASK_DESC,
                    startStation.YardID, startStation.StationColor, platformName, TRACK_COLOR,
                    "", "", TemplatePaperData.NOT_USED_COLOR,
                    carsInfo, null, pageNum.ToString(), totalPages.ToString());

                pages.Add(loadPage);
                pageNum += 1;
                taskNum += 1;
            }

            // Transport task
            const string TASK_DESC = "Haul train to the following location:";
            var haulPage = new TaskTemplatePaperData(
                taskNum.ToString(), "HAUL", TASK_DESC, "", TemplatePaperData.NOT_USED_COLOR, "", TemplatePaperData.NOT_USED_COLOR,
                endStation.Name, endStation.Type, endStation.StationColor,
                carsInfo, null, pageNum.ToString(), totalPages.ToString());

            pages.Add(haulPage);
            taskNum += 1;
            pageNum += 1;

            // unload at destination
            if( unloadTask != null )
            {
                const string UNLOAD_TASK_TYPE = "UNLOAD";
                const string UNLOAD_TASK_DESC = "Unload train at station platform:";

                string platformName = unloadTask.destinationTrack.ID.TrackPartOnly;

                var unloadPage = new TaskTemplatePaperData(
                    taskNum.ToString(), UNLOAD_TASK_TYPE, UNLOAD_TASK_DESC,
                    endStation.YardID, endStation.StationColor, platformName, TRACK_COLOR,
                    "", "", TemplatePaperData.NOT_USED_COLOR,
                    carsInfo, null, pageNum.ToString(), totalPages.ToString());

                pages.Add(unloadPage);
                pageNum += 1;
                taskNum += 1;
            }

            // uncouple at destination task
            Track dropoffTrack = storeTask?.destinationTrack ?? transportTask.destinationTrack;

            var uncouplePage = PassBookletUtil.CreateUncoupleTaskData(
                taskNum, endStation.YardID, endStation.StationColor, dropoffTrack.ID.TrackPartOnly,
                carsInfo, null, pageNum, totalPages);

            pages.Add(uncouplePage);
            pageNum += 1;
            taskNum += 1;

            pages.Add(new ValidateJobTaskTemplatePaperData(taskNum.ToString(), pageNum.ToString(), totalPages.ToString()));
            return pages;
        }
    }

    // BookletCreator.GetJobOverviewTemplateData()
    [HarmonyPatch(typeof(BookletCreator), "GetJobOverviewTemplateData")]
    [HarmonyPatch(new Type[] { typeof(Job) })]
    static class BC_GetJobOverviewTemplateData_Patch
    {
        static bool Prefix( Job job, ref List<TemplatePaperData> __result )
        {
            if( (job.jobType != PassJobType.Express) &&
                (job.jobType != PassJobType.Commuter) )
            {
                return true;
            }

            if( (PassBookletUtil.ExtractStationFromId == null) ||
                (PassBookletUtil.CreateCoupleTaskData == null) ||
                (PassBookletUtil.CreateUncoupleTaskData == null) ||
                (PassBookletUtil.GetShuntingPickupsText == null) ||
                (PassBookletUtil.GetShuntingDropOffsText == null) )
            {
                __result = null;
                PassengerJobs.ModEntry.Logger.Error("Couldn't connect to BookletCreator methods!");
                return false;
            }

            // Express or branch service
            var startTask = job.GetJobData().First();
            List<Tuple<TrainCarType, string>> carsInfo;

            if( startTask.type == TaskType.Sequential )
            {
                startTask = startTask.nestedTasks.First().GetTaskData();
            }

            // check for special
            string trainName;
            if( (job.jobType == PassJobType.Express) && SpecialConsistManager.JobToSpecialMap.TryGetValue(job.ID, out SpecialTrain special) )
            {
                int lastDashIdx = job.ID.LastIndexOf('-');
                string trainNum = job.ID.Substring(lastDashIdx + 1);
                trainName = $"{special.Name} #{trainNum}";
            }
            else
            {
                trainName = "a regional express train";
            }
            // if jobtype is commuter then the name is ignored

            carsInfo = PassBookletUtil.GetCarsInfo(startTask.cars);

            TemplatePaperData overviewPage = PassBookletUtil.CreateTransportDescriptionData(job, trainName, startTask.cars, carsInfo);
            
            __result = new List<TemplatePaperData>() { overviewPage };

            return false;
        }
    }
}
