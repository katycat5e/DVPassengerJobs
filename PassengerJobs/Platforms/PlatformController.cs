using DV.Logic.Job;
using DV.TimeKeeping;
using DV.WeatherSystem;
using PassengerJobs.Injectors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class PlatformController : MonoBehaviour
    {
        public const int START_XFER_DELAY = 5;
        private const float TRAIN_CHECK_INTERVAL = 1f;
        private const float LOAD_DELAY = 1f;

        private static readonly AudioClip? _loadCompletedSound = null;
        private static readonly Dictionary<string, PlatformController> _trackToControllerMap = new();

        public static PlatformController GetControllerForTrack(string id)
        {
            return _trackToControllerMap[id];
        }

        public static void HandleGameUnloading()
        {
            foreach (var platform in _trackToControllerMap.Values)
            {
                Destroy(platform);
            }

            _trackToControllerMap.Clear();
        }

        public IPlatformWrapper Platform = null!;
        public SignPrinter[] Signs { get; private set; } = Array.Empty<SignPrinter>();

        public bool IsLoading { get; private set; } = false;
        public bool IsUnloading { get; private set; } = false;

        private string CurrentTimeString => WeatherDriver.Instance.manager.DateTime.ToString("HH:mm");
        private string _lastTimeString = string.Empty;

        private readonly List<SignData.JobInfo> _currentJobs = new();
        private bool _displayDataDirty = false;

        private string? _overrideText = null;
        public string? OverrideText
        {
            get => _overrideText;
            set
            {
                _overrideText = value;
                _displayDataDirty = true;
            }
        }

        static PlatformController()
        {
            _loadCompletedSound = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c.name == "watch_ring");

            if (_loadCompletedSound != null)
            {
                PJMain.Log("Grabbed pocket watch bell sound");
            }
            else
            {
                PJMain.Error("Failed to grab pocket watch bell sound");
            }
        }

        public void Start()
        {
            _trackToControllerMap.Add(Platform.Id, this);
            Signs = SignManager.CreatePlatformSigns(Platform.Id).ToArray();
        }

        private void OnDisable()
        {
            ResetLoadingState();
            StopAllCoroutines();
        }

        #region Sign Handling

        public void SetSignsEnabled(bool enabled)
        {
            foreach (var sign in Signs)
            {
                sign.gameObject.SetActive(enabled);
            }

            if (enabled)
            {
                RefreshDisplays();
            }
        }

        private void RefreshDisplays()
        {
            _lastTimeString = CurrentTimeString;
            var data = new SignData(Platform.DisplayId, _lastTimeString, _currentJobs, _overrideText);

            foreach (var sign in Signs)
            {
                sign.UpdateDisplay(data);
            }

            _displayDataDirty = false;
        }

        public void AddOutgoingJobToSigns(Job job, bool takenViaLoad = false)
        {
            _currentJobs.Add(new SignData.JobInfo(job, false));
            job.JobCompleted += RemoveJobFromSigns;
            job.JobAbandoned += RemoveJobFromSigns;
            _displayDataDirty = true;
        }

        public void AddIncomingJobToSigns(Job job, bool takenViaLoad = false)
        {
            // add incoming jobs to top of list
            _currentJobs.AddToFront(new SignData.JobInfo(job, true));
            job.JobCompleted += RemoveJobFromSigns;
            job.JobAbandoned += RemoveJobFromSigns;
            _displayDataDirty = true;
        }

        public void RemoveJobFromSigns(Job job)
        {
            _currentJobs.RemoveAll(j => j.OriginalID == job.ID);
            _displayDataDirty = true;
        }

        #endregion

        #region Coroutines

        private static readonly WaitForSecondsRealtime _loadUnloadDelay = WaitFor.SecondsRealtime(LOAD_DELAY);

        private Coroutine? _loadUnloadRoutine = null;

        private bool inIdleState = false;
        private bool _currentlyLoading = false;
        private bool _currentlyUnloading = false;
        private int _loadCountdown = START_XFER_DELAY;
        private int _unloadCountdown = START_XFER_DELAY;

        private float _lastUpdate = float.MinValue;

        private void Update()
        {
            if (Time.time - _lastUpdate < TRAIN_CHECK_INTERVAL) return;

            _lastUpdate = Time.time;

            if (CurrentTimeString != _lastTimeString)
            {
                _displayDataDirty = true;
            }

            // if no trains in progress, then display the default message
            if (!(_currentlyLoading || _currentlyUnloading) && !inIdleState)
            {
                OverrideText = null;
                inIdleState = true;
            }

            // check for unloading train, we need to unload before trying to re-load train
            if (Platform.IsAnyTrainPresent(false))
            {
                if (_unloadCountdown == 0)
                {
                    if (_loadUnloadRoutine == null)
                    {
                        inIdleState = false;
                        _loadUnloadRoutine = StartCoroutine(DelayedLoadUnload(false));
                    }
                    return;
                }
                else
                {
                    _unloadCountdown -= 1;
                }
            }
            else
            {
                _unloadCountdown = START_XFER_DELAY;
            }

            // Check for loading train
            if (Platform.IsAnyTrainPresent(true))
            {
                if (_loadCountdown == 0)
                {
                    if (_loadUnloadRoutine == null)
                    {
                        inIdleState = false;
                        _loadUnloadRoutine = StartCoroutine(DelayedLoadUnload(true));
                    }
                    return;
                }
                else
                {
                    _loadCountdown -= 1;
                }
            }
            else
            {
                _loadCountdown = START_XFER_DELAY;
            }

            if (_displayDataDirty)
            {
                RefreshDisplays();
            }
        }

        private IEnumerator DelayedLoadUnload(bool isLoading)
        {
            WarehouseTaskType taskType = isLoading ? WarehouseTaskType.Loading : WarehouseTaskType.Unloading;
            var currentTasks = Platform.GetLoadableTasks(isLoading);

            if (currentTasks.Count == 0)
            {
                ResetLoadingState();
                yield break;
            }

            _currentlyLoading = isLoading;
            _currentlyUnloading = !isLoading;

            bool completedTransfer = false;

            foreach (var task in currentTasks)
            {
                yield return _loadUnloadDelay;

                // make double sure that the cars are able to be loaded
                bool allCarsStopped = Platform.AreCarsStoppedAtPlatform(task.Cars);
                if (!allCarsStopped)
                {
                    // this kills the passengers
                    continue;
                }

                // display the current transferring train
                string message = LocalizationKey.SIGN_BOARDING.L() + '\n';
                if (task.IsLoadTask)
                {
                    message += LocalizationKey.SIGN_OUTGOING_TRAIN.L(task.Job.ID, task.Job.chainData.chainDestinationYardId);
                }
                else
                {
                    message += LocalizationKey.SIGN_INCOMING_TRAIN.L(task.Job.ID, task.Job.chainData.chainOriginYardId);
                }
                OverrideText = message;
                RefreshDisplays();

                // now that we verified that the cars are okay, actually transfer the passengers to/from the cars
                for (int nToProcess = task.Cars.Count; nToProcess > 0; nToProcess--)
                {
                    Car? result = Platform.TransferOneCarOfTask(task, isLoading);

                    if (result == null)
                    {
                        PJMain.Error("Tried to (un)load a car that wasn't there :(");

                        // fail into safe state by completing task
                        task.State = TaskState.Done;
                        foreach (Car car in task.Cars)
                        {
                            if (isLoading)
                            {
                                car.DumpCargo();
                                car.LoadCargo(car.capacity, CargoInjector.PassengerCargo.v1);
                            }
                            else
                            {
                                car.DumpCargo();
                            }
                        }
                        Platform.RemoveTask(task);

                        break;
                    }

                    yield return _loadUnloadDelay;
                }
            }

            OverrideText = null;
            completedTransfer = true;

            // all jobs processed

            if (completedTransfer && !isLoading && Platform.IsAnyTrainPresent(true))
            {
                var loadRoutine = DelayedLoadUnload(true);
                while (loadRoutine.MoveNext())
                {
                    yield return loadRoutine.Current;
                }
                yield break;
            }

            if (completedTransfer && _loadCompletedSound)
            {
                if (_loadCompletedSound)
                {
                    Transform playerTform = PlayerManager.PlayerCamera.transform;
                    _loadCompletedSound.Play(playerTform.position, parent: playerTform);
                }

                OverrideText = isLoading ?
                    LocalizationKey.SIGN_DEPARTING.L() :
                    LocalizationKey.SIGN_EMPTY.L();
                RefreshDisplays();
                yield return WaitFor.Seconds(LOAD_DELAY * 10);
            }

            ResetLoadingState();
        }

        private void ResetLoadingState()
        {
            if (_loadUnloadRoutine != null)
            {
                StopCoroutine(_loadUnloadRoutine);
                _loadUnloadRoutine = null;
            }

            _currentlyLoading = _currentlyUnloading = false;
            _loadCountdown = _unloadCountdown = START_XFER_DELAY;
        }

        #endregion
    }
}
