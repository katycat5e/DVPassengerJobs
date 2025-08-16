using DV.Logic.Job;
using System;

namespace PassengerJobs.Platforms
{
    public class JobAddedArgs : EventArgs
    {
        public readonly Job Job;
        public readonly bool Incoming;
        public bool Outgoing => !Incoming;

        public JobAddedArgs(Job job, bool incoming)
        {
            Job = job;
            Incoming = incoming;
        }
    }

    public class JobRemovedArgs : EventArgs
    {
        public readonly Job Job;
        public readonly int RemainingJobs;
        public readonly bool Incoming;
        public bool Outgoing => !Incoming;

        public JobRemovedArgs(Job job, int remainingJobs, bool incoming)
        {
            Job = job;
            RemainingJobs = remainingJobs;
            Incoming = incoming;
        }
    }

    public class CarTransferredArgs : EventArgs
    {
        public readonly Car Car;
        public readonly int TotalCarCount;
        public readonly bool IsLoading;

        public CarTransferredArgs(Car car, int totalCarCount, bool isLoading)
        {
            Car = car;
            TotalCarCount = totalCarCount;
            IsLoading = isLoading;
        }
    }
}
