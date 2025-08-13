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
            public string employeeName;
            public string roleName;
        }
        private static readonly Stack<InitArgs> PendingInits = new Stack<InitArgs>();
        public static void PushInit(int addressId, int rosterIndex, Citizen citizen, Occupation occupation, string title, string employeeName, string roleName)
        {
            try { PendingInits.Push(new InitArgs { addressId = addressId, rosterIndex = rosterIndex, citizen = citizen, occupation = occupation, title = title, employeeName = employeeName, roleName = roleName }); }
            catch { }
        }

        private EmployeeDetailsView _view;

        public override string Name => _panelName;
        public override int MinWidth => 55;
        public override int MinHeight => 300;
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
            // Default: start thinner by reducing the horizontal span (right-aligned)
            // e.g., 20% width anchored to the right edge
            float right = 0.98f;
            float widthFrac = 0.35f; // tweak as desired for a thinner default
            _anchorMax = new Vector2(right, topStart);
            _anchorMin = new Vector2(Mathf.Clamp01(right - widthFrac), bottom);
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
                // Make the panel background completely transparent
                try
                {
                    // Find and hide the panel's background elements
                    var panelBg = ContentRoot.transform.parent.Find("Background");
                    if (panelBg != null)
                    {
                        panelBg.gameObject.SetActive(false);
                    }
                    
                    // Hide the title bar background
                    var titleBar = TitleBar;
                    if (titleBar != null)
                    {
                        var titleBg = titleBar.transform.Find("Background");
                        if (titleBg != null)
                        {
                            titleBg.gameObject.SetActive(false);
                        }
                    }
                    
                    // Hide the minimize button if it exists
                    try
                    {
                        var headerTransform = TitleBar;
                        if (headerTransform != null)
                        {
                            var minimizeBtn = headerTransform.transform.Find("MinimizeButton");
                            if (minimizeBtn != null)
                            {
                                minimizeBtn.gameObject.SetActive(false);
                            }
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error hiding panel elements: {ex.Message}");
                }
                
                // Create content container that fills the entire panel
                GameObject content = UIFactory.CreateUIObject("EmpPanelContent", ContentRoot);
                RectTransform contentRt = content.GetComponent<RectTransform>();
                
                // Make content fill the entire panel, not just below header
                contentRt.anchorMin = new Vector2(0, 0);
                contentRt.anchorMax = new Vector2(1, 1);
                contentRt.offsetMin = Vector2.zero;
                contentRt.offsetMax = Vector2.zero;
                var le = content.GetComponent<LayoutElement>();
                if (le == null) le = content.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                try { content.transform.SetAsLastSibling(); } catch { }

                // Pull init args set before construction
                Citizen citizenArg = null;
                Occupation occArg = null;
                int addrArg = -1;
                int rosterIdx = -1;
                string titleArg = _panelName;
                string empNameArg = null;
                string roleNameArg = null;
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
                        if (!string.IsNullOrEmpty(init.employeeName)) empNameArg = init.employeeName;
                        if (!string.IsNullOrEmpty(init.roleName)) roleNameArg = init.roleName;
                    }
                }
                catch { }

                // If citizen/occupation still null, try resolve via address/index directly to avoid stale IL2CPP refs
                if ((citizenArg == null || occArg == null) && addrArg >= 0 && rosterIdx >= 0)
                {
                    try
                    {
                        // Re-resolve from Player-owned apartments by address and roster index
                        var player = Player.Instance;
                        if (player != null && player.apartmentsOwned != null)
                        {
                            for (int i = 0; i < player.apartmentsOwned.Count; i++)
                            {
                                var apt = player.apartmentsOwned[i];
                                if (apt == null) continue;
                                if (apt.id != addrArg) continue;
                                if (apt.company != null && apt.company.companyRoster != null)
                                {
                                    if (rosterIdx >= 0 && rosterIdx < apt.company.companyRoster.Count)
                                    {
                                        var occNow = apt.company.companyRoster[rosterIdx];
                                        if (occNow != null)
                                        {
                                            occArg = occNow;
                                            try { citizenArg = occNow.employee as Citizen; } catch { }
                                        }
                                    }

                                    // Fallback: if still missing, try matching by employee name or role
                                    if ((occArg == null || citizenArg == null) && apt.company.companyRoster.Count > 0)
                                    {
                                        try
                                        {
                                            for (int r = 0; r < apt.company.companyRoster.Count; r++)
                                            {
                                                var o = apt.company.companyRoster[r];
                                                if (o == null) continue;
                                                string oRole = null;
                                                try { if (!string.IsNullOrEmpty(o.name)) oRole = o.name; else if (o.preset != null) oRole = o.preset.name; } catch { }
                                                Citizen cTry = null;
                                                try { cTry = o.employee as Citizen; } catch { }
                                                string cName = null;
                                                try { if (cTry != null) cName = cTry.GetCitizenName(); else if (o.employee != null && o.employee.name != null) cName = o.employee.name.ToString(); } catch { }
                                                bool nameMatch = !string.IsNullOrEmpty(empNameArg) && !string.IsNullOrEmpty(cName) && cName == empNameArg;
                                                bool roleMatch = !string.IsNullOrEmpty(roleNameArg) && !string.IsNullOrEmpty(oRole) && oRole == roleNameArg;
                                                if (nameMatch || roleMatch)
                                                {
                                                    occArg = o;
                                                    citizenArg = cTry;
                                                    break;
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                break; // Found address, stop searching
                            }
                        }
                    }
                    catch { }
                }

                try
                {
                    string occLabel = occArg != null ? (string.IsNullOrEmpty(occArg.name) ? (occArg.preset != null ? occArg.preset.name : "<no preset>") : occArg.name) : "<null>";
                    string citLabel = citizenArg != null ? citizenArg.GetCitizenName() : "<null>";
                    Plugin.Logger.LogInfo($"FloatingEmployeePanel.Resolve: addr={addrArg}, idx={rosterIdx}, empName={empNameArg ?? "<null>"}, role={roleNameArg ?? "<null>"}, citizen={citLabel}, occ={occLabel}");
                }
                catch { }

                _view = new EmployeeDetailsView();
                _view.Build(content);
                // Wire inner view close to only close this floating panel
                try { _view.CloseRequested += () => { try { SetActive(false); } catch { } }; } catch { }
                _view.Show(citizenArg ?? _citizen, occArg ?? _occupation, addrArg, rosterIdx);
                try { _view.BringToFront(); } catch { }

                // We don't need the redundant close button since we're using the game-styled one in EmployeeDetailsView
                // and we've hidden the UniverseLib panel elements
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error building FloatingEmployeePanel: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
