using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Panels;
using SOD.Common;

namespace BackInBusiness
{
    // Lightweight floating panel to host one EmployeeDetailsView.
    // Uses UniverseLib PanelBase which already supports header dragging and close controls under IL2CPP.
    public class FloatingEmployeePanel : PanelBase
    {
        private readonly string _panelName;
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;

        // Legacy fields (no longer used during build due to PanelBase calling Construct before ctor body runs under IL2CPP)
        private readonly Citizen _citizen;
        private readonly Occupation _occupation;

        // Init argument passing that is safe during base construction
        private struct InitArgs
        {
            public int addressId;
            public int rosterIndex;
            public Citizen citizen;
            public Occupation occupation;
            public string title;
        }
        private static readonly Stack<InitArgs> PendingInits = new Stack<InitArgs>();
        public static void PushInit(int addressId, int rosterIndex, Citizen citizen, Occupation occupation, string title)
        {
            try { PendingInits.Push(new InitArgs { addressId = addressId, rosterIndex = rosterIndex, citizen = citizen, occupation = occupation, title = title }); }
            catch { }
        }

        private EmployeeDetailsView _view;

        public override string Name => _panelName;
        public override int MinWidth => 300;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => _anchorMin;
        public override Vector2 DefaultAnchorMax => _anchorMax;

        // Preferred constructor: data will be pulled from PendingInits during ConstructPanelContent
        public FloatingEmployeePanel(UIBase owner, string title, int index)
            : base(owner)
        {
            _panelName = title;
            // Cascade windows vertically on the right side similar to previous logic
            float topStart = 0.95f - (index * 0.38f);
            float bottom = topStart - 0.35f;
            if (bottom < 0.06f) { bottom = 0.06f; topStart = bottom + 0.35f; }
            _anchorMin = new Vector2(0.60f, bottom);
            _anchorMax = new Vector2(0.98f, topStart);
        }

        // Backwards-compat constructor; will still rely on PendingInits for actual build
        public FloatingEmployeePanel(UIBase owner, string title, int index, Citizen citizen, Occupation occ)
            : this(owner, title, index)
        {
            _citizen = citizen;
            _occupation = occ;
            try
            {
                string occLabel = _occupation != null ? (string.IsNullOrEmpty(_occupation.name) ? (_occupation.preset != null ? _occupation.preset.name : "<no preset>") : _occupation.name) : "<null>";
                string citLabel = _citizen != null ? _citizen.GetCitizenName() : "<null>";
                Plugin.Logger.LogInfo($"FloatingEmployeePanel.Ctor(compat): title={title}, citizen={citLabel}, occ={occLabel}");
            }
            catch { }
        }

        protected override void ConstructPanelContent()
        {
            try
            {
                // Opaque background for content area is handled by the panel; ensure our inner content fills below header
                GameObject content = UIFactory.CreateUIObject("EmpPanelContent", ContentRoot);
                RectTransform contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0.02f, 0.04f);
                contentRt.anchorMax = new Vector2(0.98f, 0.92f);
                contentRt.offsetMin = Vector2.zero;
                contentRt.offsetMax = Vector2.zero;
                var le = content.GetComponent<LayoutElement>();
                if (le == null) le = content.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                try { content.transform.SetAsLastSibling(); } catch { }

                // Add a dedicated Canvas above the panel's content background to guarantee visibility
                try
                {
                    var parentCanvas = ContentRoot.GetComponentInParent<Canvas>();
                    var contentCanvas = content.GetComponent<Canvas>();
                    if (contentCanvas == null) contentCanvas = content.AddComponent<Canvas>();
                    contentCanvas.overrideSorting = true;
                    int baseOrder = parentCanvas != null ? parentCanvas.sortingOrder : 5000;
                    if (baseOrder < 0) baseOrder = 20000;
                    int desired = baseOrder + 50;
                    if (desired > 32760) desired = 32760;
                    if (desired < -32760) desired = -32760;
                    contentCanvas.sortingOrder = desired;
                    if (content.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                        content.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    Plugin.Logger.LogInfo($"FloatingEmpPanel: content canvas order set to {contentCanvas.sortingOrder} (parent {baseOrder})");
                }
                catch { }

                // Pull init args set before construction
                Citizen citizenArg = null;
                Occupation occArg = null;
                int addrArg = -1;
                int rosterIdx = -1;
                string titleArg = _panelName;
                try
                {
                    if (PendingInits.Count > 0)
                    {
                        var init = PendingInits.Pop();
                        addrArg = init.addressId;
                        rosterIdx = init.rosterIndex;
                        citizenArg = init.citizen;
                        occArg = init.occupation;
                        if (!string.IsNullOrEmpty(init.title)) titleArg = init.title;
                    }
                }
                catch { }

                // If citizen/occupation still null, try resolve via BusinessUIManager by address/index
                if ((citizenArg == null || occArg == null) && addrArg >= 0 && rosterIdx >= 0)
                {
                    try
                    {
                        var apt = BusinessUIManager.Instance != null ? (BusinessUIManager)BusinessUIManager.Instance : null;
                    }
                    catch { }
                }

                _view = new EmployeeDetailsView();
                _view.Build(content);
                _view.Show(citizenArg ?? _citizen, occArg ?? _occupation);
                try { _view.BringToFront(); } catch { }

                // Optional: add a small Close button inside content for redundancy
                var closeBtn = UIFactory.CreateButton(ContentRoot, "EmpPanelClose", "Close");
                closeBtn.ButtonText.fontSize = 12;
                RectTransform closeRt = closeBtn.Component.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0.86f, 0.93f);
                closeRt.anchorMax = new Vector2(0.97f, 0.98f);
                closeRt.offsetMin = Vector2.zero;
                closeRt.offsetMax = Vector2.zero;
                closeBtn.OnClick += () => {
                    try { SetActive(false); } catch { }
                };
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error building FloatingEmployeePanel: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
