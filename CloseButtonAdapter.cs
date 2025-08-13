using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniverseLib;

namespace BackInBusiness
{
    // Lightweight adapter to provide hover/press visual feedback and a click callback
    // without relying on the game's ButtonController. Safe for cloned prefabs.
    internal class CloseButtonAdapter : MonoBehaviour
    {
        public Action OnClick;
        public Image TargetImage;
        public RectTransform TargetTransform;
        public bool Interactable = true;

        // Visual settings
        public float HoverBrightness = 1.08f;   // brighten on hover
        public float PressedBrightness = 0.95f; // slightly darker on press
        public float PressedScale = 0.92f;
        public float NormalScale = 1f;

        // Additional highlight (manual highlighter), mirrors game's concept
        public bool UseAdditionalHighlight = true;
        public Color AdditionalHighlightColour = new Color(1f, 1f, 1f, 0.35f);
        public Color AdditionalHighlightUninteractableColour = Color.gray;
        public bool AdditionalHighlightAtFront = true;
        // x,y = expand min; z,w = expand max (left, bottom, right, top)
        public Vector4 AdditionalHighlightRectModifier = Vector4.zero;
        private RectTransform _additionalHighlightRect;
        private Image _additionalHImage;
        private bool _additionalHighlighted;
        private bool _forceAdditionalHighlighted;

        // Template cloning (from game scene) for authentic look
        public bool UseCloneHighlight = true;
        private static Transform _cachedHighlightTemplate;
        private static bool _searchedHighlightTemplate;
        public bool EnableNudgeOnClick = false;

        private Color _origColor;
        private Vector3 _origScale;
        private bool _hovered;
        private bool _pressed;

        private void Awake()
        {
            if (TargetTransform == null) TargetTransform = transform as RectTransform;
            if (TargetImage == null) TargetImage = GetComponent<Image>();
            _origScale = TargetTransform != null ? TargetTransform.localScale : Vector3.one;
            _origColor = TargetImage != null ? TargetImage.color : Color.white;
            if (NormalScale <= 0f) NormalScale = 1f;
        }

