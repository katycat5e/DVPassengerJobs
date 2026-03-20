using DV.Logic.Job;
using PassengerJobs.Injectors;
using System.Collections;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    internal enum PlatformState
    {
        WaitingForTrain,
        Unloading,
        Loading,
        Paused,
    }

    internal class PlatformControllerStateMachine : IEnumerator
    {
        public const int START_TRANSFER_DELAY = 5;
        public const float TRAIN_CHECK_INTERVAL = 1f;
        public const float LOAD_DELAY = 1f;

        private PlatformState _platformState = PlatformState.WaitingForTrain;
        private int _beginTransferCountdown;

        public object? Current => new WaitForSecondsRealtime(LOAD_DELAY);

        private readonly PlatformController _controller;
        private readonly IPlatformWrapper _platform;

        public PlatformControllerStateMachine(PlatformController controller)
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

                boardingFinished &= !((newState == PlatformState.Loading) || (newState == PlatformState.Unloading));
                if (boardingFinished)
                {
                    // Only send SIGN_EMPTY if we came from Unloading (final stop)
                    // If we came from Loading, send SIGN_DEPARTING (intermediate stop)
                    LocalizationKey finalState = (_platformState == PlatformState.Unloading)
                        ? LocalizationKey.SIGN_EMPTY
                        : LocalizationKey.SIGN_DEPARTING;

                    _controller.OnPlatformStateChange(null, finalState);
                    _controller.PlayBellSound();
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
            _beginTransferCountdown = START_TRANSFER_DELAY;
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
            _controller.OnPlatformStateChange(task.Job, LocalizationKey.SIGN_INCOMING_TRAIN);

            // perform transfer
            var transferredCar = _platform.TransferOneCarOfTask(task, false);
            DebugLog($"Unloaded car {transferredCar?.ID}");

            if (transferredCar == null)
            {
                PJMain.Error("Tried to (un)load a car that wasn't there :(");
                FailSafeTask(task);
            }
            else
            {
                _controller.OnCarTransferred(transferredCar, task.Cars.Count, false);
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
            _controller.OnPlatformStateChange(task.Job, LocalizationKey.SIGN_OUTGOING_TRAIN);

            // perform transfer
            var transferredCar = _platform.TransferOneCarOfTask(task, true);
            DebugLog($"Loaded car {transferredCar?.ID}");

            if (transferredCar == null)
            {
                PJMain.Error("Tried to (un)load a car that wasn't there :(");
                FailSafeTask(task);
            }
            else
            {
                _controller.OnCarTransferred(transferredCar, task.Cars.Count, true);
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
            PJMain.LogDebug($"Platform {_platform?.Id}: {message}");
#endif
        }
    }
}
