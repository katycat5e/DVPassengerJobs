﻿using DV.Logic.Job;
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