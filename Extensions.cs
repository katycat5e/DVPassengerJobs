using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobsMod
{
    internal static class Extensions
    {
        internal static T GetRandomFromList<T>( this IList<T> list, Random rand, T toExclude = default )
        {
            if( list == null || list.Count == 0 ) return default;

            T result;

            do
            {
                int i = rand.Next(list.Count);
                result = list[i];
            }
            while( Equals(result, toExclude) );

            return result;
        }

        internal static List<T> ChooseMany<T>( this IList<T> source, Random rand, int count )
        {
            var result = new List<T>(count);

            for( int i = 0; i < count; i++ )
            {
                result.Add(GetRandomFromList(source, rand));
            }

            return result;
        }
    }
}
