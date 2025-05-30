
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace BackInBusiness
{
    public class CompanyPresets
    {
        // Preset | Price | Social Credit Level
        public (string, int, int)[] CompanyPresetsList = {
            ("AmericanDiner", Plugin.AmericanDinerPrice.Value, Plugin.AmericanDinerSCL.Value),
            ("Ballroom", Plugin.BallroomPrice.Value, Plugin.BallroomPriceSCL.Value),
            ("Bar", Plugin.BarPrice.Value, Plugin.BarPriceSCL.Value),
            ("BlackmarketSyncClinic", Plugin.BlackmarketSyncClinicPrice.Value, Plugin.BlackmarketSyncClinicPriceSCL.Value),
            ("BlackmarketTrader", Plugin.BlackmarketTraderPrice.Value, Plugin.BlackmarketTraderPriceSCL.Value),
            ("BuildingManagement", Plugin.BuildingManagementPrice.Value, Plugin.BuildingManagementPriceSCL.Value),
            ("Chemist", Plugin.ChemistPrice.Value, Plugin.ChemistPriceSCL.Value),
            ("ChineseEatery", Plugin.ChineseEateryPrice.Value, Plugin.ChineseEateryPriceSCL.Value),
            ("CityHall", Plugin.CityHallPrice.Value, Plugin.CityHallPriceSCL.Value),
            ("EnforcerBranch", Plugin.EnforcerBranchPrice.Value, Plugin.EnforcerBranchPriceSCL.Value),
            ("FastFood", Plugin.FastFoodPrice.Value, Plugin.FastFoodPriceSCL.Value),
            ("GamblingDen", Plugin.GamblingDenPrice.Value, Plugin.GamblingDenPriceSCL.Value),
            ("HardwareStore", Plugin.HardwareStorePrice.Value, Plugin.HardwareStorePriceSCL.Value),
            ("HospitalWing", Plugin.HospitalWingPrice.Value, Plugin.HospitalWingPriceSCL.Value),
            ("IndustrialOffice", Plugin.IndustrialOfficePrice.Value, Plugin.IndustrialOfficePriceSCL.Value),
            ("IndustrialPlant", Plugin.IndustrialPlantPrice.Value, Plugin.IndustrialPlantPriceSCL.Value),
            ("Laboratory", Plugin.LaboratoryPrice.Value, Plugin.LaboratoryPriceSCL.Value),
            ("Landlord", Plugin.LandlordPrice.Value, Plugin.LandlordPriceSCL.Value),
            ("Launderette", Plugin.LaunderettePrice.Value, Plugin.LaunderettePriceSCL.Value),
            ("LoanShark", Plugin.LoanSharkPrice.Value, Plugin.LoanSharkPriceSCL.Value),
            ("MediumOffice", Plugin.MediumOfficePrice.Value, Plugin.MediumOfficePriceSCL.Value),
            ("PawnShop", Plugin.PawnShopPrice.Value, Plugin.PawnShopPriceSCL.Value),
            ("StreetFoodVendorSnacks", Plugin.StreetFoodVendorSnacksPrice.Value, Plugin.StreetFoodVendorSnacksPriceSCL.Value),
            ("Supermarket", Plugin.SupermarketPrice.Value, Plugin.SupermarketPriceSCL.Value),
            ("SyncClinic", Plugin.SyncClinicPrice.Value, Plugin.SyncClinicPriceSCL.Value),
            ("WeaponsDealer", Plugin.WeaponsDealerPrice.Value, Plugin.WeaponsDealerPriceSCL.Value),
            ("WorkplaceCanteen", Plugin.WorkplaceCanteenPrice.Value, Plugin.WorkplaceCanteenPriceSCL.Value)
        };

        public (string, string)[] CompanyPresetsMapping = {
            ("AmericanDiner", "American Diner"),
            ("Ballroom", "Hotel"),
            ("Bar", "Bar"),
            ("BlackmarketSyncClinic", "BM Sync Clinic"),
            ("BlackmarketTrader", "BM Trader"),
            ("BuildingManagement", "Building Management"),
            ("Chemist", "Pharmacy"),
            ("ChineseEatery", "Chinese Restaurant"),
            ("CityHall", "City Hall"),
            ("EnforcerBranch", "Enforcer Branch"),
            ("FastFood", "Fast Food"),
            ("GamblingDen", "Gambling Den"),
            ("HardwareStore", "Hardware Store"),
            ("HospitalWing", "Hospital Wing"),
            ("IndustrialOffice", "Industrial Office"),
            ("IndustrialPlant", "Industrial Plant"),
            ("Laboratory", "Laboratory"),
            ("Landlord", "Landlord"),
            ("Launderette", "Launderette"),
            ("LoanShark", "Loan Shark"),
            ("MediumOffice", "Office"),
            ("PawnShop", "Pawn Shop"),
            ("StreetFoodVendorSnacks", "Street Vendor"),
            ("Supermarket", "Supermarket"),
            ("SyncClinic", "Sync Clinic"),
            ("WeaponsDealer", "Weapons Dealer"),
            ("WorkplaceCanteen", "Workplace Canteen")
        };
    }
}