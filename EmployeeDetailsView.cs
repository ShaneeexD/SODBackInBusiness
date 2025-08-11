using System;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using SOD.Common;
using Il2CppInterop.Runtime;

namespace BackInBusiness
{
    internal class EmployeeDetailsView
    {
        private GameObject root;
        private RawImage portrait;
        private Text nameText;
        private Text jobText;
        private Text salaryText;
        private ButtonRef fireButton;
        private ButtonRef changeRoleButton;

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

            // Portrait (bigger)
            var portraitGO = UIFactory.CreateUIObject("Emp_Portrait", root);
            portrait = portraitGO.AddComponent<RawImage>();
            var pRt = portraitGO.GetComponent<RectTransform>();
            // Top-left anchored
            pRt.anchorMin = new Vector2(0f, 1f);
            pRt.anchorMax = new Vector2(0f, 1f);
            pRt.pivot = new Vector2(0f, 1f);
            pRt.anchoredPosition = new Vector2(12f, -20f);
            pRt.sizeDelta = new Vector2(128f, 128f);

            // Name
            nameText = UIFactory.CreateLabel(root, "Emp_Name", "Name", TextAnchor.UpperLeft);
            var nameRt = nameText.GetComponent<RectTransform>();
            // Top row to the right of portrait (tighter spacing)
            nameRt.anchorMin = new Vector2(0.20f, 0.86f);
            nameRt.anchorMax = new Vector2(0.98f, 0.96f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;

            // Job title
            jobText = UIFactory.CreateLabel(root, "Emp_Job", "Job", TextAnchor.UpperLeft);
            var jobRt = jobText.GetComponent<RectTransform>();
            // Directly under name
            jobRt.anchorMin = new Vector2(0.20f, 0.76f);
            jobRt.anchorMax = new Vector2(0.98f, 0.86f);
            jobRt.offsetMin = Vector2.zero;
            jobRt.offsetMax = Vector2.zero;
            jobText.fontSize = 14;
            jobText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            // Salary
            salaryText = UIFactory.CreateLabel(root, "Emp_Salary", "Salary", TextAnchor.UpperLeft);
            var salRt = salaryText.GetComponent<RectTransform>();
            // Below occupation
            salRt.anchorMin = new Vector2(0.20f, 0.68f);
            salRt.anchorMax = new Vector2(0.98f, 0.76f);
            salRt.offsetMin = Vector2.zero;
            salRt.offsetMax = Vector2.zero;
            salaryText.fontSize = 14;
            salaryText.color = Color.white;

            // Background box
            GameObject bg = UIFactory.CreateUIObject("Emp_BG", root);
            var bgImg = bg.GetComponent<UnityEngine.UI.Image>();
            if (bgImg == null) bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            // Make it clearly visible
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            // Do not block clicks and make sure it renders behind all other children
            bgImg.raycastTarget = false;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            try { bg.transform.SetAsFirstSibling(); Plugin.Logger.LogInfo("Emp_BG moved to first sibling (behind content)"); } catch { }
            // Ensure the background renders behind other children
            try { bg.transform.SetAsFirstSibling(); } catch { }

            // Buttons container - bottom bar
            GameObject btnRow = UIFactory.CreateHorizontalGroup(root, "Emp_Buttons", true, false, true, true, 12);
            var btnRt = btnRow.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.20f, 0.06f);
            btnRt.anchorMax = new Vector2(0.98f, 0.16f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            try { btnRow.transform.SetAsLastSibling(); } catch { }

            changeRoleButton = UIFactory.CreateButton(btnRow, "Emp_ChangeRole", "Change Role");
            changeRoleButton.ButtonText.fontSize = 14;
            changeRoleButton.ButtonText.color = Color.white;
            SetupButton(changeRoleButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            try { var leCR = changeRoleButton.Component.gameObject.GetComponent<LayoutElement>() ?? changeRoleButton.Component.gameObject.AddComponent<LayoutElement>(); leCR.preferredWidth = 180f; leCR.minHeight = 36f; } catch { }
            changeRoleButton.OnClick += () =>
            {
                Plugin.Logger.LogInfo("Change Role clicked (todo)");
            };

            fireButton = UIFactory.CreateButton(btnRow, "Emp_Fire", "Fire");
            fireButton.ButtonText.fontSize = 14;
            fireButton.ButtonText.color = Color.white;
            SetupButton(fireButton, new Color(0.8f, 0.2f, 0.2f, 1f));
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
