using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DV.Logic.Job;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PassengerJobsMod
{
    using WMDataState = WarehouseMachine.WarehouseLoadUnloadDataPerJob.State;

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
        private Coroutine MessageDequeueRoutine = null;

        private bool loadInRange = false;
        private bool unloadInRange = false;

        private int loadCountdown = START_XFER_DELAY;
        private int unloadCountdown = START_XFER_DELAY;

        private bool loading = false;
        private bool unloading = false;

        private readonly Queue<string> StatusMessages = new Queue<string>();

        public void Initialize( RailTrack track, string name, string yardId )
        {
            if( LoadCompletedSound == null )
            {
                LoadCompletedSound = PocketWatchManager.Instance.alarmAudio;
                if( LoadCompletedSound != null ) PassengerJobs.ModEntry.Logger.Log("Grabbed pocket watch bell sound");
            }

            PlatformTrack = track;
            LogicMachine = new WarehouseMachine(track.logicTrack, SUPPORTED_CARGO);
            PlatformName = name;
            YardId = yardId;
            enabled = true;
        }

        public void AddSign( GameObject signObject )
        {
            SignObjects.Add(signObject);

            // Job displays
            if( signObject.GetComponent<SignPrinter>() is SignPrinter newDisplay )
            {
                DisplayComponents.Add(newDisplay);
                newDisplay.UpdateDisplay(new SignData(PlatformTrack.logicTrack.ID.TrackPartOnly, "12:00"));
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning("Couldn't find SignPrinter component in station sign object");
            }

            signObject.SetActive(SignsActive);
        }

        public void UpdateSignsText( SignData data )
        {
            foreach( var printer in DisplayComponents )
            {
                printer.UpdateDisplay(data);
            }
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
            MessageDequeueRoutine = null;
            StatusMessages.Clear();
        }

        private const int ELEM_SPACING = 10;
        private const int ELEM_START_Y = 40;
        private const int ELEM_HEIGHT = 20;
        private const int ELEM_WIDTH = 110;
        private const int BOX_WIDTH = ELEM_WIDTH + (ELEM_SPACING * 2);

        void OnGUI()
        {
            if( loadInRange || unloadInRange )
            {
                int boxHeight = ELEM_START_Y + ELEM_HEIGHT + (ELEM_SPACING * 2);
                if( loadInRange && unloadInRange && !loading && !unloading )
                {
                    boxHeight += (ELEM_HEIGHT + ELEM_SPACING);
                }

                int boxX = Screen.width - ELEM_SPACING - BOX_WIDTH;
                int boxY = ELEM_SPACING;
                GUI.Box(new Rect(boxX, boxY, BOX_WIDTH, boxHeight), PlatformName);

                boxY += ELEM_START_Y;

                if( loadInRange && !unloading )
                {
                    if( loading )
                    {
                        GUI.Label(new Rect(boxX + ELEM_SPACING, boxY, ELEM_WIDTH, ELEM_HEIGHT), "Boarding...");
                    }
                    else
                    {
                        GUI.Label(new Rect(boxX + ELEM_SPACING, boxY, ELEM_WIDTH, ELEM_HEIGHT), $"Starting boarding in {loadCountdown}...");
                    }
                    boxY += (ELEM_HEIGHT + ELEM_SPACING);
                }

                if( unloadInRange && !loading )
                {
                    if( unloading )
                    {
                        GUI.Label(new Rect(boxX + ELEM_SPACING, boxY, ELEM_WIDTH, ELEM_HEIGHT), "Disembarking...");
                    }
                    else
                    {
                        GUI.Label(new Rect(boxX + ELEM_SPACING, boxY, ELEM_WIDTH, ELEM_HEIGHT), $"Disembarking in {unloadCountdown}...");
                    }
                }
            }

            if( StatusMessages.TryPeek(out string curMsg) )
            {
                int width = Screen.width / 3;
                int left = (Screen.width - width) / 2;

                GUI.Box(new Rect(left, ELEM_SPACING, width, ELEM_START_Y), curMsg);
            }
        }

        private static readonly FieldInfo CurrentTasksField = AccessTools.Field(typeof(WarehouseMachine), "currentTasks");

        private System.Collections.IEnumerator CheckForTrains()
        {
            while( true )
            {
                yield return WaitFor.SecondsRealtime(TRAIN_CHECK_INTERVAL);

                loadInRange = LogicMachine.AnyTrainToLoadPresentOnTrack();
                if( loadInRange )
                {
                    if( loadCountdown == 0 )
                    {
                        if( DelayedLoadUnloadRoutine == null )
                        {
                            DelayedLoadUnloadRoutine = StartCoroutine(DelayedLoadUnload(true));
                        }
                        else continue;
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
                
                unloadInRange = LogicMachine.AnyTrainToUnloadPresentOnTrack();
                if( unloadInRange )
                {
                    if( unloadCountdown == 0 )
                    {
                        if( DelayedLoadUnloadRoutine == null )
                        {
                            DelayedLoadUnloadRoutine = StartCoroutine(DelayedLoadUnload(false));
                        }
                        else continue;
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

                // set sign display
                if( !(loading || unloading) )
                {
                    if( CurrentTasksField.GetValue(LogicMachine) is List<WarehouseTask> tasks )
                    {
                        var signData = new SignData(PlatformTrack.logicTrack.ID.TrackPartOnly, "12:00");

                        //var sb = new StringBuilder();
                        //bool first = true;
                        if( tasks.Count > 0 )
                        {
                            int nJobs = (tasks.Count >= 2) ? 2 : 1;
                            signData.Jobs = new SignData.JobInfo[nJobs];

                            for( int i = 0; i < nJobs; i++ )
                            {
                                WarehouseTask task = tasks[i];
                                SignData.JobInfo jobInfo = signData.Jobs[i];

                                jobInfo.ID = task.Job.ID;

                                if( SpecialConsistManager.JobToSpecialMap.TryGetValue(task.Job.ID, out SpecialTrain special) )
                                {
                                    // Named Train
                                    jobInfo.Name = special.Name;
                                }
                                else
                                {
                                    // Ordinary boring train
                                    char jobTypeChar = (tasks[i].Job.jobType == PassJobType.Commuter) ? 'C' : 'E';

                                    string jobId = tasks[i].Job.ID;
                                    int lastDashIdx = jobId.LastIndexOf('-');
                                    string trainNum = jobId.Substring(lastDashIdx + 1);

                                    jobInfo.Name = $"Train {jobTypeChar}{trainNum}";
                                }

                                if( task.warehouseTaskType == WarehouseTaskType.Loading )
                                {
                                    // This is an outgoing train
                                    jobInfo.Incoming = false;
                                    jobInfo.Dest = task.Job.chainData.chainDestinationYardId;
                                }
                                else
                                {
                                    // unloading
                                    jobInfo.Incoming = true;
                                    jobInfo.Src = task.Job.chainData.chainOriginYardId;
                                }
                            }
                        }

                        UpdateSignsText(signData);
                    }
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
                yield break;
            }

            if( isLoading ) loading = true;
            else unloading = true;

            bool completedTransfer = false;

            foreach( var data in currentLoadData )
            {
                yield return carDelay;
                string loadDirStr = isLoading ? "load" : "unload";

                if( data.state == WMDataState.FullLoadUnloadPossible )
                {
                    // all cars ready to load
                    bool anyCarMoving = false;
                    foreach( WarehouseTask task in data.tasksAvailableToProcess )
                    {
                        foreach( Car car in task.cars )
                        {
                            if( SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) )
                            {
                                if( trainCar.GetForwardSpeed() > 0.3f )
                                {
                                    anyCarMoving = true;
                                    break;
                                }
                            }
                            else
                            {
                                PassengerJobs.ModEntry.Logger.Error("Unexpected error: can't pair " + car.ID + " with its TrainCar!");
                            }
                        }

                        if( anyCarMoving ) break;
                    }

                    if( anyCarMoving )
                    {
                        // this kills the passengers
                        string msg = $"[{data.job.ID}] {loadDirStr} error, cars need to be stationary!";
                        DisplayStatusMessage(msg);
                        continue;
                    }

                    foreach( WarehouseTask task in data.tasksAvailableToProcess )
                    {
                        for( int nToProcess = task.cars.Count; nToProcess > 0; nToProcess-- )
                        {
                            Car result = ( isLoading ) ?
                                LogicMachine.LoadOneCarOfTask(task) :
                                LogicMachine.UnloadOneCarOfTask(task);
                            
                            if( result == null )
                            {
                                PassengerJobs.ModEntry.Logger.Error("Tried to (un)load a car that wasn't there :(");
                                break;
                            }

                            yield return carDelay;
                        }
                    }

                    string trainStat = (isLoading) ? "loaded" : "unloaded";
                    string successMsg = $"[{data.job.ID}] train fully {trainStat}!";
                    DisplayStatusMessage(successMsg);
                    completedTransfer = true;
                }
                else
                {
                    // need to have all cars at platform to load
                    string msg = $"[{data.job.ID}] {loadDirStr} error, need all cars on track";
                    DisplayStatusMessage(msg);
                    continue;
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

        private void DisplayStatusMessage( string message )
        {
            StatusMessages.Enqueue(message);
            if( MessageDequeueRoutine == null )
            {
                MessageDequeueRoutine = StartCoroutine(DequeueMessages());
            }
        }

        private System.Collections.IEnumerator DequeueMessages()
        {
            while( StatusMessages.Count > 0 )
            {
                yield return WaitFor.SecondsRealtime(ERROR_DISPLAY_TIME);
                StatusMessages.Dequeue();
            }
        }
    }
}
