using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    class SkinManager_Patch
    {
        public static bool Enabled { get; private set; } = false;

        private delegate void ReplaceTextureDelegate( TrainCar car );
        private static ReplaceTextureDelegate SM_ReplaceTexture = null;
        private static Dictionary<string, string> CarStates = null;
        private static IDictionary SkinGroups = null;

        private static readonly List<string> RedCoachSkins = new List<string>();
        private static readonly List<string> GreenCoachSkins = new List<string>();
        private static readonly List<string> BlueCoachSkins = new List<string>();

        private static readonly System.Random Rand = new System.Random();

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
                    SkinGroups = AccessTools.Field(smType, "skinGroups")?.GetValue(null) as IDictionary;

                    if( (CarStates == null) || (SM_ReplaceTexture == null) )
                    {
                        PassengerJobs.ModEntry.Logger.Warning("SkinManager found, but failed to connect to members");
                    }
                    else
                    {
                        Enabled = true;
                        PassengerJobs.ModEntry.Logger.Log("SkinManager integration enabled");

                        SearchForNamedTrains();
                        GetPlainCoachSkinList();
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

        private static void SearchForNamedTrains()
        {
            string smFolder = UnityModManager.FindMod("SkinManagerMod").Path;
            string skinsFolderPath = Path.Combine(smFolder, "Skins");
            var skinsDir = new DirectoryInfo(skinsFolderPath);

            foreach( var passCarFolder in skinsDir.GetDirectories("CarPassenger*") )
            {
                foreach( var carSkinFolder in passCarFolder.GetDirectories() )
                {
                    string configPath = Path.Combine(carSkinFolder.FullName, "pj_specials.xml");
                    if( File.Exists(configPath) )
                    {
                        SpecialConsistManager.LoadConfig(configPath);
                    }
                }
            }
        }

        private static FieldInfo skinsField;
        private static FieldInfo skinNameField;

        private static void GetPlainCoachSkinList()
        {
            Type skinGroupType = AccessTools.TypeByName("SkinManagerMod.SkinGroup");
            Type skinType = AccessTools.TypeByName("SkinManagerMod.Skin");
            if( skinGroupType == null || skinType == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't get skin manager types");
                return;
            }

            skinsField = AccessTools.Field(skinGroupType, "skins");
            skinNameField = AccessTools.Field(skinType, "name");
            if( skinsField == null || skinNameField == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't get skin manager skin fields");
                return;
            }

            HashSet<string> blockList = GetBlockList(TrainCarType.PassengerRed);
            GetPlainSkinsForCoachType(RedCoachSkins, TrainCarType.PassengerRed, blockList);

            blockList = GetBlockList(TrainCarType.PassengerGreen);
            GetPlainSkinsForCoachType(GreenCoachSkins, TrainCarType.PassengerGreen, blockList);

            blockList = GetBlockList(TrainCarType.PassengerBlue);
            GetPlainSkinsForCoachType(BlueCoachSkins, TrainCarType.PassengerBlue, blockList);
        }

        private static HashSet<string> GetBlockList( TrainCarType carType )
        {
            return SpecialConsistManager.TrainDefinitions
                .Where(train => (train.CarType == carType) && train.ExpressOnly)
                .SelectMany(train => train.Skins)
                .ToHashSet();
        }

        private static void GetPlainSkinsForCoachType( List<string> dest, TrainCarType carType, HashSet<string> blockList )
        {
            object skinGroup = SkinGroups[carType];

            if( skinsField.GetValue(skinGroup) is IList skinList )
            {
                foreach( object skin in skinList )
                {
                    if( skinNameField.GetValue(skin) is string skinName )
                    {
                        if( !blockList.Contains(skinName) ) dest.Add(skinName);
                    }
                }
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't get skins list from skingroup");
                return;
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

        public static void SetConsistSkin( List<TrainCar> consist, string[] skinNames )
        {
            foreach( TrainCar car in consist )
            {
                CarStates[car.CarGUID] = skinNames.ChooseOne(Rand);
                SM_ReplaceTexture(car);
            }
        }

        public static void ApplyPlainSkins( List<TrainCar> consist )
        {
            foreach( TrainCar car in consist )
            {
                switch( car.carType )
                {
                    case TrainCarType.PassengerRed:
                        CarStates[car.CarGUID] = RedCoachSkins.ChooseOne(Rand);
                        break;

                    case TrainCarType.PassengerGreen:
                        CarStates[car.CarGUID] = GreenCoachSkins.ChooseOne(Rand);
                        break;

                    case TrainCarType.PassengerBlue:
                        CarStates[car.CarGUID] = BlueCoachSkins.ChooseOne(Rand);
                        break;
                }

                SM_ReplaceTexture(car);
            }
        }
    }
}
