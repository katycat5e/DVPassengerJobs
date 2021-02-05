using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PassengerJobsMod
{
    using SignComp = Tuple<string, TextRegionType>;

    public class SignPrinter : MonoBehaviour
    {
        public SignTextConfig[] Signs;

        private static readonly Dictionary<StationSignType, SignComp[]> SignPrefabComps =
            new Dictionary<StationSignType, SignComp[]>()
            {
                {
                    StationSignType.Small,
                    new SignComp[]
                    {
                        new SignComp("FrontText", TextRegionType.CompactDesc),
                        new SignComp("RearText", TextRegionType.CompactDesc)
                    }
                },
                {
                    StationSignType.Flatscreen,
                    new SignComp[]
                    {
                        new SignComp("FrontInfo", TextRegionType.PlatformInfo),
                        new SignComp("RearInfo", TextRegionType.PlatformInfo),
                        new SignComp("FrontText", TextRegionType.FullNameDesc),
                        new SignComp("RearText", TextRegionType.FullNameDesc)
                    }
                },
                {
                    StationSignType.Lillys,
                    new SignComp[]
                    {
                        new SignComp("FrontInfo", TextRegionType.PlatformInfo),
                        new SignComp("RearInfo", TextRegionType.PlatformInfo),
                        new SignComp("FrontDest", TextRegionType.FullNameDesc),
                        new SignComp("RearDest", TextRegionType.FullNameDesc),
                        new SignComp("FrontIDs", TextRegionType.JobID),
                        new SignComp("RearIDs", TextRegionType.JobID)
                    }
                }
            };

        public void Initialize( StationSignType signType )
        {
            SignComp[] comps = SignPrefabComps[signType];

            Signs = new SignTextConfig[comps.Length];
            for( int i = 0; i < comps.Length; i++ )
            {
                // find the text field with given name
                if( gameObject.transform.Find(comps[i].Item1)?.GetComponent<TextMeshPro>() is TextMeshPro tmp )
                {
                    Signs[i] = new SignTextConfig()
                    {
                        TextRenderer = tmp,
                        Type = comps[i].Item2
                    };
                    //PassengerJobs.ModEntry.Logger.Log($"Found comp {tmp.name}");
                }
                else
                {
                    Signs[i] = null;
                    PassengerJobs.ModEntry.Logger.Log($"Failed to find {comps[i].Item1} comp on {gameObject.name}, {signType}");
                }
            }
        }

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
            if( Signs == null ) return;
            foreach( var sign in Signs ) sign.UpdateText(input);
        }

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
            TrackId = string.Empty;
            TimeString = "12:00";
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
