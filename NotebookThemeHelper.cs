using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BackInBusiness
{
    /// <summary>
    /// Helper class to find and clone UI elements from the Detective's Notebook
    /// </summary>
    public static class NotebookThemeHelper
    {
        private static bool _attempted;
        public static Transform BackgroundTemplate; // The source Background to clone
        public static Transform MaskPatternTemplate; // The source MaskPattern to clone
        public static Sprite BackgroundSprite;
        public static Sprite MaskPatternSprite;

        private static string GetPath(Transform t)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                var cur = t;
                int guard = 0;
                while (cur != null && guard++ < 64)
                {
                    if (sb.Length == 0) sb.Insert(0, cur.name);
                    else sb.Insert(0, cur.name + "/");
                    cur = cur.parent;
                }
                return sb.ToString();
            }
            catch { return t != null ? t.name : "<null>"; }
        }

        private static bool IsAlive(UnityEngine.Object o) { return o != null; }

        private static void RescanTemplates()
        {
            try
            {
                try { Plugin.Logger.LogInfo("NotebookThemeHelper: rescan for Detective's Notebook UI elements due to missing/invalid templates..."); } catch { }
                BackgroundTemplate = null; MaskPatternTemplate = null; // clear cached pointers
                _attempted = false; // allow EnsureLoaded to run scan body
                EnsureLoaded();
            }
            catch { }
        }

        // Allow external callers to explicitly refresh the cache once UI is ready
        public static void ForceRescan()
        {
            RescanTemplates();
        }

        /// <summary>
        /// Find the Detective's Notebook background and mask pattern in the scene
        /// </summary>
        public static bool EnsureLoaded()
        {
            if (_attempted)
            {
                // Do not auto-rescan here to avoid repeated scan/log spam.
                // Call RescanTemplates() explicitly if a refresh is required.
                return (BackgroundTemplate != null && MaskPatternTemplate != null) || (BackgroundSprite != null && MaskPatternSprite != null);
            }
            _attempted = true;
            try
            {
                try { Plugin.Logger.LogInfo("NotebookThemeHelper: scanning scene for Detective's Notebook UI elements..."); } catch { }
                
                // Direct path lookup first
                var notebook = GameObject.Find("GameCanvas/WindowCanvas/Detective's Notebook");
                if (notebook != null)
                {
                    var background = notebook.transform.Find("Background");
                    var maskPattern = notebook.transform.Find("MaskPattern");
                    
                    if (background != null)
                    {
                        BackgroundTemplate = background;
                        try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Found Background at {GetPath(background)}"); } catch { }
                        
                        // Extract sprite for manual fallback usage
                        try
                        {
                            var img = background.GetComponent<Image>();
                            if (img != null) BackgroundSprite = img.sprite;
                        }
                        catch { }
                    }
                    
                    if (maskPattern != null)
                    {
                        MaskPatternTemplate = maskPattern;
                        try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Found MaskPattern at {GetPath(maskPattern)}"); } catch { }
                        
                        // Extract sprite for manual fallback usage
                        try
                        {
                            var img = maskPattern.GetComponent<Image>();
                            if (img != null) MaskPatternSprite = img.sprite;
                        }
                        catch { }
                    }
                }
                
                // If direct path failed, try scene search using heuristics similar to UIThemeCache
                if (BackgroundTemplate == null || MaskPatternTemplate == null)
                {
                    var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t == null || string.IsNullOrEmpty(t.name)) continue;
                        string nm = t.name;
                        bool looksBackground = string.Equals(nm, "Background", StringComparison.Ordinal);
                        bool looksMask = string.Equals(nm, "MaskPattern", StringComparison.Ordinal);
                        if (!looksBackground && !looksMask) continue;

                        // Prefer ones under a parent chain containing 'Detective' or 'Notebook'
                        bool prefer = false;
                        try
                        {
                            int depth = 0; var p = t.parent;
                            while (p != null && depth++ < 20)
                            {
                                var pnm = p.name ?? string.Empty;
                                // Require the exact notebook root name in the chain to avoid picking unrelated UIs
                                if (string.Equals(pnm, "Detective's Notebook", StringComparison.Ordinal))
                                { prefer = true; break; }
                                p = p.parent;
                            }
                        }
                        catch { }

                        if (looksBackground && (BackgroundTemplate == null || prefer))
                        {
                            // Validate expected child structure: Background should have no children
                            if (t.childCount == 0)
                            {
                                BackgroundTemplate = t;
                                try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Found Background via search at {GetPath(t)} (prefer={prefer})"); } catch { }
                                try { var img = t.GetComponent<Image>(); if (img != null) BackgroundSprite = img.sprite; } catch { }
                                if (prefer) { /* keep going to try to find matching mask */ }
                            }
                        }

                        if (looksMask && (MaskPatternTemplate == null || prefer))
                        {
                            // Validate expected child structure: MaskPattern should have a child named 'Pattern'
                            var patternChild = t.Find("Pattern");
                            if (patternChild != null)
                            {
                                MaskPatternTemplate = t;
                                try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Found MaskPattern via search at {GetPath(t)} (prefer={prefer})"); } catch { }
                                try { var img = t.GetComponent<Image>(); if (img != null) MaskPatternSprite = img.sprite; } catch { }
                            }
                        }

                        if (BackgroundTemplate != null && MaskPatternTemplate != null)
                            break;
                    }

                    // If we found only one, try to pair it with its sibling under the same parent
                    try
                    {
                        if (BackgroundTemplate != null && MaskPatternTemplate == null)
                        {
                            var parent = BackgroundTemplate.parent;
                            if (parent != null)
                            {
                                var sib = parent.Find("MaskPattern");
                                if (sib != null && sib.Find("Pattern") != null)
                                {
                                    MaskPatternTemplate = sib;
                                    try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Paired MaskPattern from sibling at {GetPath(sib)}"); } catch { }
                                    try { var img = sib.GetComponent<Image>(); if (img != null) MaskPatternSprite = img.sprite; } catch { }
                                }
                            }
                        }

                        if (MaskPatternTemplate != null && BackgroundTemplate == null)
                        {
                            var parent = MaskPatternTemplate.parent;
                            if (parent != null)
                            {
                                var sib = parent.Find("Background");
                                if (sib != null && sib.childCount == 0)
                                {
                                    BackgroundTemplate = sib;
                                    try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Paired Background from sibling at {GetPath(sib)}"); } catch { }
                                    try { var img = sib.GetComponent<Image>(); if (img != null) BackgroundSprite = img.sprite; } catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                return (BackgroundTemplate != null && MaskPatternTemplate != null) || (BackgroundSprite != null && MaskPatternSprite != null);
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"NotebookThemeHelper.EnsureLoaded error: {ex.Message}"); } catch { }
                return false;
            }
        }
        
        /// <summary>
        /// Clone the Detective's Notebook background and add it to the parent transform
        /// </summary>
        public static GameObject InstantiateBackground(Transform parent)
        {
            try
            {
                if (!EnsureLoaded()) return null;
                if (!IsAlive(BackgroundTemplate) && BackgroundSprite == null) return null;
                
                GameObject inst;
                if (IsAlive(BackgroundTemplate))
                {
                    inst = UnityEngine.Object.Instantiate(BackgroundTemplate.gameObject);
                    try { Plugin.Logger.LogInfo("NotebookThemeHelper: Background from Template clone path"); } catch { }
                    // Ensure no unexpected children remain on Background
                    try {
                        var t = inst.transform;
                        if (t.childCount > 0)
                        {
                            var toRemove = new System.Collections.Generic.List<GameObject>();
                            for (int i = 0; i < t.childCount; i++) toRemove.Add(t.GetChild(i).gameObject);
                            for (int i = 0; i < toRemove.Count; i++) UnityEngine.Object.Destroy(toRemove[i]);
                            Plugin.Logger?.LogInfo("NotebookThemeHelper: Pruned unexpected children from Background clone");
                        }
                    } catch { }
                }
                else
                {
                    // Fallback: build from sprite (avoid params Type[] ctor for IL2CPP)
                    inst = new GameObject("BIB_Background");
                    var rtCreate = inst.AddComponent<RectTransform>();
                    var img = inst.AddComponent<Image>();
                    img.sprite = BackgroundSprite;
                    img.type = Image.Type.Sliced;
                    img.preserveAspect = false;
                    try { Plugin.Logger.LogInfo("NotebookThemeHelper: Background from Sprite fallback path"); } catch { }
                }
                inst.name = "BIB_Background";
                var rt = inst.GetComponent<RectTransform>();
                if (rt == null) rt = inst.AddComponent<RectTransform>();
                
                rt.SetParent(parent, false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.SetAsFirstSibling(); // Put at the back
                
                // Sanitize
                RemoveUnwantedComponents(inst);
                try { if (!inst.activeSelf) inst.SetActive(true); } catch { }
                
                try { Plugin.Logger.LogInfo("NotebookThemeHelper: Instantiated Background under EmployeeDetailsView."); } catch { }
                return inst;
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"NotebookThemeHelper.InstantiateBackground error: {ex.Message}"); } catch { }
                return null;
            }
        }
        
        /// <summary>
        /// Clone the Detective's Notebook mask pattern and add it to the parent transform
        /// </summary>
        public static GameObject InstantiateMaskPattern(Transform parent)
        {
            try
            {
                if (!EnsureLoaded()) return null;
                if (!IsAlive(MaskPatternTemplate) && MaskPatternSprite == null) return null;
                
                GameObject inst;
                if (IsAlive(MaskPatternTemplate))
                {
                    inst = UnityEngine.Object.Instantiate(MaskPatternTemplate.gameObject);
                    try { Plugin.Logger.LogInfo("NotebookThemeHelper: MaskPattern from Template clone path"); } catch { }
                    // Keep only the 'Pattern' child, remove others like loading icons or gallery items
                    try {
                        var t = inst.transform;
                        var toRemove = new System.Collections.Generic.List<GameObject>();
                        for (int i = 0; i < t.childCount; i++)
                        {
                            var ch = t.GetChild(i);
                            if (!string.Equals(ch.name, "Pattern", StringComparison.Ordinal)) toRemove.Add(ch.gameObject);
                        }
                        for (int i = 0; i < toRemove.Count; i++) UnityEngine.Object.Destroy(toRemove[i]);
                        if (toRemove.Count > 0) Plugin.Logger?.LogInfo($"NotebookThemeHelper: Removed {toRemove.Count} unexpected children from MaskPattern clone");
                    } catch { }
                }
                else
                {
                    // Fallback: build from sprite (avoid params Type[] ctor for IL2CPP)
                    inst = new GameObject("BIB_MaskPattern");
                    var rtCreate = inst.AddComponent<RectTransform>();
                    var img = inst.AddComponent<Image>();
                    img.sprite = MaskPatternSprite;
                    img.type = Image.Type.Sliced; // pattern often tiles; sliced keeps corners right if borders set
                    img.preserveAspect = false;
                    var col = img.color; col.a = 1f; img.color = col;
                    try { Plugin.Logger.LogInfo("NotebookThemeHelper: MaskPattern from Sprite fallback path"); } catch { }
                }
                inst.name = "BIB_MaskPattern";
                var rt = inst.GetComponent<RectTransform>();
                if (rt == null) rt = inst.AddComponent<RectTransform>();
                
                rt.SetParent(parent, false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.SetSiblingIndex(1); // Put just above background but below content
                
                // Sanitize
                RemoveUnwantedComponents(inst);
                
                try { Plugin.Logger.LogInfo("NotebookThemeHelper: Instantiated MaskPattern under EmployeeDetailsView."); } catch { }
                return inst;
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"NotebookThemeHelper.InstantiateMaskPattern error: {ex.Message}"); } catch { }
                return null;
            }
        }
        
        /// <summary>
        /// Remove components that might interfere with our UI
        /// </summary>
        private static void RemoveUnwantedComponents(GameObject go)
        {
            try
            {
                // Remove canvases and raycasters to avoid nesting issues
                var canvases = go.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases) UnityEngine.Object.Destroy(canvas);
                
                var raycasters = go.GetComponentsInChildren<GraphicRaycaster>(true);
                foreach (var raycaster in raycasters) UnityEngine.Object.Destroy(raycaster);
                
                // Remove masking components which can hide visuals when detached from original hierarchy
                try {
                    var rectMasks = go.GetComponentsInChildren<RectMask2D>(true);
                    foreach (var m in rectMasks) UnityEngine.Object.Destroy(m);
                } catch { }
                try {
                    var masks = go.GetComponentsInChildren<Mask>(true);
                    foreach (var m in masks) UnityEngine.Object.Destroy(m);
                } catch { }

                // Remove any event systems or triggers
                try {
                    var eventSystems = go.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
                    foreach (var es in eventSystems) UnityEngine.Object.Destroy(es);
                } catch { }
                
                try {
                    var eventTriggers = go.GetComponentsInChildren<EventTrigger>(true);
                    foreach (var et in eventTriggers) UnityEngine.Object.Destroy(et);
                } catch { }

                // Normalize visuals: clear special materials, force alpha to 1, and disable raycast targets
                try {
                    var images = go.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        images[i].raycastTarget = false; // preserve color/material
                    }
                } catch { }
                try {
                    var raws = go.GetComponentsInChildren<RawImage>(true);
                    for (int i = 0; i < raws.Length; i++)
                    {
                        raws[i].raycastTarget = false; // preserve color/material
                    }
                } catch { }
                
                // Remove or neutralize CanvasGroups (can be alpha 0 on templates)
                try {
                    var groups = go.GetComponentsInChildren<CanvasGroup>(true);
                    for (int i = 0; i < groups.Length; i++)
                    {
                        var g = groups[i];
                        // Safer to destroy to avoid parent alpha influence
                        UnityEngine.Object.Destroy(g);
                    }
                } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"NotebookThemeHelper.RemoveUnwantedComponents error: {ex.Message}");
            }
        }
        
        // Second GetPath method removed to avoid duplication
    }
}
