using System;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;

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
            // Make sure this subview renders on top of ScrollView (which may have its own Canvas)
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();
            // Also ensure it's the last sibling in hierarchy for safety
            root.transform.SetAsLastSibling();

            // Portrait (bigger)
            var portraitGO = UIFactory.CreateUIObject("Emp_Portrait", root);
            portrait = portraitGO.AddComponent<RawImage>();
            var pRt = portraitGO.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 0.5f);
            pRt.anchorMax = new Vector2(0f, 0.5f);
            pRt.pivot = new Vector2(0f, 0.5f);
            pRt.anchoredPosition = new Vector2(12f, 0f);
            pRt.sizeDelta = new Vector2(80f, 80f);

            // Name
            nameText = UIFactory.CreateLabel(root, "Emp_Name", "Name", TextAnchor.LowerLeft);
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.18f, 0.55f);
            nameRt.anchorMax = new Vector2(0.70f, 0.95f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            nameText.fontSize = 18;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;

            // Job title
            jobText = UIFactory.CreateLabel(root, "Emp_Job", "Job", TextAnchor.UpperLeft);
            var jobRt = jobText.GetComponent<RectTransform>();
            jobRt.anchorMin = new Vector2(0.18f, 0.30f);
            jobRt.anchorMax = new Vector2(0.70f, 0.55f);
            jobRt.offsetMin = Vector2.zero;
            jobRt.offsetMax = Vector2.zero;
            jobText.fontSize = 14;
            jobText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            // Salary
            salaryText = UIFactory.CreateLabel(root, "Emp_Salary", "Salary", TextAnchor.MiddleLeft);
            var salRt = salaryText.GetComponent<RectTransform>();
            salRt.anchorMin = new Vector2(0.18f, 0.05f);
            salRt.anchorMax = new Vector2(0.70f, 0.30f);
            salRt.offsetMin = Vector2.zero;
            salRt.offsetMax = Vector2.zero;
            salaryText.fontSize = 14;
            salaryText.color = Color.white;

            // Background box
            GameObject bg = UIFactory.CreateUIObject("Emp_BG", root);
            var bgImg = bg.GetComponent<UnityEngine.UI.Image>();
            if (bgImg == null) bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            // Make it clearly visible
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            // Ensure the background renders behind other children
            try { bg.transform.SetAsFirstSibling(); } catch { }

            // Buttons container
            GameObject btnRow = UIFactory.CreateHorizontalGroup(root, "Emp_Buttons", true, false, true, true, 8);
            var btnRt = btnRow.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.72f, 0.15f);
            btnRt.anchorMax = new Vector2(0.95f, 0.85f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            changeRoleButton = UIFactory.CreateButton(btnRow, "Emp_ChangeRole", "Change Role");
            changeRoleButton.ButtonText.fontSize = 14;
            changeRoleButton.ButtonText.color = Color.white;
            SetupButton(changeRoleButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            changeRoleButton.OnClick += () =>
            {
                Plugin.Logger.LogInfo("Change Role clicked (todo)");
            };

            fireButton = UIFactory.CreateButton(btnRow, "Emp_Fire", "Fire");
            fireButton.ButtonText.fontSize = 14;
            fireButton.ButtonText.color = Color.white;
            SetupButton(fireButton, new Color(0.8f, 0.2f, 0.2f, 1f));
            fireButton.OnClick += () =>
            {
                Plugin.Logger.LogInfo("Fire clicked (todo)");
            };

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

        public void Show(Citizen citizen, Occupation occ)
        {
            try
            {
                Plugin.Logger.LogInfo($"EmployeeDetailsView.Show citizen={(citizen != null ? citizen.GetCitizenName() : "<null>")}, occ={(occ != null ? (string.IsNullOrEmpty(occ.name) ? (occ.preset != null ? occ.preset.name : "<no preset>") : occ.name) : "<null>")}");
                if (root == null) return;
                root.SetActive(true);

                // Fallback: if citizen not provided, try to obtain it from the occupation
                if (citizen == null && occ != null)
                {
                    try { citizen = occ.employee as Citizen; } catch { }
                }

                // Portrait
                portrait.texture = null;
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

                // Name
                nameText.text = citizen != null ? citizen.GetCitizenName() : "Vacant";

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
                if (ownCanvas == null) ownCanvas = root.AddComponent<Canvas>();
                ownCanvas.overrideSorting = true;
                int parentOrder = parentCanvas != null ? parentCanvas.sortingOrder : 5000;
                if (parentOrder < 0) parentOrder = 20000;
                int desired = parentOrder + 20;
                if (desired > 32760) desired = 32760;
                if (desired < -32760) desired = -32760;
                ownCanvas.sortingOrder = desired;
                root.transform.SetAsLastSibling();
                Plugin.Logger.LogInfo($"EmpDetails BringToFront parentOrder={parentOrder} set ownOrder={ownCanvas.sortingOrder}");
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
