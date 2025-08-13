using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using SOD.Common;
using Il2CppInterop.Runtime;
using TMPro; // Added for TextMeshPro support

namespace BackInBusiness
{
    // Lightweight theme helper: find the game's Container_Card and its sprites (no bundles),
    // and optionally instantiate a clone under our UI.
    internal static class UIThemeCache
    {
        private static bool _attempted;
        public static Transform CardTemplate; // The source Container_Card to clone
        public static Transform CloseTemplate; // The preferred Close button template near the card
        public static Sprite CardBG;
        public static Sprite CardNoise;
        public static Sprite CardPaper;

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

        private static void RescanCardTemplate()
        {
            try
            {
                try { Plugin.Logger.LogInfo("UIThemeCache: rescan for Container_Card due to missing/invalid template..."); } catch { }
                CardTemplate = null; CloseTemplate = null; // clear cached pointers
                _attempted = false; // allow EnsureLoaded to run scan body
                EnsureLoaded();
            }
            catch { }
        }

        public static bool EnsureLoaded()
        {
            if (_attempted)
            {
                // If previously attempted, but the scene object got destroyed, rescan
                if (!IsAlive(CardTemplate))
                {
                    RescanCardTemplate();
                }
                return (CardTemplate != null) || (CardBG != null || CardNoise != null || CardPaper != null);
            }
            _attempted = true;
            try
            {
                try { Plugin.Logger.LogInfo("UIThemeCache: scanning scene for Container_Card..."); } catch { }
                var all = Resources.FindObjectsOfTypeAll<Transform>();
                Transform best = null;
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null) continue;
                    var tn = t.name ?? string.Empty;
                    if (!string.Equals(tn, "Container_Card", StringComparison.Ordinal) &&
                        !string.Equals(tn, "Container Card", StringComparison.Ordinal) &&
                        tn.IndexOf("Container_Card", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // Prefer one under Detective's Notebook hierarchy, but accept any if needed
                    bool prefer = false;
                    try
                    {
                        int depth = 0; var p = t.parent;
                        while (p != null && depth++ < 20)
                        {
                            if (!string.IsNullOrEmpty(p.name) && p.name.IndexOf("Detective", StringComparison.OrdinalIgnoreCase) >= 0)
                            { prefer = true; break; }
                            p = p.parent;
                        }
                    }
                    catch { }

                    if (best == null || prefer)
                        best = t;
                    if (prefer) break;
                }

                if (best != null)
                {
                    CardTemplate = best;
                    try { Plugin.Logger.LogInfo($"UIThemeCache: Found card template at {GetPath(best)}"); } catch { }

                    // Extract sprites for manual fallback usage
                    try
                    {
                        Transform bgT = best.Find("BG");
                        Transform noiseT = best.Find("Mask_CardNoise");
                        Transform paperT = best.Find("Container_Paper");
                        if (bgT != null)
                        {
                            var img = bgT.GetComponent<Image>();
                            if (img != null) CardBG = img.sprite;
                        }
                        if (noiseT != null)
                        {
                            var img = noiseT.GetComponent<Image>();
                            if (img != null) CardNoise = img.sprite;
                        }
                        if (paperT != null)
                        {
                            var img = paperT.GetComponent<Image>();
                            if (img != null) CardPaper = img.sprite;
                        }
                    }
                    catch { }

                    // Also try to discover a nearby Close button once and cache it
                    try
                    {
                        CloseTemplate = FindCloseButtonNearCardTemplate();
                        if (CloseTemplate != null)
                            Plugin.Logger.LogInfo($"UIThemeCache: Cached CloseTemplate at {GetPath(CloseTemplate)}");
                    }
                    catch { }
                }
                else
                {
                    try { Plugin.Logger.LogWarning("UIThemeCache: Could not locate Container_Card in scene."); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.EnsureLoaded error: {ex.Message}"); } catch { }
            }
            return (CardTemplate != null) || (CardBG != null || CardNoise != null || CardPaper != null);
        }

        public static GameObject InstantiateCard(Transform parent)
        {
            try
            {
                if (!EnsureLoaded() || !IsAlive(CardTemplate))
                {
                    // Try once more to recover the template
                    RescanCardTemplate();
                    if (!IsAlive(CardTemplate)) return null;
                }
                var inst = UnityEngine.Object.Instantiate(CardTemplate.gameObject);
                inst.name = "Emp_Container_Card";
                var rt = inst.GetComponent<RectTransform>();
                if (rt == null) rt = inst.AddComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                // Ensure visuals don't block clicks
                try
                {
                    var imgs = inst.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgs.Length; i++) imgs[i].raycastTarget = false;
                }
                catch { }
                // Avoid nested canvases or raycasters from the source hierarchy
                try
                {
                    var canvases = inst.GetComponentsInChildren<Canvas>(true);
                    for (int i = 0; i < canvases.Length; i++) UnityEngine.Object.Destroy(canvases[i]);
                    var raycasters = inst.GetComponentsInChildren<GraphicRaycaster>(true);
                    for (int i = 0; i < raycasters.Length; i++) UnityEngine.Object.Destroy(raycasters[i]);
                }
                catch { }
                try { Plugin.Logger.LogInfo("UIThemeCache: Instantiated Container_Card under EmployeeDetailsView."); } catch { }
                return inst;
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.InstantiateCard error: {ex.Message}"); } catch { }
                return null;
            }
        }

        // Locate the Detective's Notebook CloseButton in the active resources
        public static Transform FindNotebookCloseButton()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Transform>();
                Transform best = null;
                int bestScore = -1;
                try { Plugin.Logger.LogInfo($"UIThemeCache: scanning for Detective Notebook CloseButton among {all.Length} transforms..."); } catch { }
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null) continue;
                    var tn = t.name ?? string.Empty;
                    if (!NameLooksLikeClose(tn)) continue;

