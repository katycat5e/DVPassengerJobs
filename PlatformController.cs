using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DV.Logic.Job;
using HarmonyLib;
using UnityEngine;

namespace PassengerJobsMod
{
    using WMDataState = WarehouseMachine.WarehouseLoadUnloadDataPerJob.State;

    using SignJobInfo = Tuple<Job, bool>; // true = incoming

    public class PlatformController : MonoBehaviour
    {
        public const int START_XFER_DELAY = 5;

        private static readonly List<CargoType> SUPPORTED_CARGO = new List<CargoType>() { CargoType.Passengers };
        private const float TRAIN_CHECK_INTERVAL = 1f;
        private const float LOAD_DELAY = 1f;
        private const float ERROR_DISPLAY_TIME = 5f;

        private static AudioClip LoadCompletedSound = null;

        public WarehouseMachine LogicMachine { get; protected set; }
        public RailTrack PlatformTrack { get; protected set; }
        public string PlatformName { get; protected set; }
        public string YardId { get; protected set; }

        public List<GameObject> SignObjects = new List<GameObject>();
        public bool SignsActive = false;

        public List<SignPrinter> DisplayComponents = new List<SignPrinter>();

        private Coroutine DelayedLoadUnloadRoutine = null;

        private bool loadInRange = false;
        private bool unloadInRange = false;

        private int loadCountdown = START_XFER_DELAY;
        private int unloadCountdown = START_XFER_DELAY;

        private bool loading = false;
        private bool unloading = false;

        private readonly List<SignJobInfo> DisplayedJobs = new List<SignJobInfo>();
        private bool JobListDirty = true;

        private SignData.JobInfo[] CachedJobsData = null;
        private string LastTimeString = "12:00";
        private DateTime LastTime = DateTime.MinValue;
        private string TrackId = null;
        private string OverrideMessage = null;

        public void Initialize( RailTrack track, string name, string yardId )
        {
            if( LoadCompletedSound == null )
            {
                LoadCompletedSound = PocketWatchManager.Instance.alarmAudio;
                if( LoadCompletedSound != null ) PassengerJobs.ModEntry.Logger.Log("Grabbed pocket watch bell sound");
            }

            PlatformTrack = track;
            TrackId = PlatformTrack.logicTrack.ID.TrackPartOnly;
            LogicMachine = new WarehouseMachine(track.logicTrack, SUPPORTED_CARGO);
            PlatformName = name;
            YardId = yardId;
            enabled = true;
        }

        public void AddSign( GameObject signObject, StationSignType signType )
        {
            SignObjects.Add(signObject);

            // Job displays
            SignPrinter newDisplay = signObject.AddComponent<SignPrinter>();

            if( newDisplay != null )
            {
                newDisplay.Initialize(signType);
                newDisplay.UpdateDisplay(new SignData(PlatformTrack.logicTrack.ID.TrackPartOnly, "12:00"));
                DisplayComponents.Add(newDisplay);
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning("Couldn't add SignPrinter component to station sign object");
            }

            signObject.SetActive(SignsActive);
        }

        public void AddOutgoingJobToDisplay( Job job )
        {
            DisplayedJobs.Add(new SignJobInfo(job, false));
            job.JobCompleted += RemoveJobFromDisplay;
            job.JobAbandoned += RemoveJobFromDisplay;
            JobListDirty = true;
        }

        public void AddIncomingJobToDisplay( Job job, bool _ = false )
        {
            // add incoming jobs to top of list
            DisplayedJobs.AddToFront(new SignJobInfo(job, true));
            job.JobCompleted += RemoveJobFromDisplay;
            job.JobAbandoned += RemoveJobFromDisplay;
            JobListDirty = true;
        }

        public void RemoveJobFromDisplay( Job job )
        {
            for( int i = 0; i < DisplayedJobs.Count; i++ )
            {
                if( DisplayedJobs[i].Item1.ID.Equals(job.ID) )
                {
                    DisplayedJobs.RemoveAt(i);
                    JobListDirty = true;
                    return;
                }
            }
        }

