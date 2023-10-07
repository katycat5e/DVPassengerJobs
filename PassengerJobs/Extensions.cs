using DV.Logic.Job;
using PassengerJobs.Generation;
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

        public static void AddToFront<T>(this IList<T> list, T newItem)
        {
            if (list.Count == 0)
            {
                list.Add(newItem);
                return;
            }

            list.Add(list[list.Count - 1]);
            for (int i = list.Count - 1; i > 0; i--)
            {
                list[i] = list[i - 1];
            }
            list[0] = newItem;
        }

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

        public static T? PickOneValue<T>(this IList<T> values)
            where T : struct
        {
            return (values.Count > 0) ? values[R.Next(0, values.Count)] : null;
        }

        public static T? PickOneValue<T>(this IEnumerable<T> values)
            where T : struct
        {
            return PickOneValue(values.ToList());
        }

        const double RESERVED_THRESHOLD = YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION + YardTracksOrganizer.FLOATING_POINT_IMPRECISION_THRESHOLD;

        public static IEnumerable<Track> GetUnusedTracks(this IEnumerable<Track> tracks)
        {
            foreach (var track in tracks)
            {
                if ((YardTracksOrganizer.Instance.GetReservedSpace(track) <= RESERVED_THRESHOLD) && track.IsFree())
                {
                    yield return track;
                }
            }
        }

        public static IEnumerable<RouteTrack> GetUnusedTracks(this IEnumerable<RouteTrack> tracks)
        {
            foreach (var track in tracks)
            {
                if (track.IsSegment)
                {
                    yield return track;
                }
                else if ((YardTracksOrganizer.Instance.GetReservedSpace(track.Track) <= RESERVED_THRESHOLD) && track.Track.IsFree())
                {
                    yield return track;
                }
            }
        }

        public static RailTrack GetRailTrack(this Track track)
        {
            return LogicController.Instance.LogicToRailTrack[track];
        }
    }
}