                    // Check ancestry to match the provided path segments
                    bool ok = false;
                    int score = 0;
                    if (string.Equals(tn, "CloseButton", StringComparison.Ordinal)) score += 2; else score += 1; // prefer exact name
                    try
                    {
                        bool hasDetectiveNotebook = false, hasWindowCanvas = false, hasGameCanvas = false;
                        int depth = 0; var p = t.parent;
                        while (p != null && depth++ < 32)
                        {
                            string pn = p.name ?? string.Empty;
                            string norm = pn.Replace("'", string.Empty).ToLowerInvariant();
                            if (!hasDetectiveNotebook && (norm.IndexOf("detective", StringComparison.Ordinal) >= 0 && norm.IndexOf("notebook", StringComparison.Ordinal) >= 0)) hasDetectiveNotebook = true;
                            if (!hasWindowCanvas && pn.IndexOf("WindowCanvas", StringComparison.OrdinalIgnoreCase) >= 0) hasWindowCanvas = true;
                            if (!hasGameCanvas && pn.IndexOf("GameCanvas", StringComparison.OrdinalIgnoreCase) >= 0) hasGameCanvas = true;
                            p = p.parent;
                        }
                        // Prefer exact hierarchy but don't require all; compute a score
                        if (hasDetectiveNotebook) score += 3;
                        if (hasWindowCanvas) score += 1;
                        if (hasGameCanvas) score += 1;
                        ok = hasDetectiveNotebook || (hasWindowCanvas && hasGameCanvas);
                    }
                    catch { }