        public void RegenerateJobsData()
        {
            CachedJobsData = new SignData.JobInfo[DisplayedJobs.Count];

            for( int i = 0; i < DisplayedJobs.Count; i++ )
            {
                Job job = DisplayedJobs[i].Item1;
                CachedJobsData[i] = new SignData.JobInfo()
                {
                    Incoming = DisplayedJobs[i].Item2,
                    ID = job.ID,
                    Name = GetTrainName(job),
                    Src = job.chainData.chainOriginYardId,
                    Dest = job.chainData.chainDestinationYardId
                };
            }

            JobListDirty = false;
        }

        public void RefreshDisplays()
        {
            if( JobListDirty ) RegenerateJobsData();
            var data = new SignData(TrackId, LastTimeString) { Jobs = CachedJobsData };

            foreach( var printer in DisplayComponents )
            {
                printer.UpdateDisplay(data, OverrideMessage);
            }
        }

        public void ApplyOverrideText( string message )
        {
            OverrideMessage = message;
            RefreshDisplays();
        }

        public void ClearOverrideText()
        {
            OverrideMessage = null;
            RefreshDisplays();
        }

        private string GetTrainName( Job job )
        {
            string jobId = job.ID;
            int lastDashIdx = jobId.LastIndexOf('-');
            string trainNum = jobId.Substring(lastDashIdx + 1);

            if( SpecialConsistManager.JobToSpecialMap.TryGetValue(job.ID, out SpecialTrain special) )
            {
                // Named Train
                return $"{special.Name} {trainNum}";
            }

            // Ordinary boring train
            char jobTypeChar = (job.jobType == PassJobType.Commuter) ? 'C' : 'E';

            return $"Train {jobTypeChar}{trainNum}";
        }

        public void SetSignState( bool en )
        {
            SignsActive = en;

            foreach( GameObject s in SignObjects )
            {
                s.SetActive(en);
            }
        }

        void OnEnable()
        {
            StartCoroutine(CheckForTrains());
        }

        void OnDisable()
        {
            StopAllCoroutines();
            DelayedLoadUnloadRoutine = null;
        }

        // type = List<WarehouseTask>
        private static readonly FieldInfo CurrentTasksField = AccessTools.Field(typeof(WarehouseMachine), "currentTasks");

        private bool IsAnyTrainAtPlatform( bool loading )
        {
            if( !(CurrentTasksField?.GetValue(LogicMachine) is List<WarehouseTask> currentTasks) ) return false;

            WarehouseTaskType taskType = loading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;

            foreach( WarehouseTask warehouseTask in currentTasks )
            {
                if( warehouseTask.readyForMachine && (warehouseTask.warehouseTaskType == taskType) && AreCarsStoppedAtPlatform(warehouseTask.cars) )
                {
                    return true;
                }
            }
            return false;
        }

        private bool AreCarsStoppedAtPlatform( IEnumerable<Car> cars )
        {
            // do it in two stages to exit early before speed lookup
            foreach( Car car in cars )
            {
                // make sure cars are all on the correct track
                if( car.CurrentTrack != LogicMachine.WarehouseTrack ) return false;
            }

            foreach( Car car in cars )
            {
                // make sure cars are stationary
                if( !(IdGenerator.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) && (Mathf.Abs(trainCar.GetForwardSpeed()) < 0.3f)) )
                {
                    // couldn't find logic car or car was moving
                    return false;
                }
            }

            return true;
        }