        public void SetInteractable(bool value)
        {
            Interactable = value;
            ApplyVisuals();
            UpdateAdditionalHighlight();
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _hovered = true; ApplyVisuals(); UpdateAdditionalHighlight();
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _hovered = false; _pressed = false; ApplyVisuals(); UpdateAdditionalHighlight();
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!Interactable) return;
            _pressed = true; ApplyVisuals();
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _pressed = false; ApplyVisuals();
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!Interactable) return;
            // Optional: Visual nudge similar to the game's "nudgeOnClick"
            if (EnableNudgeOnClick && !_nudgeRunning)
            {
                try { UniverseLib.RuntimeHelper.StartCoroutine(Nudge()); } catch { }
            }
            try { OnClick?.Invoke(); } catch { }
        }

        private void ApplyVisuals()
        {
            try
            {
                if (TargetImage != null)
                {
                    var c = _origColor;
                    if (!Interactable)
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
                    TargetImage.raycastTarget = true;
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
                if (TargetTransform == null) return;
                if (_additionalHighlightRect != null) return;
                GameObject go = null;
                Image img = null;
                // Try clone from template first for visual parity
                if (UseCloneHighlight)
                {
                    var tmpl = GetHighlightTemplate();
                    if (tmpl != null)
                    {
                        try
                        {
                            var clone = GameObject.Instantiate(tmpl.gameObject);
                            go = clone;
                            // Strip raycasts and any undesired behaviours
                            foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                            // Remove any ButtonController or EventTriggers if present (safety)
                            try { var bc = go.GetComponent("ButtonController"); if (bc != null) Destroy(bc); } catch { }
                            try { var et = go.GetComponent<EventTrigger>(); if (et != null) Destroy(et); } catch { }
                            // Remove Canvas-related components that can break hierarchy draw/raycast
                            try { foreach (var c in go.GetComponentsInChildren<Canvas>(true)) Destroy(c); } catch { }
                            try { foreach (var c in go.GetComponentsInChildren<CanvasScaler>(true)) Destroy(c); } catch { }
                            try { foreach (var c in go.GetComponentsInChildren<GraphicRaycaster>(true)) Destroy(c); } catch { }
                        }
                        catch { go = null; }
                    }
                }
                if (go == null)
                {
                    go = new GameObject("AdditionalHighlight");
                    img = go.AddComponent<Image>();
                    img.color = AdditionalHighlightColour;
                    img.raycastTarget = false;
                }
                var rt = go.GetComponent<RectTransform>(); if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.SetParent(TargetTransform, false);
                rt.localScale = Vector3.one;
                rt.localPosition = Vector3.zero;
                // Place behind or in front
                if (!AdditionalHighlightAtFront) rt.SetAsFirstSibling(); else rt.SetAsLastSibling();
                // Stretch to target with optional offsets
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(AdditionalHighlightRectModifier.x, AdditionalHighlightRectModifier.y);
                rt.offsetMax = new Vector2(-AdditionalHighlightRectModifier.z, -AdditionalHighlightRectModifier.w);
                if (img == null) img = go.GetComponentInChildren<Image>(true);
                _additionalHighlightRect = rt;
                _additionalHImage = img;
                if (_additionalHImage != null) _additionalHImage.raycastTarget = false;
                // Disabled by default; will be enabled only on hover
                _additionalHighlightRect.gameObject.SetActive(false);
            }
            catch { }
        }

        private static Transform GetHighlightTemplate()
        {
            if (_searchedHighlightTemplate) return _cachedHighlightTemplate;
            _searchedHighlightTemplate = true;
            try
            {
                // Direct path provided
                var path = "GameCanvas/BioDisplayCanvas/InventoryDisplayArea/DeselectSlotArea/ButtonArea/DropButton/ButtonAdditionalHighlight(Clone)";
                var go = GameObject.Find(path);
                if (go != null) _cachedHighlightTemplate = go.transform;
            }
            catch { }
            return _cachedHighlightTemplate;
        }

        public void SetForceAdditionalHighlight(bool value)
        {
            _forceAdditionalHighlighted = value;
            UpdateAdditionalHighlight();
        }

        private void UpdateAdditionalHighlight()
        {
            if (!UseAdditionalHighlight)
                return;
            // Lazy-create highlight only when we need to show it
            if (_additionalHighlightRect == null || _additionalHImage == null)
            {
                // Determine desired state before creating
                bool wantShow = (_hovered || _forceAdditionalHighlighted) && UseAdditionalHighlight;
                if (!wantShow) return; // don't create until we actually need it
                SetupAdditionalHighlight();
                if (_additionalHighlightRect == null || _additionalHImage == null) return;
            }
            bool show = (_hovered || _forceAdditionalHighlighted) && UseAdditionalHighlight;
            if (!_additionalHighlighted && show)
            {
                _additionalHighlighted = true;
                _additionalHighlightRect.gameObject.SetActive(true);
                if (_additionalHImage != null)
                {
                    var baseCol = Interactable ? AdditionalHighlightColour : AdditionalHighlightUninteractableColour;
                    _additionalHImage.color = baseCol;
                }
            }
            else if (_additionalHighlighted && !show)
            {
                _additionalHighlighted = false;
                _additionalHighlightRect.gameObject.SetActive(false);
            }
        }

        private int _flashRepeat;
        private Color _flashColour;
        private int _flashToken;
        private bool _nudgeRunning;

        public void Flash(int repeat, Color flashColour)
        {
            _flashRepeat = repeat;
            _flashColour = flashColour;
            unchecked { _flashToken++; }
            try { UniverseLib.RuntimeHelper.StartCoroutine(FlashRoutineImpl(_flashToken)); } catch { }
        }

        private System.Collections.IEnumerator FlashRoutineImpl(int token)
        {
            if (TargetImage == null) yield break;
            int cycle = 0; float progress = 0f; float speed = 10f;
            while (cycle < _flashRepeat && progress < 2f)
            {
                if (token != _flashToken) yield break;
                progress += speed * Time.deltaTime;
                float t = (progress <= 1f) ? progress : (2f - progress);
                TargetImage.color = Color.Lerp(_origColor, _flashColour, t);
                if (progress >= 2f)
                {
                    cycle++;
                    progress = 0f;
                }
                yield return null;
            }
            TargetImage.color = _origColor;
        }

        private System.Collections.IEnumerator Nudge()
        {
            if (TargetTransform == null) yield break;
            _nudgeRunning = true;
            float t = 0f; float dur = 0.08f; float minScale = PressedScale; // quick down-up
            // down
            while (t < dur)
            {
                t += Time.deltaTime; float a = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(NormalScale, minScale, a);
                TargetTransform.localScale = _origScale * s;
                yield return null;
            }
            // up
            t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float a = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(minScale, NormalScale, a);
                TargetTransform.localScale = _origScale * s;
                yield return null;
            }
            TargetTransform.localScale = _origScale * NormalScale;
            _nudgeRunning = false;
        }
    }
}