                    if (ok)
                    {
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = t;
                        }
                    }
                }
                if (best != null)
                {
                    try { Plugin.Logger.LogInfo($"UIThemeCache: Found CloseButton at {GetPath(best)} (score={bestScore})"); } catch { }
                    return best;
                }
                else
                {
                    try { Plugin.Logger.LogWarning("UIThemeCache: Detective Notebook CloseButton not found; will use fallback."); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.FindNotebookCloseButton error: {ex.Message}"); } catch { }
            }
            return null;
        }

        // Instantiate the Detective's Notebook CloseButton and sanitize it for our UI (strip nested canvases/raycasters)
        public static GameObject InstantiateCloseButton(Transform parent)
        {
            try
            {
                // Prefer cached CloseTemplate if available and alive
                Transform tmpl = null;
                if (CloseTemplate != null)
                {
                    try
                    {
                        // Accessing name will throw if destroyed
                        var _ = CloseTemplate.name;
                        tmpl = CloseTemplate;
                        Plugin.Logger.LogInfo($"UIThemeCache: Using cached CloseTemplate at {GetPath(CloseTemplate)}");
                    }
                    catch { tmpl = null; }
                }
                if (tmpl == null)
                {
                    // Re-discover near the card template
                    tmpl = FindCloseButtonNearCardTemplate();
                    if (tmpl != null) CloseTemplate = tmpl;
                }
                if (tmpl == null)
                {
                    // As a last resort, try notebook path. If still not found, we return null so caller uses simple 'X'
                    tmpl = FindNotebookCloseButton();
                }
                if (tmpl == null) return null;
                // Build a fresh, clean button and copy only sprite visuals from the template to avoid any game logic side-effects
                var go = UniverseLib.UI.UIFactory.CreateUIObject("Emp_CloseButton", parent.gameObject);
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.SetParent(parent, false);
                // Add Image and Button
                var img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();
                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                // Disable default transition, our adapter drives visuals to mirror the game's behavior
                try { btn.transition = Selectable.Transition.None; } catch { }
                // Copy sprite from template's first Image we can find
                try
                {
                    Image src = tmpl.GetComponent<Image>();
                    if (src == null) src = tmpl.GetComponentInChildren<Image>(true);
                    if (src != null)
                    {
                        img.sprite = src.sprite;
                        img.type = src.type;
                        img.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
                        img.material = src.material;
                        img.color = new Color(src.color.r, src.color.g, src.color.b, 1f);
                        img.raycastTarget = true;
                    }
                }
                catch { }
                // Ensure active
                try { go.SetActive(true); } catch { }
                try { Plugin.Logger.LogInfo("UIThemeCache: Instantiated clean CloseButton (copied sprite only)."); } catch { }
                return go;
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.InstantiateCloseButton error: {ex.Message}"); } catch { }
                return null;
            }
        }

        // Heuristic to decide if a name looks like a close/exit button
        private static bool NameLooksLikeClose(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string s = name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            if (s.Contains("close")) return true;
            if (s.Contains("exit")) return true;
            if (s == "x" || s == "btnx" || s.Contains("iconx")) return true;
            if (s.Contains("btnclose") || s.Contains("closebtn")) return true;
            return false;
        }

        // Search around the discovered CardTemplate (e.g., under InfoWindow) for a close-like button
        public static Transform FindCloseButtonNearCardTemplate()
        {
            try
            {
                if (!EnsureLoaded() || CardTemplate == null) return null;
                // Search siblings and ancestors up to a limited depth
                Transform scope = CardTemplate.parent != null ? CardTemplate.parent : CardTemplate;
                Transform best = null; int bestScore = -1;
                // Breadth-first over descendants of the scope
                var queue = new System.Collections.Generic.Queue<Transform>();
                queue.Enqueue(scope);
                int visited = 0;
                while (queue.Count > 0 && visited < 5000)
                {
                    var t = queue.Dequeue(); visited++;
                    if (t == null) continue;
                    var tn = t.name ?? string.Empty;
                    if (NameLooksLikeClose(tn))
                    {
                        int score = 0;
                        if (t.GetComponent<Button>() != null) score += 3;
                        if (t.GetComponent<Image>() != null) score += 1;
                        // Prefer immediate siblings of CardTemplate
                        if (t.parent == scope) score += 2;
                        if (score > bestScore) { bestScore = score; best = t; }
                    }
                    // enqueue children
                    for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
                }
                if (best != null)
                {
                    try { Plugin.Logger.LogInfo($"UIThemeCache: Found CloseButton near CardTemplate at {GetPath(best)} (score={bestScore})"); } catch { }
                    return best;
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.FindCloseButtonNearCardTemplate error: {ex.Message}"); } catch { }
            }
            return null;
        }

        // Broader fallback: find any reasonable Close button in the scene/resources
        public static Transform FindAnyCloseButtonCandidate()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Transform>();
                Transform best = null; int bestScore = -1; int considered = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i]; if (t == null) continue;
                    var tn = t.name ?? string.Empty;
                    if (!NameLooksLikeClose(tn)) continue;
                    // Skip our own instances
                    var tnl = tn.ToLowerInvariant();
                    if (tn == "Emp_CloseButton" || tnl.Contains("emp_")) continue;

                    // Score by ancestry and component presence
                    int score = 0; considered++;
                    try
                    {
                        if (t.GetComponent<Button>() != null) score += 3; // prefer real buttons
                        if (t.GetComponent<Image>() != null) score += 1;
                        int depth = 0; var p = t.parent;
                        while (p != null && depth++ < 32)
                        {
                            var pn = p.name ?? string.Empty; var pnl = pn.ToLowerInvariant();
                            if (pnl.Contains("infowindow")) score += 3; // we know card came from here
                            if (pnl.Contains("detective") && pnl.Contains("notebook")) score += 2;
                            if (pnl.Contains("windowcanvas")) score += 1;
                            if (pnl.Contains("gamecanvas")) score += 1;
                            p = p.parent;
                        }
                    }
                    catch { }

                    if (score > bestScore)
                    {
                        bestScore = score; best = t;
                    }
                }
                if (best != null)
                {
                    try { Plugin.Logger.LogInfo($"UIThemeCache: Fallback found a close-like candidate at {GetPath(best)} (score={bestScore}, considered={considered})"); } catch { }
                    return best;
                }
                else
                {
                    try { Plugin.Logger.LogWarning("UIThemeCache: No close-like candidate found in fallback search."); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"UIThemeCache.FindAnyCloseButtonCandidate error: {ex.Message}"); } catch { }
            }
            return null;
        }
    }

    internal class EmployeeDetailsView
    {
        private GameObject root;
        private RawImage portrait;
        private Text nameText;
        private TextMeshProUGUI jobText; // Changed to TextMeshProUGUI
        private TextMeshProUGUI salaryText; // Changed to TextMeshProUGUI
        private ButtonRef fireButton;
        private ButtonRef changeRoleButton;
        public event Action CloseRequested;

        // Helper method to create TextMeshProUGUI components with the game's font
        private TextMeshProUGUI CreateTMP(GameObject parent, string name, string text, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            
            // Find and apply the TruetypewriterPolyglott SDF font
            try
            {
                // Search for the font asset by name
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                TMP_FontAsset gameFont = null;
                
                // IL2CPP compatible loop (no LINQ)
                for (int i = 0; i < fonts.Length; i++)
                {
                    var font = fonts[i];
                    if (font != null && font.name != null && font.name.Contains("TruetypewriterPolyglott SDF"))
                    {
                        gameFont = font;
                        break;
                    }
                }
                
                if (gameFont != null)
                {
                    tmp.font = gameFont;
                    Plugin.Logger.LogInfo($"Applied TruetypewriterPolyglott SDF font to {name}");
                }
                else
                {
                    Plugin.Logger.LogWarning($"Could not find TruetypewriterPolyglott SDF font for {name}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error applying font to {name}: {ex.Message}");
            }
            
            return tmp;
        }

        public void Build(GameObject parent)
        {
            Plugin.Logger.LogInfo("EmployeeDetailsView.Build start");
            // Root container
            root = UIFactory.CreateUIObject("EmployeeDetails", parent);
            // RectTransform is already added by UIFactory; just fetch it
            var rt = root.GetComponent<RectTransform>();
            // Default to fill parent; specific layouts are set in ConfigureAsPanelLayout()
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Use parent panel's Canvas; do not add our own to avoid cross-canvas sorting issues.
            // Ensure we're the last sibling so our children draw above the panel content background.
            root.transform.SetAsLastSibling();

            // Card root container under the panel root
            GameObject cardRoot = UIFactory.CreateUIObject("Emp_CardRoot", root);
            var cardRt = cardRoot.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 0f);
            cardRt.anchorMax = new Vector2(1f, 1f);
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;
            try { cardRoot.transform.SetAsLastSibling(); } catch { }
            
            // Prime NotebookThemeHelper cache once, similar to UIThemeCache usage
            try { if (!NotebookThemeHelper.EnsureLoaded()) NotebookThemeHelper.ForceRescan(); } catch { }

            // Add Detective's Notebook background under cardRoot and make it the parent for the card
            GameObject background = NotebookThemeHelper.InstantiateBackground(cardRoot.transform);
            if (background != null) {
                background.transform.SetAsFirstSibling(); // very back in cardRoot
                try { Plugin.Logger.LogInfo("EmployeeDetailsView: Added Detective's Notebook Background to cardRoot"); } catch { }
            }

            // Mask pattern as child of background (above background image)
            GameObject maskPattern = NotebookThemeHelper.InstantiateMaskPattern(background != null ? background.transform : cardRoot.transform);
            if (maskPattern != null) {
                try { maskPattern.transform.SetAsFirstSibling(); } catch { }
                try { Plugin.Logger.LogInfo("EmployeeDetailsView: Added Detective's Notebook MaskPattern to background"); } catch { }
            }

            // Clone the actual game's Container_Card under the background; background acts as parent container
            GameObject clonedCard = UIThemeCache.InstantiateCard(background != null ? background.transform : cardRoot.transform);
            if (clonedCard != null)
            {
                // ensure it sits above mask but below other content we add later
                try { clonedCard.transform.SetSiblingIndex(1); } catch { }
                
                // Find Container_Paper within the cloned card to add the ContentsPage
                try
                {
                    Transform containerPaper = clonedCard.transform.Find("Container_Paper");
                    if (containerPaper != null)
                    {
                        // Add ContentsPage as a sibling of Container_Paper, not a child
                        // This ensures it draws over the Container_Paper but under other content
                        GameObject contentsPage = NotebookThemeHelper.InstantiateContentsPage(clonedCard.transform);
                        if (contentsPage != null)
                        {
                            // Position it just above Container_Paper in the hierarchy
                            // but below other content elements
                            int paperIndex = containerPaper.GetSiblingIndex();
                            contentsPage.transform.SetSiblingIndex(paperIndex + 1);
                            
                            // Match the ContentsPage size to Container_Paper
                            RectTransform paperRect = containerPaper.GetComponent<RectTransform>();
                            RectTransform contentsRect = contentsPage.GetComponent<RectTransform>();
                            if (paperRect != null && contentsRect != null)
                            {
                                contentsRect.anchorMin = paperRect.anchorMin;
                                contentsRect.anchorMax = paperRect.anchorMax;
                                // Make it slightly smaller than Container_Paper
                                contentsRect.offsetMin = new Vector2(paperRect.offsetMin.x + 5f, paperRect.offsetMin.y + 5f);
                                contentsRect.offsetMax = new Vector2(paperRect.offsetMax.x - 5f, paperRect.offsetMax.y - 5f);
                            }
                            
                            Plugin.Logger?.LogInfo("EmployeeDetailsView: Added Detective's Notebook ContentsPage over Container_Paper");
                        }
                        else
                        {
                            Plugin.Logger?.LogWarning("EmployeeDetailsView: Failed to instantiate ContentsPage");
                        }
                    }
                    else
                    {
                        Plugin.Logger?.LogWarning("EmployeeDetailsView: Container_Paper not found in cloned card");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"Error adding ContentsPage: {ex.Message}");
                }
            }

            // Remove RectMask2D from cardRoot to avoid clipping backgrounds
            try { var rm = cardRoot.GetComponent<RectMask2D>(); if (rm != null) UnityEngine.Object.Destroy(rm); } catch { }

            // White border box for portrait
            var portraitBorderGO = UIFactory.CreateUIObject("Emp_PortraitBorder", root);
            var borderImage = portraitBorderGO.AddComponent<Image>();
            borderImage.color = Color.gray;
            var borderRt = portraitBorderGO.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0f, 1f);
            borderRt.anchorMax = new Vector2(0f, 1f);
            borderRt.pivot = new Vector2(0f, 1f);
            borderRt.anchoredPosition = new Vector2(30f, -70f);
            // Make the border slightly larger than the portrait (5px on each side)
            borderRt.sizeDelta = new Vector2(138f, 138f);
            try { portraitBorderGO.transform.SetParent(cardRoot.transform, false); portraitBorderGO.transform.SetSiblingIndex(1); } catch { }
            
            // Portrait (bigger) - fixed to top-left
            var portraitGO = UIFactory.CreateUIObject("Emp_Portrait", root);
            portrait = portraitGO.AddComponent<RawImage>();
            var pRt = portraitGO.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 1f);
            pRt.anchorMax = new Vector2(0f, 1f);
            pRt.pivot = new Vector2(0f, 1f);
            // Center the portrait on the border
            pRt.anchoredPosition = new Vector2(30f + 5f, -70f - 5f);
            pRt.sizeDelta = new Vector2(128f, 128f);
            // Parent inside the card and set to be above the border but below text
            try { portraitGO.transform.SetParent(cardRoot.transform, false); portraitGO.transform.SetSiblingIndex(2); } catch { }

            // Name - fixed offsets from top-left
            nameText = UIFactory.CreateLabel(root, "Emp_Name", "Name", TextAnchor.UpperLeft);
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.anchoredPosition = new Vector2(20f, -12f);
            nameRt.sizeDelta = new Vector2(460f, 40f);
            nameText.fontSize = 32;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.black;
            // Ensure text is not clipped and sits above background layers
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            // Parent under the card so it's clipped correctly and ordered above the paper
            try { nameText.transform.SetParent(cardRoot.transform, false); nameText.transform.SetAsLastSibling(); } catch { }

            // Job title - fixed below name
            jobText = CreateTMP(root, "Emp_Job", "Job", TextAlignmentOptions.TopLeft);
            var jobRt = jobText.GetComponent<RectTransform>();
            jobRt.anchorMin = new Vector2(0f, 1f);
            jobRt.anchorMax = new Vector2(0f, 1f);
            jobRt.pivot = new Vector2(0f, 1f);
            jobRt.anchoredPosition = new Vector2(40f + 128f + 12f, -30f - 40f);
            jobRt.sizeDelta = new Vector2(380f, 22f);
            // Font size and color
            jobText.fontSize = 14;
            jobText.color = Color.black;
            try { jobText.transform.SetParent(cardRoot.transform, false); jobText.transform.SetAsLastSibling(); } catch { }

            // Salary - fixed below job
            salaryText = CreateTMP(root, "Emp_Salary", "Salary", TextAlignmentOptions.TopLeft);
            var salRt = salaryText.GetComponent<RectTransform>();
            salRt.anchorMin = new Vector2(0f, 1f);
            salRt.anchorMax = new Vector2(0f, 1f);
            salRt.pivot = new Vector2(0f, 1f);
            salRt.anchoredPosition = new Vector2(40f + 128f + 12f, -30f - 40f - 24f);
            salRt.sizeDelta = new Vector2(380f, 22f);
            // Font size and color
            salaryText.fontSize = 14;
            salaryText.color = Color.black;
            try { salaryText.transform.SetParent(cardRoot.transform, false); salaryText.transform.SetAsLastSibling(); } catch { }

            // Close button: clone the game's Detective's Notebook CloseButton and wire it
            RectTransform closeRt = null;
            try
            {
                Plugin.Logger.LogInfo("EmployeeDetailsView: Attempting to instantiate Detective Notebook CloseButton...");
                var closeGo = UIThemeCache.InstantiateCloseButton(root.transform);
                if (closeGo != null)
                {
                    closeRt = closeGo.GetComponent<RectTransform>();
                    closeRt.anchorMin = new Vector2(1f, 1f);
                    closeRt.anchorMax = new Vector2(1f, 1f);
                    closeRt.pivot = new Vector2(1f, 1f);
                    closeRt.anchoredPosition = new Vector2(-10f, -10f);
                    closeRt.sizeDelta = new Vector2(35f, 35f);
                    try { closeRt.transform.SetAsLastSibling(); } catch { }

                    var btn = closeGo.GetComponent<Button>();
                    if (btn == null) btn = closeGo.AddComponent<Button>();
                    // Remove any existing listeners from the source prefab to avoid invoking unrelated game logic
                    try { btn.onClick.RemoveAllListeners(); } catch { }
                    // Wire our close action
                    try { btn.onClick.AddListener(() => { try { CloseRequested?.Invoke(); } catch { } }); } catch { }
                    // Disable navigation to prevent focus side-effects interfering with pointer events
                    try { var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav; } catch { }
                    // Use BIBButtonController (mirror of game's ButtonController visuals) and pointer events
                    try
                    {
                        var controller = closeGo.GetComponent<BIBButtonController>();
                        if (controller == null) controller = closeGo.AddComponent<BIBButtonController>();
                        // Configure: clone game's AdditionalHighlight as a child now (disabled), show only on hover
                        try
                        {
                            controller.UseCloneHighlight = true;
                            controller.UseAdditionalHighlight = true;
                            controller.AdditionalHighlightAtFront = true;
                            controller.AdditionalHighlightColour = new Color(1f, 1f, 1f, 0.35f);
                            
                            // Configure button sound settings for close button
                            controller.useGenericAudioSounds = false; // Use custom audio events instead of generic ones
                            controller.isCloseButton = true; // Special flag to play both close and page sounds
                            controller.buttonDown = AudioControls.Instance.panelIconButton; // Button down sound
                            controller.clickPrimary = AudioControls.Instance.closeButton; // Close button sound
                            controller.clickSecondary = AudioControls.Instance.tab; // Page/back sound
                        }
                        catch { }
                        if (controller.TargetImage == null)
                        {
                            var imgSelf = closeGo.GetComponent<Image>();
                            if (imgSelf != null) controller.TargetImage = imgSelf;
                            else { var imgChild = closeGo.GetComponentInChildren<Image>(true); if (imgChild != null) controller.TargetImage = imgChild; }
                        }
                        if (controller.TargetTransform == null)
                        {
                            controller.TargetTransform = closeGo.GetComponent<RectTransform>();
                        }
                        // Make sure Unity's own transition doesn't interfere
                        try { btn.transition = Selectable.Transition.None; } catch { }
                        var et = closeGo.GetComponent<EventTrigger>();
                        if (et == null) et = closeGo.AddComponent<EventTrigger>();
                        if (et.triggers == null)
                        {
                            et.triggers = new Il2CppSystem.Collections.Generic.List<EventTrigger.Entry>();
                        }
                        else et.triggers.Clear();

                        EventTrigger.Entry mk(EventTriggerType t)
                        {
                            var e = new EventTrigger.Entry { eventID = t };
                            e.callback = new EventTrigger.TriggerEvent();
                            return e;
                        }

                        // PointerEnter
                        try
                        {
                            var e = mk(EventTriggerType.PointerEnter);
                            e.callback.AddListener((ev) => { try { controller.OnPointerEnter(ev as UnityEngine.EventSystems.PointerEventData); } catch { } });
                            et.triggers.Add(e);
                        }
                        catch { }
                        // PointerExit
                        try
                        {
                            var e = mk(EventTriggerType.PointerExit);
                            e.callback.AddListener((ev) => { try { controller.OnPointerExit(ev as UnityEngine.EventSystems.PointerEventData); } catch { } });
                            et.triggers.Add(e);
                        }
                        catch { }
                        // PointerDown
                        try
                        {
                            var e = mk(EventTriggerType.PointerDown);
                            e.callback.AddListener((ev) => { try { controller.OnPointerDown(ev as UnityEngine.EventSystems.PointerEventData); } catch { } });
                            et.triggers.Add(e);
                        }
                        catch { }
                        // PointerUp
                        try
                        {
                            var e = mk(EventTriggerType.PointerUp);
                            e.callback.AddListener((ev) => { try { controller.OnPointerUp(ev as UnityEngine.EventSystems.PointerEventData); } catch { } });
                            et.triggers.Add(e);
                        }
                        catch { }
                        // PointerClick (for optional nudge feedback)
                        try
                        {
                            var e = mk(EventTriggerType.PointerClick);
                            e.callback.AddListener((ev) => { try { controller.OnPointerClick(ev as UnityEngine.EventSystems.PointerEventData); } catch { } });
                            et.triggers.Add(e);
                        }
                        catch { }
                    }
                    catch { }

                    // Ensure visible and clickable
                    try { var img = closeGo.GetComponent<Image>(); if (img != null) { img.color = new Color(img.color.r, img.color.g, img.color.b, 1f); img.raycastTarget = true; } } catch { }
                    try { Plugin.Logger.LogInfo("EmployeeDetailsView: Cloned CloseButton and wired click handler."); } catch { }
                }
                else
                {
                    Plugin.Logger.LogWarning("EmployeeDetailsView: CloseButton template not found, using fallback 'X' button.");
                    var closeRef = UIFactory.CreateButton(root, "Emp_Close", "X");
                    closeRef.ButtonText.fontSize = 22;
                    closeRef.ButtonText.color = Color.black;
                    closeRef.ButtonText.fontStyle = FontStyle.Bold;
                    closeRt = closeRef.Component.GetComponent<RectTransform>();
                    closeRt.anchorMin = new Vector2(1f, 1f);
                    closeRt.anchorMax = new Vector2(1f, 1f);
                    closeRt.pivot = new Vector2(1f, 1f);
                    try { var img = closeRef.Component.GetComponent<Image>(); if (img != null) { var c = img.color; c.a = 0f; img.color = c; } } catch { }
                    closeRt.anchoredPosition = new Vector2(-10f, -10f);
                    closeRt.sizeDelta = new Vector2(35f, 35f);
                    try { closeRef.Component.transform.SetAsLastSibling(); } catch { }
                    closeRef.OnClick += () => { try { CloseRequested?.Invoke(); } catch { } };
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogWarning($"EmployeeDetailsView: error constructing CloseButton: {ex.Message}"); } catch { }
            }

            // Background and themed layers (Card BG / Paper / Noise) only if cloning failed
            if (clonedCard == null)
            {
                bool themed = UIThemeCache.EnsureLoaded();

                // BG
                GameObject bg = UIFactory.CreateUIObject("Emp_BG", cardRoot);
                var bgImg = bg.GetComponent<UnityEngine.UI.Image>();
                if (bgImg == null) bgImg = bg.AddComponent<UnityEngine.UI.Image>();
                if (themed && UIThemeCache.CardBG != null)
                {
                    bgImg.sprite = UIThemeCache.CardBG;
                    bgImg.type = Image.Type.Sliced;
                    bgImg.color = Color.white;
                }
                else
                {
                    bgImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                }
                bgImg.raycastTarget = false;
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = new Vector2(0f, 0f);
                bgRt.anchorMax = new Vector2(1f, 1f);
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                try { bg.transform.SetAsFirstSibling(); } catch { }

                // Paper
                GameObject paper = UIFactory.CreateUIObject("Emp_Paper", cardRoot);
                var paperImg = paper.GetComponent<UnityEngine.UI.Image>();
                if (paperImg == null) paperImg = paper.AddComponent<UnityEngine.UI.Image>();
                paperImg.raycastTarget = false;
                if (themed && UIThemeCache.CardPaper != null)
                {
                    paperImg.sprite = UIThemeCache.CardPaper;
                    paperImg.type = Image.Type.Sliced;
                    paperImg.color = Color.white;
                }
                else
                {
                    paperImg.color = new Color(0.95f, 0.95f, 0.92f, 1f);
                }
                var paperRt = paper.GetComponent<RectTransform>();
                paperRt.anchorMin = new Vector2(0f, 0f);
                paperRt.anchorMax = new Vector2(1f, 1f);
                paperRt.offsetMin = Vector2.zero;
                paperRt.offsetMax = Vector2.zero;
                try { paper.transform.SetSiblingIndex(1); } catch { }

                // Noise
                GameObject noise = UIFactory.CreateUIObject("Emp_Noise", cardRoot);
                var noiseImg = noise.GetComponent<UnityEngine.UI.Image>();
                if (noiseImg == null) noiseImg = noise.AddComponent<UnityEngine.UI.Image>();
                noiseImg.raycastTarget = false;
                if (themed && UIThemeCache.CardNoise != null)
                {
                    noiseImg.sprite = UIThemeCache.CardNoise;
                    noiseImg.type = Image.Type.Simple;
                    var c = Color.white; c.a = 0.22f; noiseImg.color = c;
                }
                else
                {
                    var c = Color.white; c.a = 0f; noiseImg.color = c;
                }
                var noiseRt = noise.GetComponent<RectTransform>();
                noiseRt.anchorMin = new Vector2(0f, 0f);
                noiseRt.anchorMax = new Vector2(1f, 1f);
                noiseRt.offsetMin = Vector2.zero;
                noiseRt.offsetMax = Vector2.zero;
                try { noise.transform.SetSiblingIndex(2); } catch { }
            }

            // Buttons container - bottom bar
            GameObject btnRow = UIFactory.CreateHorizontalGroup(root, "Emp_Buttons", true, false, true, true, 12);
            var btnRt = btnRow.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.20f, 0.06f);
            btnRt.anchorMax = new Vector2(0.98f, 0.16f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            // Make the button bar container fully transparent (remove grey background), not the buttons themselves
            try { var bg = btnRow.GetComponent<Image>(); if (bg != null) { var c = bg.color; c.a = 0f; bg.color = c; bg.raycastTarget = false; } } catch { }
            // Parent under the card root so it clips and layers correctly with the themed background
            try { btnRow.transform.SetParent(cardRoot.transform, false); btnRow.transform.SetAsLastSibling(); } catch { }

            changeRoleButton = UIFactory.CreateButton(btnRow, "Emp_ChangeRole", "Change Role");
            changeRoleButton.ButtonText.fontSize = 14;
            // Brownish-orange theme on button background; keep text readable
            changeRoleButton.ButtonText.color = Color.white;
            changeRoleButton.ButtonText.fontStyle = FontStyle.Bold;
            SetupButton(changeRoleButton, new Color(0.72f, 0.42f, 0.18f, 1f));
            try { var leCR = changeRoleButton.Component.gameObject.GetComponent<LayoutElement>() ?? changeRoleButton.Component.gameObject.AddComponent<LayoutElement>(); leCR.preferredWidth = 180f; leCR.minHeight = 36f; } catch { }
            changeRoleButton.OnClick += () =>
            {
                Plugin.Logger.LogInfo("Change Role clicked (todo)");
            };

            fireButton = UIFactory.CreateButton(btnRow, "Emp_Fire", "Fire");
            fireButton.ButtonText.fontSize = 14;
            // Brownish-orange theme on button background; keep text readable
            fireButton.ButtonText.color = Color.white;
            fireButton.ButtonText.fontStyle = FontStyle.Bold;
            SetupButton(fireButton, new Color(0.66f, 0.34f, 0.12f, 1f));
            try { var leF = fireButton.Component.gameObject.GetComponent<LayoutElement>() ?? fireButton.Component.gameObject.AddComponent<LayoutElement>(); leF.preferredWidth = 140f; leF.minHeight = 36f; } catch { }
            fireButton.OnClick += () =>
            {
                Plugin.Logger.LogInfo("Fire clicked (todo)");
            };

            // (Removed debug label used during diagnostics)

            Hide();
            Plugin.Logger.LogInfo("EmployeeDetailsView.Build complete (hidden by default)");
        }

        private void SetupButton(ButtonRef btn, Color normal)
        {
            // Match existing button style helper
            var colors = btn.Component.colors;
            colors.normalColor = normal;
            colors.highlightedColor = normal * 1.1f;
            colors.pressedColor = normal * 0.9f;
            colors.selectedColor = normal;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.5f);
            btn.Component.colors = colors;
        }

        public void Show(Citizen citizen, Occupation occ, int addressId = -1, int rosterIndex = -1)
        {
            try
            {
                string empObjInfo = "<none>";
                try { if (occ != null && occ.employee != null) empObjInfo = occ.employee.ToString(); } catch { }
                Plugin.Logger.LogInfo($"EmployeeDetailsView.Show citizen={(citizen != null ? citizen.GetCitizenName() : "<null>")}, occ={(occ != null ? (string.IsNullOrEmpty(occ.name) ? (occ.preset != null ? occ.preset.name : "<no preset>") : occ.name) : "<null>")}, empObj={empObjInfo}, addrId={addressId}, rosterIdx={rosterIndex}");
                if (root == null) return;
                root.SetActive(true);
                // Ensure alpha and interaction are enabled
                try
                {
                    var cg = root.GetComponent<CanvasGroup>();
                    if (cg == null) cg = root.AddComponent<CanvasGroup>();
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
                catch { }

                // Fallback: if citizen not provided, try to obtain it from the occupation
                if (citizen == null && occ != null)
                {
                    try { citizen = occ.employee as Citizen; } catch { }
                }

                // Portrait: start with a placeholder tint; try to fetch immediately, then retry if needed
                try { var old = portrait.gameObject.GetComponent(Il2CppType.Of<PortraitRetryLoader>()) as PortraitRetryLoader; if (old != null) UnityEngine.Object.Destroy(old); } catch { }
                portrait.texture = null;
                portrait.color = new Color(0.25f, 0.25f, 0.25f, 1f);
                if (citizen != null && citizen.evidenceEntry != null)
                {
                    var keys = new Il2CppSystem.Collections.Generic.List<Evidence.DataKey>();
                    keys.Add(Evidence.DataKey.photo);
                    Texture2D tex = citizen.evidenceEntry.GetPhoto(keys);
                    if (tex != null)
                    {
                        portrait.texture = tex;
                        portrait.color = Color.white;
                    }
                }
                // If we still don't have a texture, spin up a lightweight retry loader (IL2CPP-safe)
                try
                {
                    if (portrait.texture == null && (citizen != null || (addressId >= 0 && rosterIndex >= 0)))
                    {
                        var t = Il2CppType.Of<PortraitRetryLoader>();
                        var go = portrait.gameObject;
                        var comp = go.GetComponent(t) as PortraitRetryLoader;
                        if (comp == null) comp = go.AddComponent(t) as PortraitRetryLoader;
                        if (comp != null)
                        {
                            comp.target = portrait;
                            comp.citizen = citizen;
                            comp.addressId = addressId;
                            comp.rosterIndex = rosterIndex;
                            comp.interval = 0.25f;
                            comp.timeLeft = 2.0f;
                        }
                    }
                }
                catch { }

                // Name (fallback to occ.employee if not a Citizen)
                string fallbackName = null;
                try { if (occ != null && occ.employee != null && occ.employee.name != null) fallbackName = occ.employee.name.ToString(); } catch { }
                nameText.text = citizen != null ? citizen.GetCitizenName() : (!string.IsNullOrEmpty(fallbackName) ? fallbackName : "Vacant");

                // Job
                string role = "Worker";
                try
                {
                    if (occ != null)
                    {
                        if (!string.IsNullOrEmpty(occ.name)) role = occ.name;
                        else if (occ.preset != null && !string.IsNullOrEmpty(occ.preset.name)) role = occ.preset.name;
                    }
                }
                catch { }
                jobText.text = role;

                // Salary from Occupation if available
                string salaryStr = "Salary: N/A";
                try
                {
                    if (occ != null)
                    {
                        if (!string.IsNullOrEmpty(occ.salaryString))
                            salaryStr = $"Salary: {occ.salaryString}";
                        else if (occ.salary > 0)
                            salaryStr = $"Salary: {CityControls.Instance.cityCurrency}{Mathf.RoundToInt(occ.salary * 1000f)}";
                    }
                }
                catch { }
                salaryText.text = salaryStr;
                try { Canvas.ForceUpdateCanvases(); } catch { }
                // Log rects
                try
                {
                    var rt = root.GetComponent<RectTransform>();
                    Plugin.Logger.LogInfo($"EmpDetails rect: {rt.rect.width}x{rt.rect.height}");
                    Plugin.Logger.LogInfo($"Portrait pos={portrait.rectTransform.anchoredPosition} size={portrait.rectTransform.rect.size}");
                    Plugin.Logger.LogInfo($"Name rect size={nameText.rectTransform.rect.size}");
                    Plugin.Logger.LogInfo($"Job rect size={jobText.rectTransform.rect.size}");
                    Plugin.Logger.LogInfo($"Salary rect size={salaryText.rectTransform.rect.size}");
                }
                catch { }
                Plugin.Logger.LogInfo("EmployeeDetailsView.Show populated UI successfully");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error showing employee details: {ex.Message}");
            }
        }

        // When we use this view as its own panel, expand its size for a richer layout.
        public void ConfigureAsPanelLayout()
        {
            try
            {
                if (root == null) return;
                var rt = root.GetComponent<RectTransform>();
                // Occupy the center area generously
                rt.anchorMin = new Vector2(0.03f, 0.15f);
                rt.anchorMax = new Vector2(0.97f, 0.86f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // Keep existing child anchors; bump some font sizes for readability
                if (nameText != null) nameText.fontSize = 22;
                if (jobText != null) jobText.fontSize = 16;
                if (salaryText != null) salaryText.fontSize = 16;

                // Ensure we render above the parent content
                var parentCanvas = root.GetComponentInParent<Canvas>();
                var ownCanvas = root.GetComponent<Canvas>();
                if (ownCanvas == null)
                    ownCanvas = root.AddComponent<Canvas>();
                ownCanvas.overrideSorting = true;
                // Compute desired sorting in a safe range to avoid 16-bit overflow (-32768..32767)
                int parentOrder = parentCanvas != null ? parentCanvas.sortingOrder : 5000;
                if (parentOrder < 0) parentOrder = 20000;
                int desired = parentOrder + 10;
                if (desired > 32760) desired = 32760;
                if (desired < -32760) desired = -32760;
                ownCanvas.sortingOrder = desired;
                try { Plugin.Logger.LogInfo($"EmpDetails Configure: parentOrder={parentOrder} -> ownOrder={ownCanvas.sortingOrder}"); } catch { }
                if (root.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // Be drawn after siblings in this container
                root.transform.SetAsLastSibling();

                // Ensure visible (no accidental alpha 0)
                var cg = root.GetComponent<CanvasGroup>();
                if (cg == null) cg = root.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // Background should not block clicks to bottom buttons outside our rect
                try
                {
                    var bg = root.transform.Find("Emp_BG");
                    if (bg != null)
                    {
                        var img = bg.GetComponent<UnityEngine.UI.Image>();
                        if (img != null) img.raycastTarget = false;
                    }
                }
                catch { }

                // Add a center debug label to confirm drawing
                try
                {
                    var debugLbl = UIFactory.CreateLabel(root, "Emp_Debug", "[Employee Details Loaded]", TextAnchor.MiddleCenter);
                    var dRt = debugLbl.GetComponent<RectTransform>();
                    dRt.anchorMin = new Vector2(0.1f, 0.45f);
                    dRt.anchorMax = new Vector2(0.9f, 0.55f);
                    dRt.offsetMin = Vector2.zero;
                    dRt.offsetMax = Vector2.zero;
                    debugLbl.fontSize = 16;
                    debugLbl.color = new Color(0.9f, 0.9f, 0.2f, 1f);
                }
                catch { }

                // Log rect size for diagnostics
                try { Plugin.Logger.LogInfo($"EmpDetails root rect size: {rt.rect.width}x{rt.rect.height}"); } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error configuring details panel layout: {ex.Message}");
            }
        }

        // Ensure this view is above siblings and visible
        public void BringToFront()
        {
            try
            {
                if (root == null) return;
                var parentCanvas = root.GetComponentInParent<Canvas>();
                var ownCanvas = root.GetComponent<Canvas>();
                if (ownCanvas != null)
                {
                    try
                    {
                        ownCanvas.overrideSorting = true;
                        int parentOrder = parentCanvas != null ? parentCanvas.sortingOrder : 5000;
                        if (parentOrder < 0) parentOrder = 20000;
                        int desired = parentOrder + 10;
                        if (desired > 32760) desired = 32760;
                        if (desired < -32760) desired = -32760;
                        ownCanvas.sortingOrder = desired;
                    }
                    catch { }
                }
                root.transform.SetAsLastSibling();
                try { Plugin.Logger.LogInfo("EmpDetails BringToFront done (sibling last)"); } catch { }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"BringToFront error: {ex.Message}");
            }
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }
    }
}