        private System.Collections.IEnumerator CheckForTrains()
        {
            bool inIdleState = false;
            bool timeDirty = false;

            while( true )
            {
                yield return WaitFor.SecondsRealtime(TRAIN_CHECK_INTERVAL);

                // Check whether to update the time
                DateTime newTime = DVTime_Patch.GetCurrentTime();
                TimeSpan timeDelta = newTime.Subtract(LastTime);
                if( timeDelta.TotalSeconds >= 60 )
                {
                    LastTime = newTime;
                    LastTimeString = newTime.ToString("hh:mm");
                    timeDirty = true;
                }
                else
                {
                    timeDirty = false;
                }

                // Check for loading train, is highest priority on sign
                loadInRange = IsAnyTrainAtPlatform(true);
                if( loadInRange )
                {
                    if( loadCountdown == 0 )
                    {
                        if( DelayedLoadUnloadRoutine == null )
                        {
                            inIdleState = false;
                            DelayedLoadUnloadRoutine = StartCoroutine(DelayedLoadUnload(true));
                        }
                        continue;
                    }
                    else
                    {
                        loadCountdown -= 1;
                    }
                }
                else
                {
                    // !loadInRange
                    loadCountdown = START_XFER_DELAY;
                }
                
                // Check for unloading train, next highest priority
                unloadInRange = IsAnyTrainAtPlatform(false);
                if( unloadInRange )
                {
                    if( unloadCountdown == 0 )
                    {
                        if( DelayedLoadUnloadRoutine == null )
                        {
                            inIdleState = false;
                            DelayedLoadUnloadRoutine = StartCoroutine(DelayedLoadUnload(false));
                        }
                        continue;
                    }
                    else
                    {
                        unloadCountdown -= 1;
                    }
                }
                else
                {
                    // !unloadInRange
                    unloadCountdown = START_XFER_DELAY;
                }

                // if no trains in progress, then display the default message
                if( !(loading || unloading) && !inIdleState )
                {
                    ClearOverrideText();
                    inIdleState = true;
                }
                else if( JobListDirty || timeDirty )
                {
                    RefreshDisplays();
                }
            }
        }

        private System.Collections.IEnumerator DelayedLoadUnload( bool isLoading )
        {
            WaitForSeconds carDelay = WaitFor.Seconds(LOAD_DELAY);

            WarehouseTaskType taskType = isLoading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;
            var currentLoadData = LogicMachine.GetCurrentLoadUnloadData(taskType);

            if( currentLoadData.Count == 0 )
            {
                DelayedLoadUnloadRoutine = null;
                loading = false;
                unloading = false;
                yield break;
            }

            if( isLoading )
            {
                loading = true;
                unloading = false;
            }
            else
            {
                loading = false;
                unloading = true;
            }

            bool completedTransfer = false;

            foreach( var data in currentLoadData )
            {
                yield return carDelay;

                if( data.state == WMDataState.FullLoadUnloadPossible )
                {
                    // all cars ready to load
                    foreach( WarehouseTask task in data.tasksAvailableToProcess )
                    {
                        // make double sure that the cars are able to be loaded
                        bool allCarsStopped = AreCarsStoppedAtPlatform(task.cars);
                        if( !allCarsStopped )
                        {
                            // this kills the passengers
                            continue;
                        }

                        // display the current transferring train
                        string message;
                        if( task.warehouseTaskType == WarehouseTaskType.Loading )
                        {
                            message = $"Now Boarding\n{task.Job.ID} to {task.Job.chainData.chainDestinationYardId}";
                        }
                        else
                        {
                            message = $"Now Boarding\n{task.Job.ID} from {task.Job.chainData.chainOriginYardId}";
                        }
                        ApplyOverrideText(message);

                        // now that we verified that the cars are okay, actually transfer the passengers to/from the cars
                        for( int nToProcess = task.cars.Count; nToProcess > 0; nToProcess-- )
                        {
                            Car result = ( isLoading ) ?
                                LogicMachine.LoadOneCarOfTask(task) :
                                LogicMachine.UnloadOneCarOfTask(task);
                            
                            if( result == null )
                            {
                                PassengerJobs.ModEntry.Logger.Error("Tried to (un)load a car that wasn't there :(");

                                // fail into safe state by completing task
                                task.state = TaskState.Done;
                                foreach( Car car in task.cars )
                                {
                                    if( isLoading ) car.LoadCargo(car.capacity, CargoType.Passengers);
                                    else car.DumpCargo();
                                }
                                LogicMachine.RemoveWarehouseTask(task);

                                break;
                            }

                            yield return carDelay;
                        }
                    }

                    ClearOverrideText();
                    completedTransfer = true;
                }
            }
            // all jobs processed

            if( completedTransfer && (LoadCompletedSound != null) )
            {
                Transform playerTform = PlayerManager.PlayerCamera.transform;
                LoadCompletedSound.Play(playerTform.position, parent: playerTform);
            }

            loading = unloading = false;
            DelayedLoadUnloadRoutine = null;
        }
    }
}
