using System;
using System.Collections;
using System.Collections.Generic;
using SkinManagerMod;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityModManagerNet;

namespace PassengerJobsMod
{
    internal class SkinManager_Patch
    {
        public static bool Enabled { get; private set; } = false;
        public static UnityModManager.ModEntry ModEntry { get; private set; } = null;

        private static readonly Dictionary<TrainCarType, List<string>> NonExpressSkins = new Dictionary<TrainCarType, List<string>>();

        public static void Initialize()
        {
            Enabled = false;

            ModEntry = UnityModManager.FindMod("SkinManagerMod");
            if (ModEntry == null || !ModEntry.Active)
            {
                PassengerJobs.Log("SkinManager not found, skipping integration");
                return;
            }

            SearchForNamedTrains();
            GetPlainCoachSkinList();

            PassengerJobs.Log("SkinManager integration enabled");
            Enabled = true;
        }

        private static void SearchForNamedTrains()
        {
            foreach (TrainCarType passCarType in ConsistManager.PassCarTypes)
            {
                foreach (var skin in SkinManager.GetSkinsForType(passCarType, false))
                {
                    if (string.IsNullOrEmpty(skin.Path)) continue;

                    string configPath = Path.Combine(skin.Path, "pj_specials.xml");
                    if (File.Exists(configPath))
                    {
                        ConsistManager.LoadConfig(configPath);
                    }
                }
            }
        }

        private static void GetPlainCoachSkinList()
        {
            HashSet<string> blockList;
            
            foreach (TrainCarType passCarType in ConsistManager.PassCarTypes)
            {
                blockList = GetExpressSkins(passCarType);
                var plainSkinList = GetNonExpressCoachSkins(passCarType, blockList);
                NonExpressSkins[passCarType] = plainSkinList;
            }
        }

        private static HashSet<string> GetExpressSkins( TrainCarType carType )
        {
            return ConsistManager.TrainDefinitions
                .SelectMany(train => train.Skins)
                .Where(skin => (skin.CarType == carType) && skin.ExpressOnly)
                .Select(skin => skin.Name)
                .ToHashSet();
        }

        private static List<string> GetNonExpressCoachSkins(TrainCarType carType, HashSet<string> blockList)
        {
            var nonExpress = new List<string>();

            foreach (Skin skin in SkinManager.GetSkinsForType(carType))
            {
                if (!blockList.Contains(skin.Name))
                {
                    nonExpress.Add(skin.Name);
                }
            }

            return nonExpress;
        }

        public static void UnifyConsist( List<TrainCar> consist )
        {
            if( consist.Count <= 1 ) return;

            var skin = SkinManager.GetCurrentCarSkin(consist.First());
            if (skin != null)
            {
                PassengerJobs.ModEntry.Logger.Log($"Unifying consist skins to \"{skin.Name}\": {string.Join(", ", consist.Select(c => c.ID))}");

                for( int i = 1; i < consist.Count; i++ )
                {
                    SkinManager.ApplySkin(consist[i], skin);
                }
            }
        }

        public static void ApplyConsistSkins( List<TrainCar> consist, List<string> skinNames )
        {
            for( int i = 0; i < consist.Count; i++ )
            {
                var skin = SkinManager.FindSkinByName(consist[i].carType, skinNames[i]);
                SkinManager.ApplySkin(consist[i], skin);
            }
        }

        public static void ApplyPlainSkins( List<TrainCar> consist )
        {
            foreach( TrainCar car in consist )
            {
                if (NonExpressSkins.TryGetValue(car.carType, out List<string> skinChoices))
                {
                    string choice = skinChoices.ChooseOne();
                    var skin = SkinManager.FindSkinByName(car.carType, choice);
                    SkinManager.ApplySkin(car, skin);
                }
            }
        }
    }
}
