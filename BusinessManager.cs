using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SimpleJSON;
using SOD.Common;

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
        public List<OwnedBusinessData> OwnedBusinesses { get; private set; } = new List<OwnedBusinessData>();
        
        // Dictionary to quickly look up businesses by address ID
        private Dictionary<string, OwnedBusinessData> businessLookup = new Dictionary<string, OwnedBusinessData>();

        // Events
        public event Action<OwnedBusinessData> OnBusinessPurchased;
        public event Action<OwnedBusinessData> OnBusinessSold;
        public event Action<OwnedBusinessData> OnBusinessUpdated;
        public event Action<int> OnIncomeCollected;
        
        // Timer for income collection
        private float incomeCollectionTimer = 0f;
        private float incomeCollectionInterval = 300f; // 5 minutes in seconds
        private bool incomeCollectionActive = false;
        public int basePurchasePrice { get; private set; }
        public int finalPurchasePrice { get; private set; }
        public int employeeCount { get; private set; }
        public string floorName { get; private set; }

        private BusinessManager()
        {
            // Initialize
            LoadBusinessData();
            
            // No need for MonoBehaviour components in IL2CPP
            Plugin.Logger.LogInfo("BusinessManager initialized");
        }

        // Sell a business
        public bool SellBusiness(string addressId, int sellingPrice)
        {
            if (!businessLookup.TryGetValue(addressId, out OwnedBusinessData business))
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
        public List<BusinessInfo> GetBusinessByAddressId()
        {
            try
            {
                List<BusinessInfo> businesses = new List<BusinessInfo>();
                List<NewAddress> ownedBusinesses = new List<NewAddress>();
                Dictionary<NewAddress, bool> processedBusinesses = new Dictionary<NewAddress, bool>();
                
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

                foreach (NewAddress address in Player.Instance.apartmentsOwned)
                {
                    ownedBusinesses.Add(address);
                }
                
                Plugin.Logger.LogInfo($"Found {CityData.Instance.citizenDirectory.Count} citizens");
                
                foreach (Citizen citizen in CityData.Instance.citizenDirectory)
                {
                    if (citizen == null || 
                        citizen.job == null || 
                        citizen.job.employer == null || 
                        citizen.job.employer.placeOfBusiness == null || 
                        citizen.job.employer.placeOfBusiness.thisAsAddress == null)
                    {
                        continue;
                    }

                    NewAddress businessAddress = citizen.job.employer.placeOfBusiness.thisAsAddress;

                    if (ownedBusinesses.Contains(businessAddress))
                    {
                        Plugin.Logger.LogInfo($"Skipping owned business {businessAddress.name?.ToString()}");
                        continue;
                    }
                    
                    try
                    {
                        if (citizen.home != null && citizen.home.thisAsAddress != null)
                        {
                            if (businessAddress.id == citizen.home.thisAsAddress.id)
                            {
                                string businessName = businessAddress.name?.ToString() ?? "Unknown";
                                string citizenName = citizen.name?.ToString() ?? "Unknown";
                                Plugin.Logger.LogInfo($"Skipping self-employed business at home: {businessName} for {citizenName}");
                                continue;
                            }
                        }
                        
                        string bName = businessAddress.name?.ToString() ?? "";
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
                    if (processedBusinesses.ContainsKey(businessAddress))
                    {
                        Plugin.Logger.LogInfo($"Skipping duplicate business {businessAddress.name?.ToString() ?? "Unknown"}");
                        continue;
                    }
                    
                    // Mark this business as processed
                    processedBusinesses[businessAddress] = true;
                    
                    // Get employee count and floor name
                    int employeeCount = 0;
                    string floorName = "";
                    
                    try
                    {
                        if (businessAddress.company != null && businessAddress.company.companyRoster != null)
                        {
                            employeeCount = businessAddress.company.companyRoster.Count;
                        }
                        
                        if (businessAddress.floor != null)
                        {
                            floorName = businessAddress.floor.name;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"Error getting employee count or floor name: {ex.Message}");
                    }

                    // Create a BusinessData object and add it to our list
                    BusinessInfo businessData = new BusinessInfo(businessAddress, employeeCount, floorName);
                    businesses.Add(businessData);
                    
                    Plugin.Logger.LogInfo($"Found business {businessAddress.name?.ToString() ?? "Unknown"} with {employeeCount} employees on floor {floorName}");
                }

                Plugin.Logger.LogInfo($"Found {businesses.Count} unique businesses");
                return businesses;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in GetBusinessByAddressId: {ex.Message}\n{ex.StackTrace}");
                return new List<BusinessInfo>();
            }
        }

        // Check if an address is a business we own
        public bool IsOwnedBusiness(string addressId)
        {
            return businessLookup.ContainsKey(addressId);
        }

        // Update business data
        public void UpdateBusiness(OwnedBusinessData business)
        {
            if (business == null || !businessLookup.ContainsKey(business.AddressId.ToString()))
                return;

            // Update in lookup
            businessLookup[business.AddressId.ToString()] = business;

            // Save data
            SaveBusinessData();

            // Trigger event
            OnBusinessUpdated?.Invoke(business);
        }

        // Calculate daily income for a business
        public int CalculateDailyIncome(OwnedBusinessData business)
        {
            if (business == null)
                return 0;

            // Base income calculation
            float baseIncome = business.DailyIncome;
            
            // Apply upgrades
            float upgradeMultiplier = 1.0f + (business.UpgradeLevel * 0.2f);
            
            // Apply employee bonus
            float employeeBonus = business.EmployeeCount * 10;
            
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

        // Get the plugin directory path
        private string GetPluginDirectoryPath()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        
        private string GetCurrentSaveFileName()
        {
            if (RestartSafeController.Instance != null && RestartSafeController.Instance.saveStateFileInfo != null && !string.IsNullOrEmpty(RestartSafeController.Instance.saveStateFileInfo.Name))
            {
                string safeName = RestartSafeController.Instance.saveStateFileInfo.Name;
                // Sanitize the name to be path-friendly, though usually not an issue with SoD save names
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(invalidChar, '_');
                }
                return safeName;
            }
            Plugin.Instance.Log.LogInfo("No active save file name found, using default.");
            return "DefaultSave"; // Fallback save name
        }

        private string GetBusinessDataFilePath()
        {
            string pluginAssemblyDir = Path.GetDirectoryName(GetPluginDirectoryPath()); // e.g., ...\BepInEx\plugins\YourModName\
            string saveDataDirectory = Path.Combine(pluginAssemblyDir, "BIB-Data"); // e.g., ...\BepInEx\plugins\YourModName\Data\
            string fileName = $"{GetCurrentSaveFileName()}_businesses.json";
            return Path.Combine(saveDataDirectory, fileName);
        }

        // Save business data to file
        public void SaveBusinessData()
        {
            Plugin.Instance.Log.LogInfo($"Attempting to save {OwnedBusinesses.Count} businesses.");
            if (OwnedBusinesses == null)
            {
                Plugin.Instance.Log.LogWarning("OwnedBusinesses is null. Cannot save.");
                return;
            }

            try
            {
                string savePath = GetBusinessDataFilePath();
                string targetSaveDataDirectory = Path.GetDirectoryName(savePath);

                if (targetSaveDataDirectory == null)
                {
                    Plugin.Instance.Log.LogError("Could not determine target save data directory.");
                    return;
                }

                // Ensure the specific directory for the save file exists
                if (!Directory.Exists(targetSaveDataDirectory))
                {
                    Directory.CreateDirectory(targetSaveDataDirectory); 
                }

                JSONArray businessesArray = new JSONArray();
                foreach (var business in OwnedBusinesses)
                {
                    JSONObject businessJson = new JSONObject();
                    businessJson["AddressId"] = business.AddressId;
                    businessJson["BusinessName"] = business.BusinessName;
                    businessJson["Type"] = business.Type;
                    businessJson["PurchasePrice"] = business.PurchasePrice;
                    businessJson["PurchaseDate"] = business.PurchaseDate;
                    businessJson["LastIncomeCollection"] = business.LastIncomeCollection.ToString("o"); // ISO 8601 format
                    businessJson["DailyIncome"] = business.DailyIncome;
                    businessJson["EmployeeCount"] = business.EmployeeCount;
                    businessJson["FloorName"] = business.FloorName;

                    JSONArray upgradesArray = new JSONArray();
                    foreach (var upgrade in business.Upgrades)
                    {
                        JSONObject upgradeJson = new JSONObject();
                        upgradeJson["Name"] = upgrade.Name;
                        upgradeJson["Description"] = upgrade.Description; // Added Description
                        upgradeJson["Cost"] = upgrade.Cost;
                        upgradeJson["IncomeBoost"] = upgrade.IncomeBoost; // Added IncomeBoost
                        upgradeJson["Purchased"] = upgrade.Purchased; // Corrected to Purchased
                        upgradesArray.Add(upgradeJson);
                    }
                    businessJson["Upgrades"] = upgradesArray;

                    JSONObject customDataJson = new JSONObject();
                    if (business.CustomData != null)
                    {
                        foreach (var kvp in business.CustomData)
                        {
                            // SimpleJSON will handle basic types like string, int, bool, float, double by implicit conversion.
                            // For other types, you might need to convert them to a string or a supported SimpleJSON type.
                            if (kvp.Value is string || kvp.Value is int || kvp.Value is bool || kvp.Value is float || kvp.Value is double || kvp.Value == null)
                            {
                                customDataJson[kvp.Key] = new JSONString(kvp.Value?.ToString() ?? JSONNull.CreateOrGet().ToString());
                            }
                            else
                            {
                                // Fallback: store as string. You might need more sophisticated handling for complex types.
                                customDataJson[kvp.Key] = kvp.Value.ToString();
                                Plugin.Instance.Log.LogWarning($"CustomData value for key '{kvp.Key}' of type '{kvp.Value.GetType()}' was serialized as string.");
                            }
                        }
                    }
                    businessJson["CustomData"] = customDataJson;
                    businessesArray.Add(businessJson);
                }

                File.WriteAllText(savePath, businessesArray.ToString(4)); // Pretty print with 4 spaces indent
                Plugin.Instance.Log.LogInfo($"Business data saved successfully to {savePath} using SimpleJSON");
            }
            catch (Exception e)
            {
                Plugin.Instance.Log.LogError($"Failed to save business data using SimpleJSON: {e.Message}\n{e.StackTrace}");
            }
        }

        // Load business data from file
        public void LoadBusinessData()
        {
            string savePath = GetBusinessDataFilePath();
            Plugin.Instance.Log.LogInfo($"Attempting to load business data from: {savePath}");

            try
            {
                if (File.Exists(savePath))
                {
                    // Clear existing data
                    OwnedBusinesses.Clear();
                    businessLookup.Clear();
                    
                    // Read the file
                    string jsonString = File.ReadAllText(savePath);
                    
                    try 
                    {
                        // Use SimpleJSON for deserialization
                        JSONNode parsedJson = JSONNode.Parse(jsonString);
                        if (parsedJson == null || !parsedJson.IsArray)
                        {
                            Plugin.Instance.Log.LogWarning("Failed to parse JSON or root is not an array. Initializing new list.");
                            OwnedBusinesses = new List<OwnedBusinessData>(); 
                            businessLookup.Clear();
                            return;
                        }
                        JSONArray businessesArray = parsedJson.AsArray;

                        foreach (JSONNode businessNode in businessesArray.Children) // Iterate through children
                        {
                            if (!businessNode.IsObject) continue;
                            JSONObject businessJson = businessNode.AsObject;
                            {
                                OwnedBusinessData business = new OwnedBusinessData
                                {
                                    AddressId = businessJson["AddressId"].AsInt,
                                    BusinessName = businessJson["BusinessName"].Value,
                                    Type = businessJson["Type"].Value,
                                    PurchasePrice = businessJson["PurchasePrice"].AsInt,
                                    PurchaseDate = businessJson["PurchaseDate"].Value,
                                    LastIncomeCollection = DateTime.Parse(businessJson["LastIncomeCollection"].Value),
                                    DailyIncome = businessJson["DailyIncome"].AsInt,
                                    EmployeeCount = businessJson["EmployeeCount"].AsInt,
                                    FloorName = businessJson["FloorName"].Value,
                                    // Initialize lists for safety, though SimpleJSON should handle if they are missing in JSON
                                    Upgrades = new List<UpgradeData>(), 
                                    CustomData = new Dictionary<string, object>()
                                };

                                if (businessJson["Upgrades"] != null && businessJson["Upgrades"].IsArray) // Check for null and type
                                {
                                    JSONArray upgradesArray = businessJson["Upgrades"].AsArray;
                                    foreach (JSONNode upgradeNode in upgradesArray.Children) // Iterate through children
                                    {
                                        if (!upgradeNode.IsObject) continue;
                                        JSONObject upgradeJson = upgradeNode.AsObject;
                                        UpgradeData upgrade = new UpgradeData(
                                            upgradeJson["Name"].Value,
                                            upgradeJson["Description"].Value,
                                            upgradeJson["Cost"].AsFloat,
                                            upgradeJson["IncomeBoost"].AsFloat
                                        );
                                        upgrade.Purchased = upgradeJson["Purchased"].AsBool;
                                        business.Upgrades.Add(upgrade);
                                    }
                                }

                                if (businessJson["CustomData"] != null && businessJson["CustomData"].IsObject) // Check for null and type
                                {
                                    JSONObject customDataJson = businessJson["CustomData"].AsObject;
                                    foreach (KeyValuePair<string, JSONNode> kvp in customDataJson)
                                    {
                                        // For deserialization, kvp.Value can be JSONString, JSONNumber, JSONBool, etc.
                                        // Accessing .Value gives the string representation. For specific types:
                                        // kvp.Value.AsInt, kvp.Value.AsFloat, kvp.Value.AsBool can be used.
                                        // If you know the type, cast. Otherwise, storing as string or object is safer.
                                        business.CustomData[kvp.Key] = kvp.Value.Value; // Storing as string for now
                                    }
                                }
                                OwnedBusinesses.Add(business);
                                businessLookup[business.AddressId.ToString()] = business;
                            }
                        } // This closing brace matches the `foreach (JSONNode businessNode...`
                        
                        Plugin.Instance.Log.LogInfo($"Successfully loaded {OwnedBusinesses.Count} businesses from {savePath} using SimpleJSON");
                    } // This closing brace matches `try` for parsing
                    catch (Exception parseEx)
                    {
                        Plugin.Instance.Log.LogError($"Error parsing business data: {parseEx.Message}\n{parseEx.StackTrace}");
                        OwnedBusinesses = new List<OwnedBusinessData>();
                    }
                }
                else
                {
                    OwnedBusinesses = new List<OwnedBusinessData>(); 
                    businessLookup.Clear();
                    Plugin.Instance.Log.LogInfo("No business data file found, starting fresh");
                }
            }
            catch (Exception ex) // This is the outer catch for the whole LoadBusinessData method
            {
                OwnedBusinesses = new List<OwnedBusinessData>();
                businessLookup.Clear();
                Plugin.Instance.Log.LogError($"Failed to load business data: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        // Add a new business to owned businesses
        public void AddOwnedBusiness(NewAddress apartment, string businessName, int purchasePrice, string purchaseDate)
        {
            try
            {
                // Check if we already have this business
                string addressIdStr = apartment.id.ToString();
                if (businessLookup.ContainsKey(addressIdStr))
                {
                    Plugin.Logger.LogWarning($"Business with ID {addressIdStr} already exists in owned businesses");
                    return;
                }
                
                // Get business type from CompanyPresets
                string businessType = "Unknown";
                if (apartment.company != null && apartment.company.preset != null)
                {
                    string presetName = apartment.company.preset.name;
                    CompanyPresets companyPresets = new CompanyPresets();
                    
                    for (int i = 0; i < companyPresets.CompanyPresetsMapping.Length; i++)
                    {
                        if (presetName == companyPresets.CompanyPresetsMapping[i].Item1)
                        {
                            businessType = companyPresets.CompanyPresetsMapping[i].Item2;
                            break;
                        }
                    }
                }
                
                // Get employee count
                int employeeCount = 0;
                if (apartment.company != null && apartment.company.companyRoster != null)
                {
                    employeeCount = apartment.company.companyRoster.Count;
                }
                
                // Create new business data
                OwnedBusinessData business = new OwnedBusinessData
                {
                    AddressId = apartment.id,
                    BusinessName = businessName,
                    Type = businessType,
                    PurchasePrice = purchasePrice,
                    PurchaseDate = purchaseDate,
                    DailyIncome = 500, // Default income
                    EmployeeCount = employeeCount,
                    FloorName = apartment.floor?.name ?? "Unknown",
                    // CustomData = new Dictionary<string, object>() // Temporarily commented out
                };

                // Add to our collections
                OwnedBusinesses.Add(business);
                businessLookup[addressIdStr] = business;
                
                // Also add to the UI panel's list if it exists
                if (BusinessPanel.ownedBusinesses != null)
                {
                    BusinessPanel.ownedBusinesses.Add(business);
                }

                // Trigger the OnBusinessPurchased event
                OnBusinessPurchased?.Invoke(business);
                
                // Data will be saved by the game's save mechanism hook
                // SaveBusinessData(); 
                
                Plugin.Logger.LogInfo($"Successfully added business {businessName} (ID: {addressIdStr}) to owned businesses. Data will be saved on next game save.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error adding owned business: {ex.Message}\n{ex.StackTrace}");
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
    public class OwnedBusinessData
    {
        // Core data
        public int AddressId { get; set; }
        public string BusinessName { get; set; }
        public string Type { get; set; }
        public int PurchasePrice { get; set; }
        public string PurchaseDate { get; set; }
        public DateTime LastIncomeCollection { get; set; }
        
        // Business stats
        public int DailyIncome { get; set; }
        public int UpgradeLevel { get; set; }
        public int EmployeeCount { get; set; } // Renamed from EmployeesCount to match BusinessData class
        public string FloorName { get; set; }
        
        // Custom data for different business types
        public List<UpgradeData> Upgrades { get; set; } = new List<UpgradeData>();

        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }
}
