using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs
{
    internal static class Extensions
    {
        private static readonly Random R = new();

        public static T? PickOne<T>(this IList<T> values)
            where T : class
        {
            return (values.Count > 0) ? values[R.Next(0, values.Count)] : null;
        }

        public static T? PickOne<T>(this IEnumerable<T> values)
            where T : class
        {
            return PickOne(values.ToList());
        }

        public static IEnumerable<Track> GetUnusedTracks(this IEnumerable<Track> tracks)
        {
            const double RESERVED_THRESHOLD = YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION + YardTracksOrganizer.FLOATING_POINT_IMPRECISION_THRESHOLD;

            foreach (var track in tracks)
            {
                if ((YardTracksOrganizer.Instance.GetReservedSpace(track) <= RESERVED_THRESHOLD) && track.IsFree())
                {
                    yield return track;
                }
            }
        }
    }
}
