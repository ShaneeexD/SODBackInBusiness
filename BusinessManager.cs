using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using SOD.Common;
using Newtonsoft.Json;

namespace BackInBusiness
{
    public class BusinessManager
    {
        private static BusinessManager _instance;
        public static BusinessManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new BusinessManager();
                return _instance;
            }
        }

        // List of all owned businesses
        public List<BusinessData> OwnedBusinesses { get; private set; } = new List<BusinessData>();
        
        // Dictionary to quickly look up businesses by address ID
        private Dictionary<string, BusinessData> businessLookup = new Dictionary<string, BusinessData>();

        // Events
        public event Action<BusinessData> OnBusinessPurchased;
        public event Action<BusinessData> OnBusinessSold;
        public event Action<BusinessData> OnBusinessUpdated;
        public event Action<int> OnIncomeCollected;
        
        // Timer for income collection
        private float incomeCollectionTimer = 0f;
        private float incomeCollectionInterval = 300f; // 5 minutes in seconds
        private bool incomeCollectionActive = false;

        private BusinessManager()
        {
            // Initialize
            LoadBusinessData();
            
            // No need for MonoBehaviour components in IL2CPP
            Plugin.Logger.LogInfo("BusinessManager initialized");
        }

        // Purchase a new business
        public bool PurchaseBusiness(NewAddress address, string businessName, BusinessType type, int purchasePrice)
        {
            if (address == null)
                return false;

            // Check if we already own this business
            string addressId = address.id.ToString();
            if (businessLookup.ContainsKey(addressId))
                return false;

            // Create new business data
            BusinessData newBusiness = new BusinessData
            {
                AddressId = addressId.ToString(), // Convert int to string
                BusinessName = businessName,
                Type = type,
                PurchasePrice = purchasePrice,
                PurchaseDate = DateTime.Now,
                DailyIncome = CalculateBaseIncome(type, address),
                UpgradeLevel = 0,
                Employees = new List<string>(),
                CustomData = new Dictionary<string, object>()
            };

            // Add to collections
            OwnedBusinesses.Add(newBusiness);
            businessLookup[addressId] = newBusiness;

            // Save data
            SaveBusinessData();

            // Trigger event
            OnBusinessPurchased?.Invoke(newBusiness);

            return true;
        }

        // Sell a business
        public bool SellBusiness(string addressId, int sellingPrice)
        {
            if (!businessLookup.TryGetValue(addressId, out BusinessData business))
                return false;

            // Remove from collections
            OwnedBusinesses.Remove(business);
            businessLookup.Remove(addressId);

            // Save data
            SaveBusinessData();

            // Trigger event
            OnBusinessSold?.Invoke(business);

            return true;
        }

        // Get a business by address ID
        public List<NewAddress> GetBusinessByAddressId()
        {
            try
            {
                List<NewAddress> businesses = new List<NewAddress>();
                
                if (CityData.Instance == null)
                {
                    Plugin.Logger.LogError("CityData.Instance is null");
                    return businesses;
                }
                
                if (CityData.Instance.citizenDirectory == null)
                {
                    Plugin.Logger.LogError("CityData.Instance.citizenDirectory is null");
                    return businesses;
                }
                
                Plugin.Logger.LogInfo($"Found {CityData.Instance.citizenDirectory.Count} citizens");
                
                foreach (Citizen citizen in CityData.Instance.citizenDirectory)
                {
                    if (citizen == null)
                    {
                        continue;
                    }
                    
                    if (citizen.job == null)
                    {
                        continue;
                    }
                    
                    if (citizen.job.employer == null)
                    {
                        continue;
                    }
                    
                    if (citizen.job.employer.placeOfBusiness == null)
                    {
                        continue;
                    }
                    
                    if (citizen.job.employer.placeOfBusiness.thisAsAddress == null)
                    {
                        continue;
                    }
                    
                    try
                    {
                        if (citizen.home != null && citizen.home.thisAsAddress != null)
                        {
                            if (citizen.job.employer.placeOfBusiness.thisAsAddress.id == citizen.home.thisAsAddress.id)
                            {
                                string businessName = citizen.job.employer.placeOfBusiness.thisAsAddress.name?.ToString() ?? "Unknown";
                                string citizenName = citizen.name?.ToString() ?? "Unknown";
                                Plugin.Logger.LogInfo($"Skipping self-employed business at home: {businessName} for {citizenName}");
                                continue;
                            }
                        }
                        
                        string bName = citizen.job.employer.placeOfBusiness.thisAsAddress.name?.ToString() ?? "";
                        string cName = citizen.name?.ToString() ?? "";
                        
                        if (!string.IsNullOrEmpty(cName) && 
                            !string.IsNullOrEmpty(bName) && 
                            bName.Contains(cName))
                        {
                            Plugin.Logger.LogInfo($"Skipping potential self-employed business by name: {bName} for {cName}");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Error checking if business is self-employed: {ex.Message}");
                        // Continue anyway, don't break the loop
                    }
                    
                    // Check if we already have this business
                    if (businesses.Contains(citizen.job.employer.placeOfBusiness.thisAsAddress))
                    {
                        Plugin.Logger.LogInfo($"Skipping duplicate business {citizen.job.employer.placeOfBusiness.thisAsAddress.name?.ToString() ?? "Unknown"}");
                        continue;
                    }
                    
                    // Add the business to our list
                    businesses.Add(citizen.job.employer.placeOfBusiness.thisAsAddress);
                    Plugin.Logger.LogInfo($"Found business {citizen.job.employer.placeOfBusiness.thisAsAddress.name?.ToString() ?? "Unknown"}");
                }

                //Plugin.Logger.LogInfo($"Found {businesses.Count} unique businesses");
                return businesses;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in GetBusinessByAddressId: {ex.Message}\n{ex.StackTrace}");
                return new List<NewAddress>();
            }
        }

        // Check if an address is a business we own
        public bool IsOwnedBusiness(string addressId)
        {
            return businessLookup.ContainsKey(addressId);
        }

        // Update business data
        public void UpdateBusiness(BusinessData business)
        {
            if (business == null || !businessLookup.ContainsKey(business.AddressId))
                return;

            // Update in lookup
            businessLookup[business.AddressId] = business;

            // Save data
            SaveBusinessData();

            // Trigger event
            OnBusinessUpdated?.Invoke(business);
        }

        // Calculate daily income for a business
        public int CalculateDailyIncome(BusinessData business)
        {
            if (business == null)
                return 0;

            // Base income calculation
            float baseIncome = business.DailyIncome;
            
            // Apply upgrades
            float upgradeMultiplier = 1.0f + (business.UpgradeLevel * 0.2f);
            
            // Apply employee bonus
            float employeeBonus = business.Employees.Count * 10;
            
            // Calculate final income
            return Mathf.RoundToInt(baseIncome * upgradeMultiplier + employeeBonus);
        }

        // Calculate base income for a business type and location
        private int CalculateBaseIncome(BusinessType type, NewAddress address)
        {
            // Base income by business type
            int baseIncome = 0;
            switch (type)
            {
                case BusinessType.Restaurant:
                    baseIncome = 100;
                    break;
                case BusinessType.Bar:
                    baseIncome = 120;
                    break;
                case BusinessType.Shop:
                    baseIncome = 80;
                    break;
                case BusinessType.Office:
                    baseIncome = 150;
                    break;
                default:
                    baseIncome = 50;
                    break;
            }

            // Apply location modifiers (if address info is available)
            if (address != null)
            {
                // Example: Higher floors have better income
                int floorNumber = 1; // Default to first floor
                if (address.floor != null)
                {
                    // Just use a simple value based on whether floor exists
                    floorNumber = 2; // If we have a floor, assume it's at least the second floor
                }
                float floorMultiplier = 1.0f + (floorNumber * 0.05f);
                
                // Example: Better districts have better income
                // This would need to be expanded based on game data
                float districtMultiplier = 1.0f;
                
                baseIncome = Mathf.RoundToInt(baseIncome * floorMultiplier * districtMultiplier);
            }

            return baseIncome;
        }

        // Collect income from all businesses
        public int CollectAllIncome()
        {
            int totalIncome = 0;
            
            foreach (var business in OwnedBusinesses)
            {
                int income = CalculateDailyIncome(business);
                totalIncome += income;
                
                // Update last collection date
                business.LastIncomeCollection = DateTime.Now;
            }
            
            // Save data
            SaveBusinessData();
            
            return totalIncome;
        }

        // Save business data to file
        public void SaveBusinessData()
        {
            try
            {
                string saveFolder = Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "BackInBusiness");
                Directory.CreateDirectory(saveFolder);
                
                string savePath = Path.Combine(saveFolder, "businesses.json");
                string json = JsonConvert.SerializeObject(OwnedBusinesses, Formatting.Indented);
                File.WriteAllText(savePath, json);
                
                Plugin.Logger.LogInfo($"Saved {OwnedBusinesses.Count} businesses to {savePath}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to save business data: {ex.Message}");
            }
        }

        // Load business data from file
        public void LoadBusinessData()
        {
            try
            {
                string saveFolder = Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "BackInBusiness");
                string savePath = Path.Combine(saveFolder, "businesses.json");
                
                if (File.Exists(savePath))
                {
                    string json = File.ReadAllText(savePath);
                    OwnedBusinesses = JsonConvert.DeserializeObject<List<BusinessData>>(json) ?? new List<BusinessData>();
                    
                    // Rebuild lookup dictionary
                    businessLookup.Clear();
                    foreach (var business in OwnedBusinesses)
                    {
                        businessLookup[business.AddressId] = business;
                    }
                    
                    Plugin.Logger.LogInfo($"Loaded {OwnedBusinesses.Count} businesses from {savePath}");
                }
                else
                {
                    OwnedBusinesses = new List<BusinessData>();
                    businessLookup.Clear();
                    Plugin.Logger.LogInfo("No business data file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                OwnedBusinesses = new List<BusinessData>();
                businessLookup.Clear();
                Plugin.Logger.LogError($"Failed to load business data: {ex.Message}");
            }
        }
    }
    
    // We'll handle periodic updates through the SaveGamerHandlers class instead

    // Business types
    public enum BusinessType
    {
        Restaurant,
        Bar,
        Shop,
        Office,
        Other
    }

    // Business data class
    [Serializable]
    public class BusinessData
    {
        // Core data
        public string AddressId { get; set; }
        public string BusinessName { get; set; }
        public BusinessType Type { get; set; }
        public int PurchasePrice { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime LastIncomeCollection { get; set; }
        
        // Business stats
        public int DailyIncome { get; set; }
        public int UpgradeLevel { get; set; }
        public List<string> Employees { get; set; } = new List<string>();
        
        // Custom data for different business types
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }
}
