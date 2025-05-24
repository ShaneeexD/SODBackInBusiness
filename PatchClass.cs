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
        public static ConfigEntry<bool> exampleConfigVariable;
        public static ManualLogSource Logger;
        public static Plugin Instance { get; private set; }

        public override void Load()
        {
            Instance = this;
            Logger = Log;
            
            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
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
            
            exampleConfigVariable = Config.Bind("General", "ExampleConfigVariable", false, new ConfigDescription("Example config description."));
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
    }
}