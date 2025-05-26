using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BackInBusiness;

namespace BackInBusiness
{
    public class Paused
    {
        public static bool IsPaused { get; set; }
    }
    
    [HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("PauseGame")]
    public class PauseManager
    {
        public static void Prefix()
        {
            Paused.IsPaused = true;
        }
    }

    [HarmonyPatch(typeof(SessionData))]
    [HarmonyPatch("ResumeGame")]
    public class ResumeGameManager
    {
        public static void Prefix() 
        {
            Paused.IsPaused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}