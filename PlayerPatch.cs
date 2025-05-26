using HarmonyLib;
using BepInEx.Unity.IL2CPP.UnityEngine;
using UnityEngine;
using UnityEngine.UI;
using static CaseComponent;
using static InteractableController;
using System.Collections;
using System.Reflection;
using System;
using UnityStandardAssets.Characters.FirstPerson;
using UnityEngine.EventSystems;
using Rewired.Demos;
using SOD.Common;

namespace BackInBusiness
{
    [HarmonyPatch(typeof(Player), "Update")]
    public class PlayerPatch
    {
        // Track if the B key was pressed in the previous frame to avoid repeated toggling
        private static bool wasKeyPressed = false;
        
        // References to game objects
        public static SessionData sessionData;
        public static InputController inputController;
        public static Game game = new Game();
        public static Player player = Player.Instance;

        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            try
            {
                // Check for B key press to toggle the business UI
                bool isKeyPressed = Input.GetKeyInt(BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.B);
                
                // Only toggle on key down (when it transitions from not pressed to pressed)
                if (isKeyPressed && !wasKeyPressed)
                {
                    // Make sure we're not in a menu,in bed or paused
                    if (MainMenuController.Instance.mainMenuActive == false && __instance.isInBed == false)
                    {
                        ToggleBusinessUI();
                    }
                }
                
                // Update the key state for the next frame
                wasKeyPressed = isKeyPressed;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in PlayerPatch.Prefix: {ex.Message}\n{ex.StackTrace}");
            }

            if(!BusinessUIManager.uiEnabled && !MainMenuController.Instance.mainMenuActive && !Paused.IsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // Toggle the business UI
        private static void ToggleBusinessUI()
        {
            try
            {
                // The new implementation handles both creation and toggling
                BusinessUIManager.Instance.ToggleBusinessUI();
                Plugin.Logger.LogInfo("Toggled business UI");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error toggling business UI: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}