// Lightweight, IL2CPP-safe retry helper to fetch a citizen portrait shortly after the view appears.
// Uses Update with unscaled time, avoids coroutines/lambdas/generics.
namespace BackInBusiness
{
    internal class PortraitRetryLoader : MonoBehaviour
    {
        public RawImage target;
        public Citizen citizen;
        public int addressId = -1;
        public int rosterIndex = -1;
        public float interval = 0.25f; // seconds between attempts
        public float timeLeft = 2.0f;  // total time budget for retries

        private float _timer;

        private void OnEnable()
        {
            _timer = interval;
        }

        private void Update()
        {
            try
            {
                // Bail if prerequisites are missing or we ran out of time
                if (target == null)
                {
                    Destroy(this);
                    return;
                }

                float dt = Time.unscaledDeltaTime;
                _timer -= dt;
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    Destroy(this);
                    return;
                }

                if (_timer > 0f)
                    return;

                _timer = interval > 0.05f ? interval : 0.05f;

                // If we don't have a citizen reference, try to resolve via address/roster once we tick
                if (citizen == null && addressId >= 0 && rosterIndex >= 0)
                {
                    try
                    {
                        var player = Player.Instance;
                        if (player != null && player.apartmentsOwned != null)
                        {
                            for (int i = 0; i < player.apartmentsOwned.Count; i++)
                            {
                                var apt = player.apartmentsOwned[i];
                                if (apt == null) continue;
                                if (apt.id != addressId) continue;
                                if (apt.company != null && apt.company.companyRoster != null)
                                {
                                    if (rosterIndex >= 0 && rosterIndex < apt.company.companyRoster.Count)
                                    {
                                        var occNow = apt.company.companyRoster[rosterIndex];
                                        if (occNow != null)
                                        {
                                            try { citizen = occNow.employee as Citizen; } catch { }
                                        }
                                    }
                                }
                                break; // Found address, stop searching
                            }
                        }
                    }
                    catch { }
                }

                // Attempt to fetch the portrait
                try
                {
                    if (citizen != null && citizen.evidenceEntry != null)
                    {
                        var keys = new Il2CppSystem.Collections.Generic.List<Evidence.DataKey>();
                        keys.Add(Evidence.DataKey.photo);
                        Texture2D tex = citizen.evidenceEntry.GetPhoto(keys);
                        if (tex != null)
                        {
                            target.texture = tex;
                            target.color = Color.white;
                            try { Plugin.Logger.LogInfo("PortraitRetryLoader: portrait acquired on retry"); } catch { }
                            Destroy(this);
                            return;
                        }
                    }
                }
                catch { }
            }
            catch { Destroy(this); }
        }
    }
}
