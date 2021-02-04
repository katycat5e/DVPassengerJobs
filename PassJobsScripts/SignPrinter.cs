using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PassengerJobsMod
{
    public class SignPrinter : MonoBehaviour
    {
        public SignTextConfig[] Signs;

        private static string JobIdFormat( SignData d )
        {
            if( d.Jobs == null || d.Jobs.Length == 0 ) return string.Empty;
            return string.Join("\n", d.Jobs.Take(2).Select(j => j.ID));
        }

        private static string CompactFormat( SignData d )
        {
            if( d.Jobs == null || d.Jobs.Length == 0 ) return string.Empty;
            return string.Join("\n", d.Jobs.Take(2).Select(j => (j.Incoming) ? $"{j.ID} from {j.Src}" : $"{j.ID} to {j.Dest}"));
        }

        private static string TrainDescFormat( SignData d )
        {
            if( d.Jobs == null || d.Jobs.Length == 0 ) return string.Empty;
            return string.Join("\n", d.Jobs.Take(2).Select(j => (j.Incoming) ? $"{j.Name} from {j.Src}" : $"{j.Name} to {j.Dest}"));
        }

        private static readonly Func<SignData, string>[] Formatters =
        {
            d => $"{d.TrackId}\n{d.TimeString}",
            JobIdFormat,
            CompactFormat,
            TrainDescFormat
        };

        public void UpdateDisplay( SignData input )
        {
            foreach( var sign in Signs ) sign.UpdateText(input);
        }

        [Serializable]
        public class SignTextConfig
        {
            public TextMeshPro TextRenderer;
            public TextRegionType Type;

            public void UpdateText( SignData data )
            {
                TextRenderer.text = Formatters[(int)Type](data);
            }
        }
    }

    public enum TextRegionType : int
    {
        PlatformInfo = 0,
        JobID = 1,
        CompactDesc = 2,
        FullNameDesc = 3
    }
    
    public class SignData
    {
        public string TrackId;
        public string TimeString;

        public JobInfo[] Jobs = null;

        public SignData()
        {

        }

        public SignData( string track, string time )
        {
            TrackId = track;
            TimeString = time;
        }

        public class JobInfo
        {
            public bool Incoming;
            public string Src;
            public string Dest;
            public string Name;
            public string ID;
        }
    }
}
