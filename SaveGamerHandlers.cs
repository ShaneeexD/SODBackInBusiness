using System;
using BepInEx.Logging;
using SOD.Common;
using SOD.Common.Helpers;
using BackInBusiness;
public class SaveGamerHandlers
{
    public static ManualLogSource Logger;

    public SaveGamerHandlers()
    {
        Lib.SaveGame.OnAfterLoad += HandleGameLoaded;
        Lib.SaveGame.OnAfterNewGame += HandleNewGameStarted;
    }   

    private void HandleNewGameStarted(object sender, EventArgs e)
    {
        try
        {
            // Load business data to ensure it exists
            BusinessManager.Instance.LoadBusinessData();
            
            // Wait a bit before trying to get businesses
            // The game might not have fully initialized all data yet
            BackInBusiness.Plugin.Logger.LogInfo("New game started - Will scan for businesses later");
            
            // We'll scan for businesses in the HandleGameLoaded method
        }
        catch (Exception ex)
        {
            BackInBusiness.Plugin.Logger.LogError($"Error in HandleNewGameStarted: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void HandleGameLoaded(object sender, EventArgs e)
    {
        try
        {
            // Load business data
            BusinessManager.Instance.LoadBusinessData();
            
            // Give the game a moment to initialize all data
            BackInBusiness.Plugin.Logger.LogInfo("Game loaded - Scanning for businesses");
            
            // Scan for businesses
            var businesses = BusinessManager.Instance.GetBusinessByAddressId();
            BackInBusiness.Plugin.Logger.LogInfo($"Found {businesses.Count} businesses in the city");
            
            // Log some details about the businesses
            foreach (var business in businesses)
            {
                if (business != null && business.name != null)
                {
                    BackInBusiness.Plugin.Logger.LogInfo($"Business: {business.name}, ID: {business.id}");
                }
            }
        }
        catch (Exception ex)
        {
            BackInBusiness.Plugin.Logger.LogError($"Error in HandleGameLoaded: {ex.Message}\n{ex.StackTrace}");
        }
    }
}