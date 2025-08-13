using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.Config;
using SOD.Common; // For Lib
using SOD.Common.Helpers; // For Lib.SaveGame (potentially, or other helpers)
using Il2CppInterop.Runtime.Injection;

namespace BackInBusiness
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(SOD.Common.Plugin.PLUGIN_GUID)]
    [BepInDependency("RealEstateListingCruncherApp", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BasePlugin
    {
        public const string PLUGIN_GUID = "ShaneeexD.BackInBusiness";
        public const string PLUGIN_NAME = "BackInBusiness";
        public const string PLUGIN_VERSION = "1.0.0";

        //Use social credit level?
        public static ConfigEntry<bool> useSocialCreditLevel;
        //Business Prices
        public static ConfigEntry<int> AmericanDinerPrice;
        public static ConfigEntry<int> BallroomPrice;
        public static ConfigEntry<int> BarPrice;
        public static ConfigEntry<int> BlackmarketSyncClinicPrice;
        public static ConfigEntry<int> BlackmarketTraderPrice;
        public static ConfigEntry<int> BuildingManagementPrice;
        public static ConfigEntry<int> ChemistPrice;
        public static ConfigEntry<int> ChineseEateryPrice;
        public static ConfigEntry<int> CityHallPrice;
        public static ConfigEntry<int> EnforcerBranchPrice;
        public static ConfigEntry<int> FastFoodPrice;
        public static ConfigEntry<int> GamblingDenPrice;
        public static ConfigEntry<int> HardwareStorePrice;
        public static ConfigEntry<int> HospitalWingPrice;
        public static ConfigEntry<int> IndustrialOfficePrice;
        public static ConfigEntry<int> IndustrialPlantPrice;
        public static ConfigEntry<int> LaboratoryPrice;
        public static ConfigEntry<int> LandlordPrice;
        public static ConfigEntry<int> LaunderettePrice;
        public static ConfigEntry<int> LoanSharkPrice;
        public static ConfigEntry<int> MediumOfficePrice;
        public static ConfigEntry<int> PawnShopPrice;
        public static ConfigEntry<int> StreetFoodVendorSnacksPrice;
        public static ConfigEntry<int> SupermarketPrice;
        public static ConfigEntry<int> SyncClinicPrice;
        public static ConfigEntry<int> WeaponsDealerPrice;
        public static ConfigEntry<int> WorkplaceCanteenPrice;

        //Business Social Credit Requirement
        public static ConfigEntry<int> AmericanDinerSCL;
        public static ConfigEntry<int> BallroomPriceSCL;
        public static ConfigEntry<int> BarPriceSCL;
        public static ConfigEntry<int> BlackmarketSyncClinicPriceSCL;
        public static ConfigEntry<int> BlackmarketTraderPriceSCL;
        public static ConfigEntry<int> BuildingManagementPriceSCL;
        public static ConfigEntry<int> ChemistPriceSCL;
        public static ConfigEntry<int> ChineseEateryPriceSCL;
        public static ConfigEntry<int> CityHallPriceSCL;
        public static ConfigEntry<int> EnforcerBranchPriceSCL;
        public static ConfigEntry<int> FastFoodPriceSCL;
        public static ConfigEntry<int> GamblingDenPriceSCL;
        public static ConfigEntry<int> HardwareStorePriceSCL;
        public static ConfigEntry<int> HospitalWingPriceSCL;
        public static ConfigEntry<int> IndustrialOfficePriceSCL;
        public static ConfigEntry<int> IndustrialPlantPriceSCL;
        public static ConfigEntry<int> LaboratoryPriceSCL;
        public static ConfigEntry<int> LandlordPriceSCL;
        public static ConfigEntry<int> LaunderettePriceSCL;
        public static ConfigEntry<int> LoanSharkPriceSCL;
        public static ConfigEntry<int> MediumOfficePriceSCL;
        public static ConfigEntry<int> PawnShopPriceSCL;
        public static ConfigEntry<int> StreetFoodVendorSnacksPriceSCL;
        public static ConfigEntry<int> SupermarketPriceSCL;
        public static ConfigEntry<int> SyncClinicPriceSCL;
        public static ConfigEntry<int> WeaponsDealerPriceSCL;
        public static ConfigEntry<int> WorkplaceCanteenPriceSCL;
        public static ManualLogSource Logger;
        public static Plugin Instance { get; private set; }

        public override void Load()
        {
            Instance = this;
            Logger = Log;
            
            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            // Register managed MonoBehaviours for IL2CPP so AddComponent<T>() works
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<BackInBusiness.BIBButtonController>();
                Logger.LogInfo("Registered Il2Cpp type: BIBButtonController");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to register BIBButtonController: {ex}");
            }
            // CloseButtonAdapter is deprecated in favor of BIBButtonController
            
            // Initialize UniverseLib first
            Logger.LogInfo("Initializing UniverseLib...");
            Universe.Init(5.0f, OnUniverseInit, OnUniverseLog, new UniverseLibConfig()
            {
                Disable_EventSystem_Override = true,
                Force_Unlock_Mouse = false,
                Allow_UI_Selection_Outside_UIBase = true,
            });
            
            SaveGamerHandlers.Logger = Logger;
            var saveGamerHandlers = new SaveGamerHandlers();
            
            Logger.LogInfo("SaveGamerHandlers initialized");
            
            useSocialCreditLevel = Config.Bind("General", "UseSocialCreditLevel", true, new ConfigDescription("Use social credit level for purchase ability."));

            //Prices
            AmericanDinerPrice = Config.Bind("Prices", "AmericanDinerPrice", 5000, new ConfigDescription("Price of American Diner."));
            BallroomPrice = Config.Bind("Prices", "BallroomPrice", 100000, new ConfigDescription("Price of Hotel."));
            BarPrice = Config.Bind("Prices", "BarPrice", 5000, new ConfigDescription("Price of Bar."));
            BlackmarketSyncClinicPrice = Config.Bind("Prices", "BlackmarketSyncClinicPrice", 20000, new ConfigDescription("Price of Blackmarket Sync Clinic."));
            BlackmarketTraderPrice = Config.Bind("Prices", "BlackmarketTraderPrice", 25000, new ConfigDescription("Price of Blackmarket Trader."));
            BuildingManagementPrice = Config.Bind("Prices", "BuildingManagementPrice", 30000, new ConfigDescription("Price of Building Management."));
            ChemistPrice = Config.Bind("Prices", "ChemistPrice", 35000, new ConfigDescription("Price of Chemist."));
            ChineseEateryPrice = Config.Bind("Prices", "ChineseEateryPrice", 7500, new ConfigDescription("Price of Chinese Eatery."));
            CityHallPrice = Config.Bind("Prices", "CityHallPrice", 100000, new ConfigDescription("Price of City Hall."));
            EnforcerBranchPrice = Config.Bind("Prices", "EnforcerBranchPrice", 50000, new ConfigDescription("Price of Enforcer Branch."));
            FastFoodPrice = Config.Bind("Prices", "FastFoodPrice", 3500, new ConfigDescription("Price of Fast Food."));
            GamblingDenPrice = Config.Bind("Prices", "GamblingDenPrice", 30000, new ConfigDescription("Price of Gambling Den."));
            HardwareStorePrice = Config.Bind("Prices", "HardwareStorePrice", 25000, new ConfigDescription("Price of Hardware Store."));
            HospitalWingPrice = Config.Bind("Prices", "HospitalWingPrice", 50000, new ConfigDescription("Price of Hospital Wing."));
            IndustrialOfficePrice = Config.Bind("Prices", "IndustrialOfficePrice", 30000, new ConfigDescription("Price of Industrial Office."));
            IndustrialPlantPrice = Config.Bind("Prices", "IndustrialPlantPrice", 15000, new ConfigDescription("Price of Industrial Plant."));
            LaboratoryPrice = Config.Bind("Prices", "LaboratoryPrice", 120000, new ConfigDescription("Price of Laboratory."));
            LandlordPrice = Config.Bind("Prices", "LandlordPrice", 90000, new ConfigDescription("Price of Landlord."));
            LaunderettePrice = Config.Bind("Prices", "LaunderettePrice", 2500, new ConfigDescription("Price of Launderette."));
            LoanSharkPrice = Config.Bind("Prices", "LoanSharkPrice", 35000, new ConfigDescription("Price of Loan Shark."));
            MediumOfficePrice = Config.Bind("Prices", "MediumOfficePrice", 25000, new ConfigDescription("Price of Medium Office."));
            PawnShopPrice = Config.Bind("Prices", "PawnShopPrice", 15000, new ConfigDescription("Price of Pawn Shop."));
            StreetFoodVendorSnacksPrice = Config.Bind("Prices", "StreetFoodVendorSnacksPrice", 1500, new ConfigDescription("Price of Street Food Vendor Snacks."));
            SupermarketPrice = Config.Bind("Prices", "SupermarketPrice", 15000, new ConfigDescription("Price of Supermarket."));
            SyncClinicPrice = Config.Bind("Prices", "SyncClinicPrice", 50000, new ConfigDescription("Price of Sync Clinic."));
            WeaponsDealerPrice = Config.Bind("Prices", "WeaponsDealerPrice", 30000, new ConfigDescription("Price of Weapons Dealer."));
            WorkplaceCanteenPrice = Config.Bind("Prices", "WorkplaceCanteenPrice", 2000, new ConfigDescription("Price of Workplace Canteen."));
            
            //Social Credit Requirements
            AmericanDinerSCL = Config.Bind("SocialCreditRequirements", "AmericanDinerSCL", 2, new ConfigDescription("Social Credit Level required to purchase American Diner."));
            BallroomPriceSCL = Config.Bind("SocialCreditRequirements", "BallroomPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Hotel."));
            BarPriceSCL = Config.Bind("SocialCreditRequirements", "BarPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Bar."));
            BlackmarketSyncClinicPriceSCL = Config.Bind("SocialCreditRequirements", "BlackmarketSyncClinicPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Blackmarket Sync Clinic."));
            BlackmarketTraderPriceSCL = Config.Bind("SocialCreditRequirements", "BlackmarketTraderPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Blackmarket Trader."));
            BuildingManagementPriceSCL = Config.Bind("SocialCreditRequirements", "BuildingManagementPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Building Management."));
            ChemistPriceSCL = Config.Bind("SocialCreditRequirements", "ChemistPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Chemist."));
            ChineseEateryPriceSCL = Config.Bind("SocialCreditRequirements", "ChineseEateryPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Chinese Eatery."));
            CityHallPriceSCL = Config.Bind("SocialCreditRequirements", "CityHallPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase City Hall."));
            EnforcerBranchPriceSCL = Config.Bind("SocialCreditRequirements", "EnforcerBranchPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Enforcer Branch."));
            FastFoodPriceSCL = Config.Bind("SocialCreditRequirements", "FastFoodPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Fast Food."));
            GamblingDenPriceSCL = Config.Bind("SocialCreditRequirements", "GamblingDenPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Gambling Den."));
            HardwareStorePriceSCL = Config.Bind("SocialCreditRequirements", "HardwareStorePriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Hardware Store."));
            HospitalWingPriceSCL = Config.Bind("SocialCreditRequirements", "HospitalWingPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Hospital Wing."));
            IndustrialOfficePriceSCL = Config.Bind("SocialCreditRequirements", "IndustrialOfficePriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Industrial Office."));
            IndustrialPlantPriceSCL = Config.Bind("SocialCreditRequirements", "IndustrialPlantPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Industrial Plant."));
            LaboratoryPriceSCL = Config.Bind("SocialCreditRequirements", "LaboratoryPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Laboratory."));
            LandlordPriceSCL = Config.Bind("SocialCreditRequirements", "LandlordPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Landlord."));
            LaunderettePriceSCL = Config.Bind("SocialCreditRequirements", "LaunderettePriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Launderette."));
            LoanSharkPriceSCL = Config.Bind("SocialCreditRequirements", "LoanSharkPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Loan Shark."));
            MediumOfficePriceSCL = Config.Bind("SocialCreditRequirements", "MediumOfficePriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Medium Office."));
            PawnShopPriceSCL = Config.Bind("SocialCreditRequirements", "PawnShopPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Pawn Shop."));
            StreetFoodVendorSnacksPriceSCL = Config.Bind("SocialCreditRequirements", "StreetFoodVendorSnacksPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Street Food Vendor Snacks."));
            SupermarketPriceSCL = Config.Bind("SocialCreditRequirements", "SupermarketPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Supermarket."));
            SyncClinicPriceSCL = Config.Bind("SocialCreditRequirements", "SyncClinicPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Sync Clinic."));
            WeaponsDealerPriceSCL = Config.Bind("SocialCreditRequirements", "WeaponsDealerPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Weapons Dealer."));
            WorkplaceCanteenPriceSCL = Config.Bind("SocialCreditRequirements", "WorkplaceCanteenPriceSCL", 2, new ConfigDescription("Social Credit Level required to purchase Workplace Canteen."));
            
            // Subscribe to game save event
            Logger.LogInfo("Subscribing to Lib.SaveGame.OnAfterSave event.");
            Lib.SaveGame.OnAfterSave += OnGameSaveComplete;
        }
        
        private void OnUniverseInit()
        {
            Logger.LogInfo("UniverseLib initialized, creating business UI");
            // Don't create UI here - let the player press B to create it when needed
        }
        
        private void OnUniverseLog(string message, LogType logType)
        {
            Logger.LogInfo($"[UniverseLib] {message}");
        }

        private void OnGameSaveComplete(object sender, EventArgs e)
        {
            // EventArgs might not have IsNewGame or SaveGameName directly.
            // We'll log a generic message and then save.
            Logger.LogInfo("Game save detected. Saving business data.");
            try
            {
                BusinessManager.Instance.SaveBusinessData();
                Logger.LogInfo("Business data saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving business data: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }
    }
}