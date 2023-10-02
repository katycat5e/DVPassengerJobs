using DV.Logic.Job;
using DV.WeatherSystem;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Platforms
{
    public abstract class SignFormatter
    {
        public static readonly SignFormatter PlatformInfo = new PlatformInfoFormatter();
        public static readonly SignFormatter JobId = new JobIdFormatter();
        public static readonly SignFormatter Compact = new CompactFormatter();
        public static readonly SignFormatter FullName = new FullNameFormatter();

        public abstract string Format(SignData data);
    }

    public sealed class PlatformInfoFormatter : SignFormatter
    {
        public override string Format(SignData data)
        {
            return $"{data.TrackId}\n{data.TimeString}";
        }
    }

    public sealed class JobIdFormatter : SignFormatter
    {
        public override string Format(SignData data)
        {
            if (data.Jobs == null || data.Jobs.Count == 0)
            {
                return string.Empty;
            }
            return string.Join("\n", data.Jobs.Take(2).Select(j => j.ID));
        }
    }

    public sealed class CompactFormatter : SignFormatter
    {
        public override string Format(SignData data)
        {
            // [ID] to/from [station]
            if (data.OverrideText != null)
            {
                return data.OverrideText;
            }

            if (data.Jobs == null || data.Jobs.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", GetJobNames(data.Jobs));
        }

        private static IEnumerable<string> GetJobNames(IEnumerable<SignData.JobInfo> jobs)
        {
            foreach (var job in jobs.Take(2))
            {
                if (job.Incoming)
                {
                    yield return LocalizationKey.SIGN_INCOMING_TRAIN.L(job.ID, job.Src);
                }
                else
                {
                    yield return LocalizationKey.SIGN_OUTGOING_TRAIN.L(job.ID, job.Dest);
                }
            }
        }
    }

    public sealed class FullNameFormatter : SignFormatter
    {
        public override string Format(SignData data)
        {
            // [Name] to/from [station]
            if (data.OverrideText != null)
            {
                return data.OverrideText;
            }

            if (data.Jobs == null || data.Jobs.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", GetJobNames(data.Jobs));
        }

        private static IEnumerable<string> GetJobNames(IEnumerable<SignData.JobInfo> jobs)
        {
            foreach (var job in jobs.Take(2))
            {
                if (job.Incoming)
                {
                    yield return LocalizationKey.SIGN_INCOMING_TRAIN.L(job.Name, job.Src);
                }
                else
                {
                    yield return LocalizationKey.SIGN_OUTGOING_TRAIN.L(job.Name, job.Dest);
                }
            }
        }
    }

    public readonly struct SignData
    {
        public readonly string TrackId;
        public readonly string TimeString;

        public readonly ICollection<JobInfo>? Jobs;
        public readonly string? OverrideText;

        public SignData(string track, string time, ICollection<JobInfo>? jobs, string? overrideText)
        {
            TrackId = track;
            TimeString = time;
            Jobs = jobs;
            OverrideText = overrideText;
        }

        public readonly struct JobInfo
        {
            public readonly bool Incoming;
            public readonly string Src;
            public readonly string Dest;
            public readonly string Name;
            public readonly string OriginalID;
            public readonly string ID;

            public JobInfo(Job job, bool incoming)
            {
                Incoming = incoming;
                OriginalID = job.ID;
                if (job.ID.Count(c => c == '-') > 1)
                {
                    ID = job.ID.Substring(job.ID.IndexOf('-') + 1); // remove yard ID
                }
                else
                {
                    ID = job.ID;
                }
                Name = GetTrainName(job);
                Src = job.chainData.chainOriginYardId;
                Dest = job.chainData.chainDestinationYardId;
            }

            private static string GetTrainName(Job job)
            {
                string jobId = job.ID;
                int lastDashIdx = jobId.LastIndexOf('-');
                string trainNum = jobId.Substring(lastDashIdx + 1);

                //if (ConsistManager.JobToSpecialMap.TryGetValue(job.ID, out SpecialTrain special))
                //{
                //    // Named Train
                //    return $"{special.Name} {trainNum}";
                //}

                //// Ordinary boring train
                //char jobTypeChar = (job.jobType == PassJobType.Commuter) ? 'C' : 'E';

                return LocalizationKey.SIGN_EXPRESS_NAME.L(trainNum);
            }
        }
    }
}
