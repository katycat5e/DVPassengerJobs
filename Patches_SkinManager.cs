using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace PassengerJobsMod
{
    class SkinManager_Patch
    {
        public static bool Enabled { get; private set; } = false;

        private delegate void ReplaceTextureDelegate( TrainCar car );
        private static ReplaceTextureDelegate SM_ReplaceTexture = null;
        private static Dictionary<string, string> CarStates = null;

        public static void Initialize()
        {
            try
            {
                Assembly smAssembly = Assembly.Load("SkinManagerMod");
                Type smType = smAssembly?.GetType("SkinManagerMod.Main");

                if( smType != null )
                {
                    SM_ReplaceTexture = AccessTools.Method(smType, "ReplaceTexture")?.CreateDelegate(typeof(ReplaceTextureDelegate)) as ReplaceTextureDelegate;
                    CarStates = AccessTools.Field(smType, "trainCarState")?.GetValue(null) as Dictionary<string, string>;

                    if( (CarStates == null) || (SM_ReplaceTexture == null) )
                    {
                        PassengerJobs.ModEntry.Logger.Warning("SkinManager found, but failed to connect to members");
                    }
                    else
                    {
                        Enabled = true;
                        PassengerJobs.ModEntry.Logger.Log("SkinManager integration enabled");
                    }
                }
                else
                {
                    PassengerJobs.ModEntry.Logger.Log("SkinManager not found, skipping integration");
                }
            }
            catch( Exception ex ) when( ex is FileNotFoundException || ex is BadImageFormatException )
            {
                PassengerJobs.ModEntry.Logger.Log("SkinManager not found, skipping integration");
            }
            catch( Exception ex )
            {
                PassengerJobs.ModEntry.Logger.Log($"Error while trying to connect with SkinManager:\n{ex.Message}");
            }
        }

        public static void UnifyConsist( List<TrainCar> consist )
        {
            if( consist.Count <= 1 ) return;

            if( CarStates.TryGetValue(consist[0].CarGUID, out string skinName) )
            {
                PassengerJobs.ModEntry.Logger.Log($"Unifying consist skins to \"{skinName}\": {string.Join(", ", consist.Select(c => c.ID))}");

                for( int i = 1; i < consist.Count; i++ )
                {
                    CarStates[consist[i].CarGUID] = skinName;
                    SM_ReplaceTexture(consist[i]);
                }
            }
        }
    }
}
