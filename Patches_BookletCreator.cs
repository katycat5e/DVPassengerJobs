using Harmony12;
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
        public const string JOB_COVER_TITLE = "PASSENGER";
        public const string JOB_TYPE_NAME = "PASSENGER RUN";
        public static readonly Color PASS_JOB_COLOR = PassengerLicenseUtil.PASSENGER_LICENSE_COLOR;

        public static string GetJobDescription( string[] destYards )
        {
            if( destYards.Length < 2 ) return "Transport passengers";
            
            var sb = new StringBuilder("Transport passengers via ");
            for( int i = 0; i < (destYards.Length - 1); i++ )
            {
                if( i == (destYards.Length - 2) )
                {
                    if( destYards.Length == 3 ) sb.Append(' ');
                    if( destYards.Length >= 3 ) sb.Append("and ");
                }
                sb.Append(destYards[i]);

                if( i == (destYards.Length - 2) ) return sb.ToString();
                if( destYards.Length > 3 ) sb.Append(", ");
            }

            return sb.ToString();
        }

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

    }

    // BookletCreator.GetBookletTemplateData
    [HarmonyPatch(typeof(BookletCreator), "GetBookletTemplateData")]
    static class BC_GetBookletTemplateData_Patch
    {
        static bool Prefix( Job job, ref List<TemplatePaperData> __result, Color ___TRACK_COLOR )
        {
            if( job.jobType != PassengerJobGenerator.JT_Passenger ) return true;

            if( (PassBookletUtil.ExtractStationFromId == null) || 
                (PassBookletUtil.CreateCoupleTaskData == null) || 
                (PassBookletUtil.CreateUncoupleTaskData == null) )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't connect to BookletCreator methods!");
                return false;
            }

            if( !(job.chainData is ComplexChainData jobChainData) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Wrong type of chain data on job {job.ID}");
                return false;
            }

            var tasks = job.GetJobData();
            TaskData startTaskData = tasks.First();
            TaskData endTaskData = tasks.Last();

            var pages = new List<TemplatePaperData>();
            int pageNum = 1;
            int totalPages = 4 + tasks.Count;

            var carsInfo = PassBookletUtil.GetCarsInfo(startTaskData.cars);

            // Cover page
            var coverPage = new CoverPageTemplatePaperData(job.ID, PassBookletUtil.JOB_COVER_TITLE, pageNum.ToString(), totalPages.ToString());
            pages.Add(coverPage);
            pageNum += 1;

            // Job description page
            string description = PassBookletUtil.GetJobDescription(jobChainData.chainDestinationYardIds);
            string trainLength = startTaskData.cars.Sum(c => c.length).ToString("F") + " m";
            string trainMass = (PassBookletUtil.GetLoadedConsistMass(startTaskData.cars) * 0.001f).ToString("F") + " t";
            string trainValue = "$" + (PassBookletUtil.GetTrainValue(startTaskData.cars) / 1000000f).ToString("F") + "m";
            string timeLimit = (job.TimeLimit > 0) ? (Mathf.FloorToInt(job.TimeLimit / 60f) + " min") : "No bonus";
            string payment = job.GetBasePaymentForTheJob().ToString();

            StationInfo startStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainOriginYardId);
            StationInfo endStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainDestinationYardId);

            var descriptionPage = new FrontPageTemplatePaperData(
                PassBookletUtil.JOB_TYPE_NAME, "", job.ID, PassBookletUtil.PASS_JOB_COLOR, description,
                job.requiredLicenses, new List<CargoType>() { CargoType.Passengers }, startTaskData.cargoTypePerCar,
                "", "", TemplatePaperData.NOT_USED_COLOR,
                startStation.Name, startStation.Type, startStation.StationColor,
                endStation.Name, endStation.Type, endStation.StationColor,
                carsInfo, trainLength, trainMass, trainValue, timeLimit, payment,
                pageNum.ToString(), totalPages.ToString());

            pages.Add(descriptionPage);
            pageNum += 1;

            // Task pages
            int taskNum = 1;

            // initial coupling
            var couplePage = PassBookletUtil.CreateCoupleTaskData(
                taskNum, startStation.YardID, startStation.StationColor, startTaskData.startTrack.ID.TrackPartOnly,
                carsInfo, startTaskData.cargoTypePerCar,
                pageNum, totalPages);

            pages.Add(couplePage);
            pageNum += 1;
            taskNum += 1;

            // transport legs
            for( int i = 0; i < tasks.Count; i++ )
            {
                TaskData task = tasks[i];
                var destYard = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[jobChainData.chainDestinationYardIds[i]];
                string destTrackName = task.destinationTrack.ID.TrackPartOnly;

                const string HAUL_TASK_TYPE = "HAUL";
                const string HAUL_TASK_DESC = "Haul train to the following station platform:";

                var taskPage = new TaskTemplatePaperData(
                    taskNum.ToString(), HAUL_TASK_TYPE, HAUL_TASK_DESC,
                    destYard.stationInfo.YardID, destYard.stationInfo.StationColor, destTrackName, ___TRACK_COLOR,
                    "", "", TemplatePaperData.NOT_USED_COLOR,
                    carsInfo, task.cargoTypePerCar,
                    pageNum.ToString(), totalPages.ToString());

                pages.Add(taskPage);
                pageNum += 1;
                taskNum += 1;
            }

            // final uncoupling
            var uncouplePage = PassBookletUtil.CreateUncoupleTaskData(
                taskNum, endStation.YardID, endStation.StationColor, endTaskData.destinationTrack.ID.TrackPartOnly,
                carsInfo, endTaskData.cargoTypePerCar,
                pageNum, totalPages);

            pages.Add(uncouplePage);

            __result = pages;

            return false;
        }
    }

    // BookletCreator.GetJobOverviewTemplateData()
    [HarmonyPatch(typeof(BookletCreator), "GetJobOverviewTemplateData")]
    [HarmonyPatch(new Type[] { typeof(Job) })]
    static class BC_GetJobOverviewTemplateData_Patch
    {
        static bool Prefix( Job job, ref List<TemplatePaperData> __result )
        {
            if( job.jobType != PassengerJobGenerator.JT_Passenger ) return true;

            if( PassBookletUtil.ExtractStationFromId == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't connect to BookletCreator methods!");
                return false;
            }

            if( !(job.chainData is ComplexChainData jobChainData) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Wrong type of chain data on job {job.ID}");
                return false;
            }

            var tasks = job.GetJobData();
            TaskData startTaskData = tasks.First();

            var carsInfo = PassBookletUtil.GetCarsInfo(startTaskData.cars);

            string description = PassBookletUtil.GetJobDescription(jobChainData.chainDestinationYardIds);
            string trainLength = startTaskData.cars.Sum(c => c.length).ToString("F") + " m";
            string trainMass = (PassBookletUtil.GetLoadedConsistMass(startTaskData.cars) * 0.001f).ToString("F") + " t";
            string trainValue = "$" + (PassBookletUtil.GetTrainValue(startTaskData.cars) / 1000000f).ToString("F") + "m";
            string timeLimit = (job.TimeLimit > 0) ? (Mathf.FloorToInt(job.TimeLimit / 60f) + " min") : "No bonus";
            string payment = job.GetBasePaymentForTheJob().ToString();

            StationInfo startStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainOriginYardId);
            StationInfo endStation = PassBookletUtil.ExtractStationFromId(job.chainData.chainDestinationYardId);

            var pageData = new FrontPageTemplatePaperData(
                PassBookletUtil.JOB_TYPE_NAME, "", job.ID, PassBookletUtil.PASS_JOB_COLOR, description,
                job.requiredLicenses, new List<CargoType>() { CargoType.Passengers }, startTaskData.cargoTypePerCar,
                "", "", TemplatePaperData.NOT_USED_COLOR, 
                startStation.Name, startStation.Type, startStation.StationColor,
                endStation.Name, endStation.Type, endStation.StationColor, 
                carsInfo, trainLength, trainMass, trainValue, timeLimit, payment, "", "");

            __result = new List<TemplatePaperData>()
            {
                pageData
            };

            return false;
        }
    }
}
