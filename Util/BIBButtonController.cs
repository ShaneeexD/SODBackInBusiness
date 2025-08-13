using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BackInBusiness
{
    // A local controller that mirrors the game's ButtonController visuals/behavior, but routes click to our UI logic.
    // It manages hover/press states, brightness/scale, and the additional highlight child (cloned from the game's template).
    public class BIBButtonController : MonoBehaviour
    {
        public Image TargetImage;          // button image we tint
        public RectTransform TargetTransform; // button rect we scale
        public AudioEvent buttonDown;
        public float NormalScale = 1.0f;
        public float PressedScale = 0.95f;
        public float HoverBrightness = 1.15f;
        public float PressedBrightness = 0.9f;

        public bool UseAdditionalHighlight = true;
        public bool UseCloneHighlight = true;
        public bool AdditionalHighlightAtFront = true;
        public Color AdditionalHighlightColour = new Color(1f, 1f, 1f, 1f);
        public Color AdditionalHighlightUninteractableColour = Color.gray;
        // Insets for sizing the highlight relative to its parent (left, top, right, bottom)
        // Negative values expand beyond parent edges, positive values shrink inside parent edges
        public Vector4 AdditionalHighlightRectModifier = new Vector4(-12f, -12f, -12f, -12f);
        // Base transform adjustments applied via a wrapper so the in-game animation (which changes localScale) is preserved
        public Vector3 AdditionalHighlightLocalPositionOffset = new Vector3(0f, 0f, 0f);
        public float AdditionalHighlightScaleMultiplier = 1.0f;
        public bool ForceWrapperOffsetEachFrame = true;
        public bool ForceRectSizeEachFrame = true;
        public bool ForceOpaqueHighlight = true;
        public bool VerboseHighlightLogs = false;

        private bool _hovered, _pressed, _interactable = true;
        private Color _origColor;
        private Vector3 _origScale;

        private RectTransform _highlightRect;
        private RectTransform _highlightWrapper;
        private Image _highlightImage;

        private static Transform _cachedHighlightTemplate;
        private static bool _searchedHighlightTemplate;

        public void Awake()
        {
            if (TargetTransform == null) TargetTransform = GetComponent<RectTransform>();
            if (TargetImage == null) TargetImage = GetComponent<Image>();
            _origScale = TargetTransform != null ? TargetTransform.localScale : Vector3.one;
            _origColor = TargetImage != null ? TargetImage.color : Color.white;
            if (NormalScale <= 0f) NormalScale = 1f;

            // Create highlight now (disabled), per user request: "make it along with the close button"
            if (UseAdditionalHighlight)
                SetupAdditionalHighlight();

            try { Plugin.Logger?.LogInfo($"[BIBButtonController] Awake on {gameObject.name}. UseAdditionalHighlight={UseAdditionalHighlight}, UseCloneHighlight={UseCloneHighlight}"); } catch { }
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            ApplyVisuals();
            UpdateAdditionalHighlight();
        }

        public void OnPointerEnter(PointerEventData _)
        {
            _hovered = true;
            ApplyVisuals();
            UpdateAdditionalHighlight();
        }

        public void OnPointerExit(PointerEventData _)
        {
            _hovered = false;
            _pressed = false;
            ApplyVisuals();
            UpdateAdditionalHighlight();
        }

        public void OnPointerDown(PointerEventData _)
        {
            if (!_interactable) return;
            _pressed = true;
            AudioController.Instance.Play2DSound(this.buttonDown, null, 1f);
            ApplyVisuals();
        }

        public void OnPointerUp(PointerEventData _)
        {
            _pressed = false;
            ApplyVisuals();
        }

        public void OnPointerClick(PointerEventData _)
        {
            if (!_interactable) return;
            // Click behavior is handled by Button.onClick wiring in EmployeeDetailsView.
        }

        private void ApplyVisuals()
        {
            try
            {
                if (TargetImage != null)
                {
                    var c = _origColor;
                    if (!_interactable)
                    {
                        c.a *= 1.0f;
                    }
                    else if (_pressed)
                    {
                        c.r = Mathf.Clamp01(c.r * PressedBrightness);
                        c.g = Mathf.Clamp01(c.g * PressedBrightness);
                        c.b = Mathf.Clamp01(c.b * PressedBrightness);
                    }
                    else if (_hovered)
                    {
                        c.r = Mathf.Clamp01(c.r * HoverBrightness);
                        c.g = Mathf.Clamp01(c.g * HoverBrightness);
                        c.b = Mathf.Clamp01(c.b * HoverBrightness);
                    }
                    TargetImage.color = c;
                }
            }
            catch { }
            try
            {
                if (TargetTransform != null)
                {
                    float s = (!_hovered && !_pressed) ? NormalScale : (_pressed ? PressedScale : NormalScale);
                    TargetTransform.localScale = _origScale * s;
                }
            }
            catch { }
        }

        private void SetupAdditionalHighlight()
        {
            try
            {
                // Create highlight child under this button.
                GameObject go = null;
                if (UseCloneHighlight)
                {
                    var tmpl = GetHighlightTemplateFromScene();
                    if (tmpl != null)
                    {
                        try
                        {
                            go = GameObject.Instantiate(tmpl.gameObject);
                            // Strip unwanted components
                            foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                            try { var bc = go.GetComponent("ButtonController"); if (bc != null) Destroy(bc); } catch { }
                            try { var et = go.GetComponent<EventTrigger>(); if (et != null) Destroy(et); } catch { }
                            try { foreach (var c in go.GetComponentsInChildren<Canvas>(true)) Destroy(c); } catch { }
                            try { foreach (var c in go.GetComponentsInChildren<CanvasScaler>(true)) Destroy(c); } catch { }
                            try { foreach (var c in go.GetComponentsInChildren<GraphicRaycaster>(true)) Destroy(c); } catch { }
                            try { Plugin.Logger?.LogInfo("[BIBButtonController] Cloned AdditionalHighlight from template."); } catch { }
                        }
                        catch { go = null; }
                    }
                }
                if (go == null)
                {
                    // Fallback simple overlay (avoid GameObject(Type[]) overload for IL2CPP compatibility)
                    go = new GameObject("AdditionalHighlight_Fallback");
                    var rtCreated = go.GetComponent<RectTransform>();
                    if (rtCreated == null) rtCreated = go.AddComponent<RectTransform>();
                    var img = go.GetComponent<Image>();
                    if (img == null) img = go.AddComponent<Image>();
                    img.color = AdditionalHighlightColour;
                    // For fallback, use a proper 9-slice sprite with small corners
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 1.0f;
                    img.raycastTarget = false;
                    try { Plugin.Logger?.LogInfo("[BIBButtonController] Created fallback AdditionalHighlight overlay."); } catch { }
                }

                // Create a wrapper under the button, stretch to parent; apply base scale on wrapper
                var wrapper = new GameObject("AdditionalHighlight_Wrapper");
                var wrapperRT = wrapper.AddComponent<RectTransform>();
                wrapperRT.SetParent(TargetTransform, false);
                // Center-anchored wrapper so we can offset safely without interfering with child's anchors/pivot
                wrapperRT.anchorMin = new Vector2(0.5f, 0.5f);
                wrapperRT.anchorMax = new Vector2(0.5f, 0.5f);
                wrapperRT.pivot = new Vector2(0.5f, 0.5f);
                wrapperRT.anchoredPosition3D = AdditionalHighlightLocalPositionOffset;
                wrapperRT.anchoredPosition = new Vector2(AdditionalHighlightLocalPositionOffset.x, AdditionalHighlightLocalPositionOffset.y);
                // Ensure wrapper has a size so stretched children don't collapse to zero if the prefab uses stretch anchors
                try { wrapperRT.sizeDelta = TargetTransform != null ? TargetTransform.rect.size : Vector2.zero; } catch { }
                // Ensure layout groups do not override our anchored position
                try { var le = wrapper.AddComponent<LayoutElement>(); le.ignoreLayout = true; } catch { }
                wrapperRT.localScale = Vector3.one * Mathf.Max(0.01f, AdditionalHighlightScaleMultiplier);
                if (!AdditionalHighlightAtFront) wrapperRT.SetAsFirstSibling(); else wrapperRT.SetAsLastSibling();
                _highlightWrapper = wrapperRT;

                // Parent the highlight under wrapper and set its anchored position offset so animation scale is preserved
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.SetParent(wrapperRT, false);
                // Stretch to parent and apply game-style insets via Toolbox.SetRectSize(left, top, right, bottom)
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                // Ensure the Image is set to Sliced type for proper corner rendering
                var rtImage = rt.GetComponent<Image>();
                if (rtImage != null)
                {
                    rtImage.type = Image.Type.Sliced;
                    // Set sprite pixels per unit to match game's UI scale
                    rtImage.pixelsPerUnitMultiplier = 1.0f;
                }
                try
                {
                    if (Toolbox.Instance != null)
                    {
                        Toolbox.Instance.SetRectSize(rt,
                            AdditionalHighlightRectModifier.x,
                            AdditionalHighlightRectModifier.y,
                            AdditionalHighlightRectModifier.z,
                            AdditionalHighlightRectModifier.w);
                    }
                }
                catch { }
                rt.localPosition = Vector3.zero;
                rt.localScale = Vector3.one; // animated by game controller if present

                _highlightRect = rt; // We toggle the active state on the highlight child, not the wrapper
                _highlightImage = go.GetComponent<Image>();
                if (_highlightImage != null) _highlightImage.raycastTarget = false;

                // Disabled by default; will be enabled only when hovered
                _highlightRect.gameObject.SetActive(false);
                try { Plugin.Logger?.LogInfo($"[BIBButtonController] Highlight wrapper pos={_highlightWrapper.anchoredPosition}, child localPos={_highlightRect.localPosition}, offset={AdditionalHighlightLocalPositionOffset}, scaleMult={AdditionalHighlightScaleMultiplier} under {gameObject.name}, active=false"); } catch { }
            }
            catch { }
        }

        private Transform GetHighlightTemplateFromScene()
        {
            if (_cachedHighlightTemplate != null) return _cachedHighlightTemplate;
            if (_searchedHighlightTemplate) return null;
            _searchedHighlightTemplate = true;
            try
            {
                // Direct known path
                var path = "GameCanvas/BioDisplayCanvas/InventoryDisplayArea/DeselectSlotArea/ButtonArea/DropButton/ButtonAdditionalHighlight(Clone)";
                var go = GameObject.Find(path);
                if (go == null)
                {
                    // Safe fallback: find by name once (local to this call)
                    var all = GameObject.FindObjectsOfType<Transform>(true);
                    foreach (var t in all)
                    {
                        if (t != null && t.name != null && t.name.Contains("ButtonAdditionalHighlight")) { go = t.gameObject; break; }
                    }
                }
                if (go != null) _cachedHighlightTemplate = go.transform;
                try { Plugin.Logger?.LogInfo($"[BIBButtonController] Template lookup result: {(_cachedHighlightTemplate != null ? _cachedHighlightTemplate.name : "null")}"); } catch { }
            }
            catch { }
            return _cachedHighlightTemplate;
        }

        private void UpdateAdditionalHighlight()
        {
            if (!UseAdditionalHighlight || _highlightRect == null) return;
            bool show = _interactable && _hovered;
            if (show)
            {
                if (!_highlightRect.gameObject.activeSelf)
                {
                    // Set color for state each time we show
                    var baseCol = _interactable ? AdditionalHighlightColour : AdditionalHighlightUninteractableColour;
                    if (ForceOpaqueHighlight) baseCol.a = 1f;
                    if (_highlightImage != null) _highlightImage.color = baseCol;
                    // Force color for all child graphics to guarantee opacity and tint
                    try
                    {
                        var graphics = _highlightRect.GetComponentsInChildren<MaskableGraphic>(true);
                        for (int i = 0; i < graphics.Length; i++)
                        {
                            graphics[i].color = baseCol;
                        }
                    }
                    catch { }
                    // Log ancestor CanvasGroup chain once to help diagnose unexpected transparency
                    try
                    {
                        var t = _highlightRect.transform;
                        int hops = 0;
                        while (t != null && hops < 8)
                        {
                            var cg = t.GetComponent<CanvasGroup>();
                            if (cg != null)
                            {
                                Plugin.Logger?.LogInfo($"[BIBButtonController] CanvasGroup on '{t.name}' alpha={cg.alpha}, interactable={cg.interactable}, blocksRaycasts={cg.blocksRaycasts}");
                            }
                            t = t.parent;
                            hops++;
                        }
                    }
                    catch { }
                    _highlightRect.gameObject.SetActive(true);
                    try
                    {
                        Plugin.Logger?.LogInfo("[BIBButtonController] Highlight enabled (hover)");
                        if (VerboseHighlightLogs && _highlightRect != null)
                        {
                            var rt = _highlightRect;
                            Plugin.Logger?.LogInfo($"[BIBButtonController] rt.anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} offsetMin={rt.offsetMin} offsetMax={rt.offsetMax} sizeDelta={rt.sizeDelta} parentSize={(TargetTransform!=null?TargetTransform.rect.size:Vector2.zero)} insets(L,T,R,B)=({AdditionalHighlightRectModifier.x},{AdditionalHighlightRectModifier.y},{AdditionalHighlightRectModifier.z},{AdditionalHighlightRectModifier.w})");
                        }
                    }
                    catch { }
                }
            }
            else
            {
                if (_highlightRect.gameObject.activeSelf)
                {
                    _highlightRect.gameObject.SetActive(false);
                    try { Plugin.Logger?.LogInfo("[BIBButtonController] Highlight disabled (exit)"); } catch { }
                }
            }
        }

        private void LateUpdate()
        {
            // Some layouts/scripts may reset anchored position; re-assert if requested
            try
            {
                if (ForceWrapperOffsetEachFrame && _highlightWrapper != null)
                {
                    _highlightWrapper.anchoredPosition3D = AdditionalHighlightLocalPositionOffset;
                    _highlightWrapper.anchoredPosition = new Vector2(AdditionalHighlightLocalPositionOffset.x, AdditionalHighlightLocalPositionOffset.y);
                    // Keep wrapper size tracking parent button so child stretch + insets behave consistently
                    if (TargetTransform != null)
                    {
                        _highlightWrapper.sizeDelta = TargetTransform.rect.size;
                    }
                }
                // Allow live tweaking of insets in the inspector during play by reapplying each frame
                if (ForceRectSizeEachFrame && _highlightRect != null)
                {
                    _highlightRect.anchorMin = Vector2.zero;
                    _highlightRect.anchorMax = Vector2.one;
                    if (Toolbox.Instance != null)
                    {
                        Toolbox.Instance.SetRectSize(_highlightRect,
                            AdditionalHighlightRectModifier.x,
                            AdditionalHighlightRectModifier.y,
                            AdditionalHighlightRectModifier.z,
                            AdditionalHighlightRectModifier.w);
                    }
                    if (VerboseHighlightLogs && (Time.frameCount % 15 == 0))
                    {
                        var rt = _highlightRect;
                        try { Plugin.Logger?.LogInfo($"[BIBButtonController] (LateUpdate) rt.anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} offsetMin={rt.offsetMin} offsetMax={rt.offsetMax} sizeDelta={rt.sizeDelta} parentSize={(TargetTransform!=null?TargetTransform.rect.size:Vector2.zero)} insets(L,T,R,B)=({AdditionalHighlightRectModifier.x},{AdditionalHighlightRectModifier.y},{AdditionalHighlightRectModifier.z},{AdditionalHighlightRectModifier.w})"); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
