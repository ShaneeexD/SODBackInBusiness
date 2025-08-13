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

        public float NormalScale = 1.0f;
        public float PressedScale = 0.95f;
        public float HoverBrightness = 1.15f;
        public float PressedBrightness = 0.9f;

        public bool UseAdditionalHighlight = true;
        public bool UseCloneHighlight = true;
        public bool AdditionalHighlightAtFront = true;
        public Color AdditionalHighlightColour = new Color(1f, 1f, 1f, 0.35f);
        public Color AdditionalHighlightUninteractableColour = Color.gray;

        private bool _hovered, _pressed, _interactable = true;
        private Color _origColor;
        private Vector3 _origScale;

        private RectTransform _highlightRect;
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
                        c.a *= 0.5f;
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
                    img.raycastTarget = false;
                    try { Plugin.Logger?.LogInfo("[BIBButtonController] Created fallback AdditionalHighlight overlay."); } catch { }
                }

                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.SetParent(TargetTransform, false);
                if (!AdditionalHighlightAtFront) rt.SetAsFirstSibling(); else rt.SetAsLastSibling();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localPosition = Vector3.zero;

                _highlightRect = rt;
                _highlightImage = go.GetComponent<Image>();
                if (_highlightImage != null) _highlightImage.raycastTarget = false;

                // Disabled by default; will be enabled only when hovered
                _highlightRect.gameObject.SetActive(false);
                try { Plugin.Logger?.LogInfo($"[BIBButtonController] Highlight child set under {gameObject.name}, active=false"); } catch { }
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
            if (!UseAdditionalHighlight || _highlightRect == null || _highlightImage == null) return;
            bool show = _interactable && _hovered;
            if (show)
            {
                if (!_highlightRect.gameObject.activeSelf)
                {
                    // Set color for state each time we show
                    var baseCol = _interactable ? AdditionalHighlightColour : AdditionalHighlightUninteractableColour;
                    if (_highlightImage != null) _highlightImage.color = baseCol;
                    _highlightRect.gameObject.SetActive(true);
                    try { Plugin.Logger?.LogInfo("[BIBButtonController] Highlight enabled (hover)"); } catch { }
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
    }
}
