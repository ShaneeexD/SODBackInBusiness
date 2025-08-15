using System;
using System.Collections.Generic;
using UnityEngine;

namespace BackInBusiness
{
    /// <summary>
    /// Utility class for managing AI goals and citizen state transitions
    /// </summary>
    public static class AIGoalManager
    {
        /// <summary>
        /// Resets a citizen's AI state and clears all goals
        /// </summary>
        /// <param name="citizen">The citizen to reset</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ResetAIState(Citizen citizen)
        {
            try
            {
                if (citizen == null || citizen.ai == null)
                {
                    Plugin.Logger.LogWarning("ResetAIState: Citizen or citizen.ai is null");
                    return false;
                }

                // Reset work state
                citizen.isAtWork = false;
                
                // Clear ALL existing goals
                if (citizen.ai.goals != null)
                {
                    // Create a copy of the goals to avoid modification during enumeration
                    // Use a standard C# List to avoid Il2Cpp conversion issues
                    var goalsToRemove = new List<NewAIGoal>();
                    
                    // Copy goals to our temporary list
                    foreach (NewAIGoal goal in citizen.ai.goals)
                    {
                        goalsToRemove.Add(goal);
                    }
                    
                    // Remove all goals
                    foreach (NewAIGoal goal in goalsToRemove)
                    {
                        try
                        {
                            goal.Remove();
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"Error removing goal: {ex.Message}");
                        }
                    }
                    
                    // Force update priorities to make AI reconsider goals
                    citizen.ai.AITick(true, false);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in ResetAIState: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// Makes a citizen unemployed and homeless
        /// </summary>
        /// <param name="citizen">The citizen to make unemployed and homeless</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool MakeUnemployedAndHomeless(Citizen citizen)
        {
            try
            {
                if (citizen == null)
                {
                    Plugin.Logger.LogWarning("MakeUnemployedAndHomeless: Citizen is null");
                    return false;
                }
                
                // Track the old home for logging
                var oldHome = citizen.home;
                string oldAddress = oldHome?.thisAsAddress?.name?.ToString() ?? "unknown address";
                
                // Make unemployed
                var unemployed = CitizenCreator.Instance.CreateUnemployed();
                citizen.SetJob(unemployed);
                
                // Make homeless
                if (citizen.home != null)
                {
                    citizen.SetResidence(null, true);
                    citizen.isHomeless = true;
                    
                    // Add to homeless directory if not already there
                    if (!CityData.Instance.homelessDirectory.Contains(citizen))
                    {
                        CityData.Instance.homelessDirectory.Add(citizen);
                        
                        // Remove from homed directory if present
                        if (CityData.Instance.homedDirectory.Contains(citizen))
                        {
                            CityData.Instance.homedDirectory.Remove(citizen);
                        }
                    }
                    
                    Plugin.Logger.LogInfo($"Citizen '{citizen.GetCitizenName()}' evicted from {oldAddress}.");
                }
                
                // Reset AI state and goals
                ResetAIState(citizen);
                
                // Try to generate new routine goals safely
                try
                {
                    citizen.GenerateRoutineGoals();
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Error generating routine goals: {ex.Message}");
                    // Continue even if this fails - we'll add our own goals below
                }
                
                // Add custom goals for homeless citizens
                if (citizen.isHomeless)
                {
                    AddHomelessGoals(citizen);
                }
                
                Plugin.Logger.LogInfo($"Citizen '{citizen.GetCitizenName()}' set to Unemployed and Homeless.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in MakeUnemployedAndHomeless: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// Adds goals for homeless citizens to make them move around the city
        /// </summary>
        /// <param name="citizen">The homeless citizen</param>
        /// <returns>True if goals were added, false otherwise</returns>
        public static bool AddHomelessGoals(Citizen citizen)
        {
            try
            {
                if (citizen == null || !citizen.isHomeless || citizen.ai == null)
                {
                    Plugin.Logger.LogWarning("AddHomelessGoals: Invalid citizen state");
                    return false;
                }
                
                if (RoutineControls.Instance == null || RoutineControls.Instance.toGoGoal == null)
                {
                    Plugin.Logger.LogWarning("AddHomelessGoals: RoutineControls.Instance or toGoGoal is null");
                    return false;
                }
                
                bool addedAnyGoals = false;
                
                // Find some public places for the citizen to visit
                // Create a list of potential public locations
                List<NewGameLocation> publicLocations = new List<NewGameLocation>();
                
                // First try to find streets
                foreach (NewGameLocation location in CityData.Instance.gameLocationDirectory)
                {
                    if (location != null && location.building != null && 
                        location.building.preset != null)
                    {
                        // Add streets to our list of potential locations
                        if (location.building.preset.presetName == "Street")
                        {
                            publicLocations.Add(location);
                            Plugin.Logger.LogInfo($"Found street location: {location.name}");
                        }
                    }
                }
                
                // Pick a random street if we found any
                NewGameLocation streetLocation = null;
                if (publicLocations.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, publicLocations.Count);
                    streetLocation = publicLocations[randomIndex];
                }
                
                // If we found a street, create a goal to go there
                if (streetLocation != null)
                {
                    Plugin.Logger.LogInfo($"Creating toGoGoal for {citizen.GetCitizenName()} to street: {streetLocation.name}");
                    NewNode safeNode = citizen.FindSafeTeleport(streetLocation, false, true);
                    if (safeNode != null)
                    {
                        // Higher priority (0.8) for streets to make them more likely to go there first
                        citizen.ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0.8f, 0f, safeNode, null, streetLocation, null, null, -2);
                        Plugin.Logger.LogInfo($"Successfully added street goal for {citizen.GetCitizenName()} at node {safeNode.name}");
                        addedAnyGoals = true;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Could not find safe node in {streetLocation.name} for {citizen.GetCitizenName()}");
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"No street locations found for {citizen.GetCitizenName()} to visit");
                }
                
                // Also try to find public buildings like City Hall, Park, etc.
                List<NewGameLocation> publicBuildings = new List<NewGameLocation>();
                foreach (NewGameLocation location in CityData.Instance.gameLocationDirectory)
                {
                    if (location != null && location.building != null && 
                        location.building.preset != null)
                    {
                        string presetName = location.building.preset.presetName;
                        // Add various public buildings to our list
                        if (presetName == "CityHall" || 
                            presetName == "Park" ||
                            presetName == "Hotel" ||
                            presetName == "AmericanDiner" ||
                            presetName == "ShantyTown" ||
                            presetName == "Townhouse" ||
                            presetName == "OneFIfthAve" ||
                            presetName == "BrandyNetherland")
                        {
                            publicBuildings.Add(location);
                            Plugin.Logger.LogInfo($"Found public building: {location.name} (Type: {presetName})");
                        }
                    }
                }
                
                // Pick a random public building if we found any
                NewGameLocation publicBuilding = null;
                if (publicBuildings.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, publicBuildings.Count);
                    publicBuilding = publicBuildings[randomIndex];
                }
                
                // If we found a public building, create a goal to go there
                if (publicBuilding != null)
                {
                    string buildingType = publicBuilding.building?.preset?.presetName ?? "unknown";
                    Plugin.Logger.LogInfo($"Creating toGoGoal for {citizen.GetCitizenName()} to {buildingType}: {publicBuilding.name}");
                    NewNode safeNode = citizen.FindSafeTeleport(publicBuilding, false, true);
                    if (safeNode != null)
                    {
                        // Slightly lower priority (0.7) for public buildings
                        citizen.ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0.7f, 0f, safeNode, null, publicBuilding, null, null, -2);
                        Plugin.Logger.LogInfo($"Successfully added public building goal for {citizen.GetCitizenName()} at node {safeNode.name}");
                        addedAnyGoals = true;
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Could not find safe node in {publicBuilding.name} for {citizen.GetCitizenName()}");
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning($"No public buildings found for {citizen.GetCitizenName()} to visit");
                }
                
                // Force AI to update priorities again after adding these goals
                if (addedAnyGoals)
                {
                    citizen.ai.AITick(true, false);
                }
                
                return addedAnyGoals;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in AddHomelessGoals: {ex}");
                return false;
            }
        }
    }
}
