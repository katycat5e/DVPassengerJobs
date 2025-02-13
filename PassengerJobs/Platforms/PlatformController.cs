using DV.Logic.Job;
using DV.WeatherSystem;
using PassengerJobs.Injectors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private string? _overrideText = null;
        public string? OverrideText
        {
            get => _overrideText;
            set
            {
                _overrideText = value;
            }
        }

        static PlatformController()
        {
            _loadCompletedSound = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c.name == "watch_ring");

            if (_loadCompletedSound != null)
            {
                PJMain.Log("Grabbed pocket watch bell sound");
                DontDestroyOnLoad(_loadCompletedSound);
            }
            else
            {
                PJMain.Error("Failed to grab pocket watch bell sound");
            }
        }

        public void Start()
        {
            _trackToControllerMap.Add(Platform.TrackId, this);
            Signs = SignManager.CreatePlatformSigns(Platform.Id).ToArray();

            _stateMachine = new StateMachine(this);
        }

        private void Update()
        {
            if (_loadUnloadRoutine is null)
            {
                _stateMachine.Reset();
                _loadUnloadRoutine = StartCoroutine(_stateMachine);
            }
        }

        private void OnDisable()
        {
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
        }

        public void AddOutgoingJobToSigns(Job job, bool _ = false)
        {
            _currentJobs.Add(new SignData.JobInfo(job, false));
            job.JobCompleted += RemoveJobFromSigns;
            job.JobAbandoned += RemoveJobFromSigns;
            job.JobExpired += RemoveJobFromSigns;

            OverrideText = null;
        }

        public void AddIncomingJobToSigns(Job job, bool _ = false)
        {
            // add incoming jobs to top of list
            _currentJobs.AddToFront(new SignData.JobInfo(job, true));
            job.JobCompleted += RemoveJobFromSigns;
            job.JobAbandoned += RemoveJobFromSigns;
            job.JobExpired += RemoveJobFromSigns;

            OverrideText = null;
        }

        public void RemoveJobFromSigns(Job job)
        {
            _currentJobs.RemoveAll(j => j.OriginalID == job.ID);
            OverrideText = null;
        }

        #endregion

        #region Coroutines

        private StateMachine _stateMachine = null!;
        private Coroutine? _loadUnloadRoutine = null;

        private enum PlatformState
        {
            WaitingForTrain,
            Unloading,
            Loading,
            Paused,
        }

        private class StateMachine : IEnumerator
        {
            private PlatformState _platformState = PlatformState.WaitingForTrain;
            private int _beginTransferCountdown;

            public object? Current => new WaitForSecondsRealtime(LOAD_DELAY);

            private PlatformController _controller;
            private IPlatformWrapper _platform;

            public StateMachine(PlatformController controller)
            {
                _controller = controller;
                _platform = controller.Platform;
            }

            public bool MoveNext()
            {
                var newState = _platformState switch
                {
                    PlatformState.WaitingForTrain => TickWaitForTrain(),
                    PlatformState.Unloading => TickUnloading(),
                    PlatformState.Loading => TickLoading(),
                    _ => PlatformState.WaitingForTrain,
                };

                if (newState != _platformState)
                {
                    DebugLog($"Switching state: {newState}");

                    bool boardingFinished = false;
                    if (_platformState == PlatformState.Unloading)
                    {
                        boardingFinished = true;
                        _controller.OverrideText = LocalizationKey.SIGN_EMPTY.L();
                    }
                    else if (_platformState == PlatformState.Loading)
                    {
                        boardingFinished = true;
                        _controller.OverrideText = LocalizationKey.SIGN_DEPARTING.L();
                    }

                    if (boardingFinished)
                    {
                        _loadCompletedSound.Play2D();
                    }

                    _platformState = newState;
                    ResetCounters();
                }

                _controller.RefreshDisplays();

                return true;
            }

            public void Reset()
            {
                _platformState = PlatformState.WaitingForTrain;
            }

            private void ResetCounters()
            {
                _beginTransferCountdown = START_XFER_DELAY;
            }

            private PlatformState TickWaitForTrain()
            {
                if (!_platform.IsAnyTrainPresent())
                {
                    ResetCounters();
                    return PlatformState.WaitingForTrain;
                }

                if (_beginTransferCountdown > 0)
                {
                    _beginTransferCountdown--;
                    DebugLog($"Countdown = {_beginTransferCountdown}");
                    return PlatformState.WaitingForTrain;
                }

                // train present, countdown == 0
                return _platform.IsAnyTrainPresent(false) ? PlatformState.Unloading : PlatformState.Loading;
            }

            private PlatformState TickUnloading()
            {
                var tasks = _platform.GetLoadableTasks(false);

                if (tasks.Count == 0)
                {
                    return _platform.IsAnyTrainPresent(true) ? PlatformState.Loading : PlatformState.WaitingForTrain;
                }

                var task = tasks[0];

                // update displays
                string message = LocalizationKey.SIGN_BOARDING.L() + '\n';
                message += LocalizationKey.SIGN_INCOMING_TRAIN.L(task.Job.ID, task.Job.chainData.chainOriginYardId);

                _controller.OverrideText = message;

                // perform transfer
                var transferredCar = _platform.TransferOneCarOfTask(task, false);
                DebugLog($"Unloaded car {transferredCar?.ID}");

                if (transferredCar == null)
                {
                    PJMain.Error("Tried to (un)load a car that wasn't there :(");
                    FailSafeTask(task);
                }

                return PlatformState.Unloading;
            }

            private PlatformState TickLoading()
            {
                var tasks = _platform.GetLoadableTasks(true);

                if (tasks.Count == 0)
                {
                    return _platform.IsAnyTrainPresent(false) ? PlatformState.Unloading : PlatformState.WaitingForTrain;
                }
                
                var task = tasks[0];

                // update displays
                string message = LocalizationKey.SIGN_BOARDING.L() + '\n';
                message += LocalizationKey.SIGN_OUTGOING_TRAIN.L(task.Job.ID, task.Job.chainData.chainDestinationYardId);

                _controller.OverrideText = message;

                // perform transfer
                var transferredCar = _platform.TransferOneCarOfTask(task, true);
                DebugLog($"Loaded car {transferredCar?.ID}");

                if (transferredCar == null)
                {
                    PJMain.Error("Tried to (un)load a car that wasn't there :(");
                    FailSafeTask(task);
                }

                return PlatformState.Loading;
            }

            // fail into safe state by completing task
            private void FailSafeTask(PlatformTask task)
            {
                task.State = TaskState.Done;
                foreach (Car car in task.Cars)
                {
                    if (task.IsLoadTask)
                    {
                        car.DumpCargo();
                        car.LoadCargo(car.capacity, CargoInjector.PassengerCargo.v1);
                    }
                    else
                    {
                        car.DumpCargo();
                    }
                }
                _platform.RemoveTask(task);
            }

            private void DebugLog(string message)
            {
#if DEBUG
                PJMain.Log($"Platform {_platform?.Id}: {message}");
#endif
            }
        }

        #endregion
    }
}
