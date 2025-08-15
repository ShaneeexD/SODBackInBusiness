using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UniverseLib.UI;
using UniverseLib.UI.Panels;
using TMPro;

namespace BackInBusiness
{
    // Simple Yes/No confirmation panel using UniverseLib PanelBase
    // No background/title bar visible; content clones ClearSearch-style buttons
    public class FireYesNoPanel : PanelBase
    {
        // Simple callback relay to avoid IL2CPP delegate issues
        private class ClickRelay : MonoBehaviour
        {
            public System.Action onClick;
            public FireYesNoPanel panel;
            
            public void OnClick()
            {
                try { onClick?.Invoke(); } catch { }
                try { panel?.SetActive(false); } catch { }
            }
        }
        private class InitArgs
        {
            public string title;
            public string message;
            public string leftText;
            public string rightText;
            public Action onLeft;
            public Action onRight;
        }

        private static readonly Stack<InitArgs> Pending = new Stack<InitArgs>();

        public static void PushInit(string title, string message, string leftText, string rightText, Action onLeft, Action onRight)
        {
            try { Pending.Push(new InitArgs { title = title, message = message, leftText = leftText, rightText = rightText, onLeft = onLeft, onRight = onRight }); }
            catch { }
        }

        // Keep a simple constant name; title bar is hidden anyway
        public override string Name => "FireYesNo";
        public override int MinWidth => 300;
        public override int MinHeight => 140;
        public override Vector2 DefaultAnchorMin => new Vector2(0.35f, 0.40f);
        public override Vector2 DefaultAnchorMax => new Vector2(0.65f, 0.60f);

        public FireYesNoPanel(UIBase owner) : base(owner) { }

        protected override void ConstructPanelContent()
        {
            string title = "Confirm";
            string message = "Are you sure?";
            string leftText = "Cancel";
            string rightText = "OK";
            Action onLeft = null;
            Action onRight = null;

            try
            {
                if (Pending.Count > 0)
                {
                    var args = Pending.Pop();
                    if (args != null)
                    {
                        title = string.IsNullOrEmpty(args.title) ? title : args.title;
                        message = string.IsNullOrEmpty(args.message) ? message : args.message;
                        leftText = string.IsNullOrEmpty(args.leftText) ? leftText : args.leftText;
                        rightText = string.IsNullOrEmpty(args.rightText) ? rightText : args.rightText;
                        onLeft = args.onLeft;
                        onRight = args.onRight;
                    }
                }
            }
            catch { }

            try
            {
                // Hide background and header elements for a clean, modal-like look
                try
                {
                    var panelBg = ContentRoot.transform.parent.Find("Background");
                    if (panelBg != null) panelBg.gameObject.SetActive(false);
                    if (TitleBar != null) TitleBar.gameObject.SetActive(false);
                }
                catch { }

                // Build content container filling the panel
                GameObject content = UIFactory.CreateUIObject("FireYesNoContent", ContentRoot);
                var rt = content.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // Title text (optional, smaller)
                var titleGo = UIFactory.CreateUIObject("Title", content);
                var titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0, 1);
                titleRt.anchorMax = new Vector2(1, 1);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(0, 24);
                titleRt.anchoredPosition = new Vector2(0, -6);
                var titleTMP = CreateTMP(titleGo.transform, title, TextAlignmentOptions.Center);
                titleTMP.fontSize = 18f;

                // Message
                var msgGo = UIFactory.CreateUIObject("Message", content);
                var msgRt = msgGo.GetComponent<RectTransform>();
                msgRt.anchorMin = new Vector2(0.05f, 0.35f);
                msgRt.anchorMax = new Vector2(0.95f, 0.80f);
                msgRt.offsetMin = Vector2.zero;
                msgRt.offsetMax = Vector2.zero;
                var msgTMP = CreateTMP(msgGo.transform, message, TextAlignmentOptions.Center);
                msgTMP.fontSize = 16f;
                msgTMP.enableWordWrapping = true;

                // Buttons container
                var btnRow = UIFactory.CreateUIObject("Buttons", content);
                var rowRt = btnRow.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0.5f, 0.1f);
                rowRt.anchorMax = new Vector2(0.5f, 0.1f);
                rowRt.pivot = new Vector2(0.5f, 0.5f);
                rowRt.sizeDelta = new Vector2(260f, 44f);
                rowRt.anchoredPosition = Vector2.zero;

