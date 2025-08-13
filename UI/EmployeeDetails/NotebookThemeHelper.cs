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
        public static Transform ContentsPageTemplate; // The source ContentsPage to clone
        public static Sprite BackgroundSprite;
        public static Sprite MaskPatternSprite;
        public static Sprite ContentsPageSprite;

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
                // Clear all cached pointers
                BackgroundTemplate = null; 
                MaskPatternTemplate = null; 
                ContentsPageTemplate = null;
                ContentsPageSprite = null;
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
                bool hasBasicElements = (BackgroundTemplate != null && MaskPatternTemplate != null) || 
                                       (BackgroundSprite != null && MaskPatternSprite != null);
                
                // Log ContentsPage status but don't affect the return value
                if (ContentsPageTemplate == null && ContentsPageSprite == null)
                {
                    Plugin.Logger?.LogWarning("NotebookThemeHelper: ContentsPage template and sprite are both null");
                }
                
                return hasBasicElements;
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
                    
                    // Find ContentsPage in the Detective's Notebook
                    try
                    {
                        // Path: GameCanvas/WindowCanvas/Detective's Notebook/Page/Scroll View/Viewport/History/ContentsPage
                        var page = notebook.transform.Find("Page");
                        if (page != null)
                        {
                            var scrollView = page.Find("Scroll View");
                            if (scrollView != null)
                            {
                                var viewport = scrollView.Find("Viewport");
                                if (viewport != null)
                                {
                                    var history = viewport.Find("History");
                                    if (history != null)
                                    {
                                        var contentsPage = history.Find("ContentsPage");
                                        if (contentsPage != null)
                                        {
                                            ContentsPageTemplate = contentsPage;
                                            Plugin.Logger?.LogInfo($"NotebookThemeHelper: Found ContentsPage at {GetPath(contentsPage)}");
                                            
                                            // Extract sprite for manual fallback usage
                                            try
                                            {
                                                var img = contentsPage.GetComponent<Image>();
                                                if (img != null) ContentsPageSprite = img.sprite;
                                                Plugin.Logger?.LogInfo($"NotebookThemeHelper: Extracted ContentsPage sprite: {(img != null ? (img.sprite != null ? img.sprite.name : "<null sprite>") : "<null image>")}.");
                                            }
                                            catch (Exception ex) { Plugin.Logger?.LogWarning($"Error extracting ContentsPage sprite: {ex.Message}"); }
                                        }
                                        else
                                        {
                                            Plugin.Logger?.LogWarning("NotebookThemeHelper: ContentsPage not found under History");
                                        }
                                    }
                                    else
                                    {
                                        Plugin.Logger?.LogWarning("NotebookThemeHelper: History not found under Viewport");
                                    }
                                }
                                else
                                {
                                    Plugin.Logger?.LogWarning("NotebookThemeHelper: Viewport not found under Scroll View");
                                }
                            }
                            else
                            {
                                Plugin.Logger?.LogWarning("NotebookThemeHelper: Scroll View not found under Page");
                            }
                        }
                        else
                        {
                            Plugin.Logger?.LogWarning("NotebookThemeHelper: Page not found under Detective's Notebook");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger?.LogWarning($"Error finding ContentsPage: {ex.Message}");
                    }
                }
                
                // If direct path failed, try scene search using heuristics similar to UIThemeCache
                if (BackgroundTemplate == null || MaskPatternTemplate == null || ContentsPageTemplate == null)
                {
                    var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t == null || string.IsNullOrEmpty(t.name)) continue;
                        string nm = t.name;
                        bool looksBackground = string.Equals(nm, "Background", StringComparison.Ordinal);
                        bool looksMask = string.Equals(nm, "MaskPattern", StringComparison.Ordinal);
                        bool looksContentsPage = string.Equals(nm, "ContentsPage", StringComparison.Ordinal);
                        if (!looksBackground && !looksMask && !looksContentsPage) continue;

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

                        if (looksContentsPage && (ContentsPageTemplate == null || prefer))
                        {
                            // Validate that this is a ContentsPage with an Image component
                            var img = t.GetComponent<Image>();
                            if (img != null && img.sprite != null)
                            {
                                ContentsPageTemplate = t;
                                ContentsPageSprite = img.sprite;
                                try { Plugin.Logger.LogInfo($"NotebookThemeHelper: Found ContentsPage via search at {GetPath(t)} (prefer={prefer})"); } catch { }
                            }
                        }

                        if (BackgroundTemplate != null && MaskPatternTemplate != null && ContentsPageTemplate != null)
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
                
                // Also ensure ContentsPage is loaded
                if (ContentsPageTemplate == null && ContentsPageSprite == null)
                {
                    FindContentsPage();
                }
                
                // Return true if we have either templates or sprites for the required UI elements
                bool hasBackground = BackgroundTemplate != null || BackgroundSprite != null;
                bool hasMaskPattern = MaskPatternTemplate != null || MaskPatternSprite != null;
                bool hasContentsPage = ContentsPageTemplate != null || ContentsPageSprite != null;
                
                // Log the status of each UI element
                Plugin.Logger?.LogInfo($"NotebookThemeHelper: UI elements status - Background: {hasBackground}, MaskPattern: {hasMaskPattern}, ContentsPage: {hasContentsPage}");
                
                // We need at least Background and MaskPattern to proceed
                return (hasBackground && hasMaskPattern);
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
                    var col = img.color; col.a = 1f; img.color = col;
                    try { Plugin.Logger.LogInfo("NotebookThemeHelper: Background from Sprite fallback path"); } catch { }
                }
                inst.name = "BIB_Background";
                // Apply dark grey tint (almost black) to the background visual
                try {
                    var imgBg = inst.GetComponent<Image>();
                    if (imgBg != null)
                    {
                        imgBg.color = new Color(0.10f, 0.10f, 0.10f, 1f); // very dark grey
                    }
                    else
                    {
                        var raw = inst.GetComponent<RawImage>();
                        if (raw != null) raw.color = new Color(0.10f, 0.10f, 0.10f, 1f);
                    }
                } catch { }
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
                // Use the same approach as the game's Detective's Notebook
                try {
                    // We want a very dark color for the pattern
                    Color almostBlack = new Color(0.05f, 0.05f, 0.05f, 1f);
                    
                    // First, check if we need to fix the Image component settings
                    var rootImg = inst.GetComponent<Image>();
                    if (rootImg != null) 
                    {
                        // Keep original sprite but make it almost black
                        rootImg.color = almostBlack;
                        rootImg.preserveAspect = false;
                        
                        // Make sure we're using the right Image type for proper corners
                        if (rootImg.sprite != null)
                        {
                            // Try to match the game's Detective's Notebook settings
                            rootImg.type = Image.Type.Sliced;
                            Plugin.Logger?.LogInfo($"MaskPattern sprite: {rootImg.sprite.name}, set type to Sliced");
                        }
                    }
                    
                    var rootRaw = inst.GetComponent<RawImage>();
                    if (rootRaw != null)
                    {
                        rootRaw.color = almostBlack;
                        Plugin.Logger?.LogInfo($"MaskPattern RawImage tinted almost black");
                    }
                    
                        // Find the Pattern child if it exists
                    Transform patternChild = inst.transform.Find("Pattern");
                    
                    // Create a custom rounded border frame with paper-like appearance
                    GameObject borderFrame = new GameObject("RoundedBorder");
                    borderFrame.transform.SetParent(inst.transform, false);
                    
                    var borderRt = borderFrame.AddComponent<RectTransform>();
                    borderRt.anchorMin = Vector2.zero;
                    borderRt.anchorMax = Vector2.one;
                    // Make the border larger than the content to create a visible frame
                    // Extend further at the top and bottom to ensure proper spacing
                    borderRt.offsetMin = new Vector2(-8f, -8f); // Increased bottom and side margins
                    borderRt.offsetMax = new Vector2(8f, 16f); // Increased top and side margins
                    
                    // Add a custom border image with rounded corners
                    var borderImg = borderFrame.AddComponent<Image>();
                    
                    // Create a custom rounded rectangle sprite with larger radius
                    Texture2D roundedTexture = CreateRoundedRectTexture(128, 128, 30);
                    borderImg.sprite = Sprite.Create(roundedTexture, new Rect(0, 0, roundedTexture.width, roundedTexture.height), 
                                                   new Vector2(0.5f, 0.5f), 100f, 1, SpriteMeshType.FullRect, 
                                                   new Vector4(30, 30, 30, 30)); // Border sizes for 9-slice
                    borderImg.type = Image.Type.Sliced;
                    // Use a color similar to the Detective's Notebook paper edge
                    borderImg.color = new Color(0.45f, 0.4f, 0.35f, 1f); // Darker paper color
                    Plugin.Logger?.LogInfo("NotebookThemeHelper: Created custom rounded corner border");
                    
                    // Create a dark background inside the border
                    GameObject darkBg = new GameObject("DarkBackground");
                    darkBg.transform.SetParent(inst.transform, false);
                    // Set dark background to be at the back (lowest sibling index)
                    darkBg.transform.SetAsFirstSibling();
                    
                    var darkBgRt = darkBg.AddComponent<RectTransform>();
                    darkBgRt.anchorMin = Vector2.zero;
                    darkBgRt.anchorMax = Vector2.one;
                    // Extend the dark background to match the border but with a small margin
                    darkBgRt.offsetMin = new Vector2(-4f, -4f);
                    darkBgRt.offsetMax = new Vector2(4f, 12f); // Extra height at top with small margin
                    
                    var darkBgImg = darkBg.AddComponent<Image>();
                    
                    // Create a custom rounded rectangle for the dark background too
                    // Use slightly smaller radius for inner background
                    Texture2D innerRoundedTexture = CreateRoundedRectTexture(128, 128, 28);
                    darkBgImg.sprite = Sprite.Create(innerRoundedTexture, 
                                                   new Rect(0, 0, innerRoundedTexture.width, innerRoundedTexture.height), 
                                                   new Vector2(0.5f, 0.5f), 100f, 1, SpriteMeshType.FullRect, 
                                                   new Vector4(28, 28, 28, 28)); // Border sizes for 9-slice
                    darkBgImg.type = Image.Type.Sliced;
                    darkBgImg.color = new Color(0.05f, 0.05f, 0.05f, 1f); // Almost black
                    Plugin.Logger?.LogInfo("NotebookThemeHelper: Created dark background with rounded corners");
                    
                    darkBgImg.raycastTarget = false;
                    
                    // If we have a pattern child, make it black and ensure it doesn't extend beyond the border
                    if (patternChild != null)
                    {
                        // Adjust the pattern's RectTransform to stay within the border
                        var patternRt = patternChild.GetComponent<RectTransform>();
                        if (patternRt != null)
                        {
                            // Make the pattern significantly smaller than the dark background to avoid drawing into the border
                            patternRt.anchorMin = Vector2.zero;
                            patternRt.anchorMax = Vector2.one;
                            patternRt.offsetMin = new Vector2(0f, 0f);
                            patternRt.offsetMax = new Vector2(0f, 0f); // Keep pattern within the original bounds
                            Plugin.Logger?.LogInfo("NotebookThemeHelper: Adjusted pattern size to stay within border");
                        }
                        
                        var patternImg = patternChild.GetComponent<Image>();
                        if (patternImg != null)
                        {
                            // Try to preserve the texture but make it black
                            patternImg.color = new Color(0.05f, 0.05f, 0.05f, 1f);
                            Plugin.Logger?.LogInfo("NotebookThemeHelper: Set Pattern image to black");
                            
                            // Log more details about the pattern
                            if (patternImg.sprite != null)
                            {
                                Plugin.Logger?.LogInfo($"Pattern child sprite: {patternImg.sprite.name}, type: {patternImg.type}");
                            }
                        }
                        
                        var patternRaw = patternChild.GetComponent<RawImage>();
                        if (patternRaw != null)
                        {
                            patternRaw.color = new Color(0.05f, 0.05f, 0.05f, 1f);
                            Plugin.Logger?.LogInfo("NotebookThemeHelper: Set Pattern raw image to black");
                            
                            // Log more details about the pattern
                            if (patternRaw.texture != null)
                            {
                                Plugin.Logger?.LogInfo($"Pattern child texture: {patternRaw.texture.name}");
                            }
                        }
                    }
                    
                    Plugin.Logger?.LogInfo("NotebookThemeHelper: Applied dark styling to pattern");
                    
                    // Move the border back to the first position (behind everything)
                    borderFrame.transform.SetAsFirstSibling();
                    // Move the dark background to be just above the border
                    darkBg.transform.SetSiblingIndex(1);
                    Plugin.Logger?.LogInfo("NotebookThemeHelper: Set border at the back with dark background above it");
                } catch (Exception ex) { Plugin.Logger?.LogError($"Error replacing pattern: {ex.Message}"); }
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
        
        /// <summary>
        /// Creates a texture with rounded corners for UI elements
        /// </summary>
        private static Texture2D CreateRoundedRectTexture(int width, int height, int radius)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            
            // Fill with transparent pixels initially
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.clear;
            
            // Fill the center with white (will be tinted by Image.color)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Check if pixel is inside rounded rectangle
                    if (IsInsideRoundedRect(x, y, width, height, radius))
                    {
                        colors[y * width + x] = Color.white;
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// Determines if a point is inside a rounded rectangle
        /// </summary>
        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            // Points inside the main rectangle (not in corner regions)
            if (x >= radius && x <= width - radius && y >= 0 && y <= height)
                return true;
            if (y >= radius && y <= height - radius && x >= 0 && x <= width)
                return true;
                
            // Check corners - we need to check if the point is OUTSIDE the corner circles
            // For each corner, we check if the distance from the point to the corner center is greater than radius
            
            // Top-left corner
            if (x < radius && y < radius)
            {
                float distanceFromCorner = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius));
                return distanceFromCorner <= radius;
            }
            // Top-right corner
            else if (x > width - radius && y < radius)
            {
                float distanceFromCorner = Mathf.Sqrt((x - (width - radius)) * (x - (width - radius)) + (y - radius) * (y - radius));
                return distanceFromCorner <= radius;
            }
            // Bottom-left corner
            else if (x < radius && y > height - radius)
            {
                float distanceFromCorner = Mathf.Sqrt((x - radius) * (x - radius) + (y - (height - radius)) * (y - (height - radius)));
                return distanceFromCorner <= radius;
            }
            // Bottom-right corner
            else if (x > width - radius && y > height - radius)
            {
                float distanceFromCorner = Mathf.Sqrt((x - (width - radius)) * (x - (width - radius)) + (y - (height - radius)) * (y - (height - radius)));
                return distanceFromCorner <= radius;
            }
            
            return false;
        }
        
        // Helper method removed as calculations are now done directly in IsInsideRoundedRect
        
        // ContentsPage template is now declared at the top of the class with other UI templates

        /// <summary>
        /// Find the ContentsPage in the Detective's Notebook if not already cached
        /// </summary>
        private static void FindContentsPage()
        {
            if (ContentsPageTemplate != null && ContentsPageSprite != null) return;
            
            try
            {
                // Try multiple known paths where ContentsPage might be found
                var paths = new string[] {
                    "GameCanvas/WindowCanvas/Detective's Notebook/Page/Scroll View/Viewport/History/ContentsPage",
                    "GameCanvas/WindowCanvas/Detective's Notebook/Page/Scroll View/Viewport/Content/ContentsPage",
                    "GameCanvas/WindowCanvas/Detective's Notebook/ContentsPage"
                };
                
                foreach (var path in paths)
                {
                    var obj = GameObject.Find(path);
                    if (obj != null)
                    {
                        ContentsPageTemplate = obj.transform;
                        var img = obj.GetComponent<Image>();
                        if (img != null && img.sprite != null)
                        {
                            ContentsPageSprite = img.sprite;
                            Plugin.Logger?.LogInfo($"NotebookThemeHelper: Found ContentsPage at {path} with sprite {img.sprite.name}");
                            return;
                        }
                    }
                }
                
                // If direct paths failed, search all objects with name "ContentsPage"
                var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t == null || string.IsNullOrEmpty(t.name)) continue;
                    if (t.name == "ContentsPage")
                    {
                        // Check if this is under the Detective's Notebook
                        bool isUnderNotebook = false;
                        Transform parent = t.parent;
                        int depth = 0;
                        while (parent != null && depth++ < 10)
                        {
                            if (parent.name == "Detective's Notebook")
                            {
                                isUnderNotebook = true;
                                break;
                            }
                            parent = parent.parent;
                        }
                        
                        if (isUnderNotebook)
                        {
                            ContentsPageTemplate = t;
                            var img = t.GetComponent<Image>();
                            if (img != null && img.sprite != null)
                            {
                                ContentsPageSprite = img.sprite;
                                Plugin.Logger?.LogInfo($"NotebookThemeHelper: Found ContentsPage via search at {GetPath(t)} with sprite {img.sprite.name}");
                                return;
                            }
                        }
                    }
                }
                
                Plugin.Logger?.LogWarning("NotebookThemeHelper: Could not find ContentsPage in Detective's Notebook");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error finding ContentsPage: {ex.Message}");
            }
        }

        /// <summary>
        /// Clone the Detective's Notebook ContentsPage and add it to the parent transform
        /// Only copies the Image component without any children
        /// </summary>
        public static GameObject InstantiateContentsPage(Transform parent)
        {
            try
            {
                // Make sure we've tried to find the ContentsPage
                FindContentsPage();
                
                // Create a new GameObject for our ContentsPage
                GameObject inst = new GameObject("BIB_ContentsPage");
                
                // Add an Image component
                var contentsImage = inst.AddComponent<Image>();
                
                // Use the sprite from the template if available
                if (IsAlive(ContentsPageTemplate))
                {
                    var templateImg = ContentsPageTemplate.GetComponent<Image>();
                    if (templateImg != null && templateImg.sprite != null)
                    {
                        // Copy all properties exactly from the original
                        contentsImage.sprite = templateImg.sprite;
                        contentsImage.type = templateImg.type;
                        contentsImage.color = templateImg.color;
                        contentsImage.material = templateImg.material;
                        contentsImage.preserveAspect = templateImg.preserveAspect;
                        contentsImage.fillCenter = templateImg.fillCenter;
                        contentsImage.fillMethod = templateImg.fillMethod;
                        contentsImage.fillAmount = templateImg.fillAmount;
                        contentsImage.fillClockwise = templateImg.fillClockwise;
                        contentsImage.fillOrigin = templateImg.fillOrigin;
                        contentsImage.alphaHitTestMinimumThreshold = templateImg.alphaHitTestMinimumThreshold;
                        
                        // Log the sprite name and properties for debugging
                        Plugin.Logger?.LogInfo($"NotebookThemeHelper: Copied ContentsPage image properties from template. Sprite name: {templateImg.sprite.name}, Type: {templateImg.type}, Color: {templateImg.color}");
                    }
                }
                else if (ContentsPageSprite != null)
                {
                    contentsImage.sprite = ContentsPageSprite;
                    Plugin.Logger?.LogInfo($"NotebookThemeHelper: Using cached ContentsPage sprite: {ContentsPageSprite.name}");
                }
                else
                {
                    Plugin.Logger?.LogWarning("NotebookThemeHelper: ContentsPage template not found, using fallback");
                    // Use a fallback color if we couldn't find the template
                    contentsImage.color = new Color(0.9f, 0.85f, 0.8f, 1f); // Light paper color
                }
                
                // Set up the RectTransform - make it slightly smaller than the container
                var rt = inst.GetComponent<RectTransform>();
                if (rt == null) rt = inst.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                // Add small margins to make it slightly smaller than the container
                rt.offsetMin = new Vector2(5f, 5f);
                rt.offsetMax = new Vector2(-5f, -5f);
                
                // Disable raycast target to prevent blocking interactions
                contentsImage.raycastTarget = false;
                
                // Set the parent
                if (parent != null)
                {
                    inst.transform.SetParent(parent, false);
                    // Make sure it's at the back
                    inst.transform.SetAsFirstSibling();
                }
                
                return inst;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error creating ContentsPage: {ex.Message}");
                return null;
            }
        }
    }
}
