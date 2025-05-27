using System;
using System.Collections.Generic;
using UnityEngine;

namespace BackInBusiness
{
    // Extended classes for business management UI
    
    // Class to store information about employees
    public class EmployeeData
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public float Salary { get; set; }
        public float Productivity { get; set; }
        
        public EmployeeData(string name, string role, float salary, float productivity)
        {
            Name = name;
            Role = role;
            Salary = salary;
            Productivity = productivity;
        }
    }
    
    // Class to store information about upgrades
    public class UpgradeData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public float Cost { get; set; }
        public float IncomeBoost { get; set; }
        public bool Purchased { get; set; }
        
        public UpgradeData(string name, string description, float cost, float incomeBoost)
        {
            Name = name;
            Description = description;
            Cost = cost;
            IncomeBoost = incomeBoost;
            Purchased = false;
        }
    }
    
    // Note: OwnedBusinessData class is defined elsewhere in the project
    
    // Extension methods for OwnedBusinessData
    public static class OwnedBusinessDataExtensions
    {
        private static Dictionary<string, List<EmployeeData>> _employeesByBusiness = new Dictionary<string, List<EmployeeData>>();
        private static Dictionary<string, List<UpgradeData>> _upgradesByBusiness = new Dictionary<string, List<UpgradeData>>();
        private static Dictionary<string, string> _businessNames = new Dictionary<string, string>();
        private static Dictionary<string, string> _businessLocations = new Dictionary<string, string>();
        
        // Get the business name
        public static string GetName(this OwnedBusinessData business)
        {
            if (business == null)
                return "Unknown Business";
                
            string businessId = business.GetBusinessId();
            
            // Check if we already have a cached name
            if (_businessNames.ContainsKey(businessId))
                return _businessNames[businessId];
                
            // Generate a default name based on the business ID
            string name = "Business " + businessId.Substring(0, Math.Min(8, businessId.Length));
            _businessNames[businessId] = name;
            
            return name;
        }
        
        // Get the business ID
        public static string GetBusinessId(this OwnedBusinessData business)
        {
            // This is a placeholder - in the actual implementation, you would
            // access the appropriate property from the OwnedBusinessData class
            // For now, we'll use the hash code as a unique identifier
            return business.GetHashCode().ToString();
        }
        
        // Get the business location
        public static string GetLocation(this OwnedBusinessData business)
        {
            if (business == null)
                return "Unknown Location";
                
            string businessId = business.GetBusinessId();
            
            // Check if we already have a cached location
            if (_businessLocations.ContainsKey(businessId))
                return _businessLocations[businessId];
                
            // Generate a default location
            string location = "Downtown";
            _businessLocations[businessId] = location;
            
            return location;
        }
        
        public static List<EmployeeData> GetEmployees(this OwnedBusinessData business)
        {
            string key = business.AddressId.ToString();
            if (!_employeesByBusiness.ContainsKey(key))
            {
                _employeesByBusiness[key] = new List<EmployeeData>();
            }
            return _employeesByBusiness[key];
        }
        
        public static List<UpgradeData> GetUpgrades(this OwnedBusinessData business)
        {
            string key = business.AddressId.ToString();
            if (!_upgradesByBusiness.ContainsKey(key))
            {
                _upgradesByBusiness[key] = new List<UpgradeData>();
                // Add some default upgrades
                _upgradesByBusiness[key].Add(new UpgradeData("Better Equipment", "Improves productivity by 10%", 5000, 0.1f));
                _upgradesByBusiness[key].Add(new UpgradeData("Marketing Campaign", "Increases income by 15%", 8000, 0.15f));
                _upgradesByBusiness[key].Add(new UpgradeData("Office Renovation", "Improves morale and increases income by 20%", 12000, 0.2f));
            }
            return _upgradesByBusiness[key];
        }
        
        public static float GetNetProfit(this OwnedBusinessData business)
        {
            // Calculate expenses based on employee count
            float expenses = business.EmployeeCount * 500; // Assume $500 per employee
            return business.DailyIncome - expenses;
        }
        
        public static string GetFriendlyBusinessTypeName(this OwnedBusinessData business)
        {
            // Get the friendly name from CompanyPresets
            CompanyPresets presets = new CompanyPresets();
            string businessType = business.Type.ToString();
            
            // Find the matching business type in the mapping
            foreach (var mapping in presets.CompanyPresetsMapping)
            {
                if (mapping.Item1 == businessType)
                {
                    return mapping.Item2;
                }
            }
            
            return businessType;
        }
    }
}