                // Left (Cancel)
                var leftGo = UIThemeCache.InstantiateClearSearchButton(btnRow.transform);
                if (leftGo != null)
                {
                    leftGo.name = "CancelButton";
                    var lrt = leftGo.GetComponent<RectTransform>();
                    lrt.anchorMin = new Vector2(0, 0.5f);
                    lrt.anchorMax = new Vector2(0, 0.5f);
                    lrt.pivot = new Vector2(0, 0.5f);
                    lrt.sizeDelta = new Vector2(120, 40);
                    lrt.anchoredPosition = new Vector2(0, 0);

                    var leftLabel = CreateTMP(leftGo.transform, leftText, TextAlignmentOptions.Center);
                    leftLabel.fontSize = 16f;
                    leftLabel.raycastTarget = false;
                    leftLabel.color = Color.black;

                    SetupButton(leftGo, onLeft);
                }

                // Right (Fire)
                var rightGo = UIThemeCache.InstantiateClearSearchButton(btnRow.transform);
                if (rightGo != null)
                {
                    rightGo.name = "FireButton";
                    var rrt = rightGo.GetComponent<RectTransform>();
                    rrt.anchorMin = new Vector2(1, 0.5f);
                    rrt.anchorMax = new Vector2(1, 0.5f);
                    rrt.pivot = new Vector2(1, 0.5f);
                    rrt.sizeDelta = new Vector2(90, 40);
                    rrt.anchoredPosition = new Vector2(0, 0);

                    var rightLabel = CreateTMP(rightGo.transform, rightText, TextAlignmentOptions.Center);
                    rightLabel.fontSize = 16f;
                    rightLabel.raycastTarget = false;
                    rightLabel.color = Color.black;

                    SetupButton(rightGo, onRight);
                }
            }
            catch (Exception ex)
            {
                try { Plugin.Logger.LogError($"FireYesNoPanel build error: {ex.Message}\n{ex.StackTrace}"); } catch { }
            }
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string text, TextAlignmentOptions align)
        {
            var go = new GameObject("Label");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(5, 5);
            rt.offsetMax = new Vector2(-5, -5);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.text = text ?? string.Empty;
            tmp.color = Color.white;
            return tmp;
        }

        private void SetupButton(GameObject go, Action onClick)
        {
            if (go == null) return;
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                // Ensure fully opaque sprite copied from template remains visible
                img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
                img.raycastTarget = true;
            }

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            try { btn.onClick.RemoveAllListeners(); } catch { }
            try { var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav; } catch { }
            try { btn.transition = Selectable.Transition.None; } catch { }

            // Attach relay for click handling (fallback only - main popup uses game's native UI)
            var relay = go.GetComponent<ClickRelay>() ?? go.AddComponent<ClickRelay>();
            relay.onClick = onClick;
            relay.panel = this;
            
            // Note: Skipping Button.onClick due to IL2CPP delegate issues. 
            // This FireYesNoPanel is just a fallback; main popup uses cloned TooltipCanvas/PopupMessage.

            // Optional: attach our highlight/sound controller if available
            try
            {
                var ctrl = go.GetComponent<BIBButtonController>();
                if (ctrl == null) ctrl = go.AddComponent<BIBButtonController>();
                ctrl.UseCloneHighlight = true;
                ctrl.UseAdditionalHighlight = true;
                ctrl.AdditionalHighlightAtFront = true;
                ctrl.AdditionalHighlightColour = new Color(1f, 1f, 1f, 0.35f);
                ctrl.useGenericAudioSounds = true;
                ctrl.isCloseButton = false;
                ctrl.TargetImage = img;
                if (ctrl.TargetTransform == null) ctrl.TargetTransform = go.GetComponent<RectTransform>();
            }
            catch { }
        }
    }
}
