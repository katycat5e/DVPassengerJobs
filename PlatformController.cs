using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV.Logic.Job;
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

        private Coroutine DelayedLoadUnloadRoutine = null;
        private Coroutine MessageDequeueRoutine = null;

        private bool loadInRange = false;
        private bool unloadInRange = false;

        private int loadCountdown = START_XFER_DELAY;
        private int unloadCountdown = START_XFER_DELAY;

        private bool loading = false;
        private bool unloading = false;

        private readonly Queue<string> StatusMessages = new Queue<string>();

        public void Initialize( RailTrack track, string name )
        {
            if( LoadCompletedSound == null )
            {
                LoadCompletedSound = PocketWatchManager.Instance.alarmAudio;
                if( LoadCompletedSound != null ) PassengerJobs.ModEntry.Logger.Log("Grabbed pocket watch bell sound");
            }

            PlatformTrack = track;
            LogicMachine = new WarehouseMachine(track.logicTrack, SUPPORTED_CARGO);
            PlatformName = name;
            enabled = true;
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
                            if( TrainCar.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) )
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
