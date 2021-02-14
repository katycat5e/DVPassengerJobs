using System;
using System.Reflection;
using HarmonyLib;

namespace PassengerJobsMod
{
    public static class DVTime_Patch
    {
        public static Func<DateTime> GetCurrentTime;

        public static void Initialize()
        {
            try
            {
                Assembly dvTimeAssembly = Assembly.Load("RedworkDE.DvTime");

                if( dvTimeAssembly?.GetType("CurrentTime") is Type currentTimeType )
                {
                    if( AccessTools.PropertyGetter(currentTimeType, "Time") is MethodInfo timeGetter )
                    {
                        GetCurrentTime = timeGetter.CreateDelegate(typeof(Func<DateTime>)) as Func<DateTime>;

                        if( GetCurrentTime != null )
                        {
                            PassengerJobs.ModEntry.Logger.Warning($"Found DVTime mod, enabling");
                            return;
                        }
                    }
                    else
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"unable to get CurrentTime.Time property");
                    }
                }
                else
                {
                    PassengerJobs.ModEntry.Logger.Warning($"unable to find DVTime CurrentTime type");
                }
            }
            catch( Exception )
            {
                PassengerJobs.ModEntry.Logger.Warning($"unable to find DVTime");
            }

            // Implement some kind of fallback strategy here
            GetCurrentTime = () => DateTime.Now;
        }
    }
}
