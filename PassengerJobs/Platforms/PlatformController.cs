using DV.Logic.Job;
using DV.WeatherSystem;
using PassengerJobs.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class PlatformController : MonoBehaviour
    {
        private static readonly AudioClip? _loadCompletedSound = null;
        private static readonly Dictionary<string, PlatformController> _trackToControllerMap = new();

        public static void PlayBellSound()
        {
            _loadCompletedSound.Play2D();
        }

        public static PlatformController GetControllerForTrack(string id)
        {
            return _trackToControllerMap[id];
        }

        public static bool TryGetControllerForTrack(string? id, out PlatformController? controller)
        {
            if (string.IsNullOrEmpty(id))
            {
                controller = null;
                return false;
            }

            return _trackToControllerMap.TryGetValue(id!, out controller);
        }

        public static void HandleGameUnloading()
        {
            foreach (var platform in _trackToControllerMap.Values)
            {
                Destroy(platform);
            }

            _trackToControllerMap.Clear();
        }

        public PassStationData.PlatformData PlatformData = null!;
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

        public event EventHandler<JobAddedArgs>? JobAdded;
        public event EventHandler<JobRemovedArgs>? JobRemoved;
        public event EventHandler<CarTransferredArgs>? CarTransferred;

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
            _trackToControllerMap[Platform.TrackId] = this;
            Signs = SignManager.CreatePlatformSigns(Platform.Id).ToArray();

            _stateMachine = new PlatformControllerStateMachine(this);
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

        public void SetDecorationsEnabled(bool enabled)
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

        public void RefreshDisplays()
        {
            _lastTimeString = CurrentTimeString;
            var data = new SignData(Platform.DisplayId, _lastTimeString, _currentJobs, _overrideText);

            foreach (var sign in Signs)
            {
                sign.UpdateDisplay(data);
            }
        }

        #endregion

        #region Job Handling

        public bool HasOutgoingJob => _currentJobs.Any(j => !j.Incoming);

        public void RegisterOutgoingJob(Job job, bool _ = false)
        {
            _currentJobs.Add(new SignData.JobInfo(job, false));
            job.JobCompleted += UnregisterOutgoingJob;
            job.JobAbandoned += UnregisterOutgoingJob;
            job.JobExpired += UnregisterOutgoingJob;

            OverrideText = null;

            JobAdded?.Invoke(this, new(job, false));
        }

        public void RegisterIncomingJob(Job job, bool _ = false)
        {
            // add incoming jobs to top of list
            _currentJobs.AddToFront(new SignData.JobInfo(job, true));
            job.JobCompleted += UnregisterIncomingJob;
            job.JobAbandoned += UnregisterIncomingJob;
            job.JobExpired += UnregisterIncomingJob;

            OverrideText = null;

            JobAdded?.Invoke(this, new(job, true));
        }

        public void UnregisterOutgoingJob(Job job)
        {
            _currentJobs.RemoveAll(j => j.OriginalID == job.ID);
            OverrideText = null;

            JobRemoved?.Invoke(this, new(job, _currentJobs.Count, false));
        }

        public void UnregisterIncomingJob(Job job)
        {
            _currentJobs.RemoveAll(j => j.OriginalID == job.ID);
            OverrideText = null;

            JobRemoved?.Invoke(this, new(job, _currentJobs.Count, true));
        }

        public void OnCarTransferred(Car car, int totalCarsInTrain, bool isLoading)
        {
            CarTransferred?.Invoke(this, new(car, totalCarsInTrain, isLoading));
        }

        #endregion

        #region Coroutines

        private PlatformControllerStateMachine _stateMachine = null!;
        private Coroutine? _loadUnloadRoutine = null;

        #endregion
    }
}
