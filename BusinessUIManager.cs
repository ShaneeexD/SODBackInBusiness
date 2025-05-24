using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using Il2CppInterop.Runtime;
using Il2CppSystem;

namespace BackInBusiness
{
    public class BusinessUIManager
    {
        // Singleton instance
        private static BusinessUIManager _instance;
        public static BusinessUIManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new BusinessUIManager();
                return _instance;
            }
        }
        
        // UI components
        private UIBase uiBase;
        private BusinessPanel businessPanel;
        
        // Create the business UI
        public void CreateBusinessUI()
        {
            try
            {
                // Check if UI already exists
                if (businessPanel != null)
                {
                    // Just toggle visibility
                    ToggleBusinessUI();
                    return;
                }
                
                Plugin.Logger.LogInfo("Creating business UI using UniverseLib...");
                
                // Register the UI with UniverseLib
                uiBase = UniversalUI.RegisterUI("com.backinbusiness.ui", null);
                
                // Create the panel
                businessPanel = new BusinessPanel(uiBase);
                
                // Show the panel
                businessPanel.SetActive(true);
                
                Plugin.Logger.LogInfo("Business UI created successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error creating business UI: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        // Toggle UI visibility
        public void ToggleBusinessUI()
        {
            if (businessPanel != null)
            {
                businessPanel.SetActive(!businessPanel.Enabled);
                Plugin.Logger.LogInfo($"Toggled business UI visibility to {businessPanel.Enabled}");
            }
            else
            {
                // If panel doesn't exist yet, create it
                CreateBusinessUI();
            }
        }
    }
    
    // Custom panel class for business UI
    public class BusinessPanel : PanelBase
    {
        private Text businessListText;
        private List<NewAddress> availableBusinesses = new List<NewAddress>();
        private ButtonRef refreshButton;
        private ButtonRef purchaseButton;
        private ButtonRef closeButton;
        private int selectedBusinessIndex = -1;
        
        // Required abstract properties
        public override string Name => "Business Management";
        public override int MinWidth => 600;
        public override int MinHeight => 450;
        public override UnityEngine.Vector2 DefaultAnchorMin => new UnityEngine.Vector2(0.25f, 0.25f);
        public override UnityEngine.Vector2 DefaultAnchorMax => new UnityEngine.Vector2(0.75f, 0.75f);
        
        public BusinessPanel(UIBase owner) : base(owner) { }
        
        protected override void ConstructPanelContent()
        {
            // Create title
            Text titleText = UIFactory.CreateLabel(ContentRoot, "TitleText", "Available Businesses", TextAnchor.MiddleCenter);
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
            titleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
            titleRect.offsetMin = UnityEngine.Vector2.zero;
            titleRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create a scroll view for the business list
            GameObject scrollObj = UIFactory.CreateScrollView(ContentRoot, "BusinessListScroll", out GameObject scrollContent, out _);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.25f);
            scrollRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = UnityEngine.Vector2.zero;
            scrollRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the business list text
            businessListText = UIFactory.CreateLabel(scrollContent, "BusinessListText", "Loading businesses...", TextAnchor.UpperLeft);
            businessListText.fontSize = 14;
            businessListText.color = Color.white;
            
            // Create button container
            GameObject buttonContainer = UIFactory.CreateHorizontalGroup(ContentRoot, "ButtonContainer", true, false, true, true, 10);
            
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
            buttonContainerRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.2f);
            buttonContainerRect.offsetMin = UnityEngine.Vector2.zero;
            buttonContainerRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the refresh button
            refreshButton = UIFactory.CreateButton(buttonContainer, "RefreshButton", "Refresh List");
            refreshButton.OnClick += RefreshBusinessList;
            refreshButton.ButtonText.fontSize = 14;
            SetupButton(refreshButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            
            // Create the purchase button
            purchaseButton = UIFactory.CreateButton(buttonContainer, "PurchaseButton", "Purchase Selected");
            purchaseButton.OnClick += PurchaseSelectedBusiness;
            purchaseButton.ButtonText.fontSize = 14;
            SetupButton(purchaseButton, new Color(0.2f, 0.8f, 0.2f, 1f));
            purchaseButton.Component.interactable = false; // Disabled by default
            
            // Create the close button
            closeButton = UIFactory.CreateButton(buttonContainer, "CloseButton", "Close");
            closeButton.OnClick += () => SetActive(false);
            closeButton.ButtonText.fontSize = 14;
            SetupButton(closeButton, new Color(0.8f, 0.2f, 0.2f, 1f));
            
            // Create business selection buttons (will be populated when refreshing)
            
            // Refresh the business list initially
            RefreshBusinessList();
        }
        
        private void SetupButton(ButtonRef buttonRef, Color normalColor)
        {
            Button button = buttonRef.Component;
            
            // Set button colors
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = new Color(normalColor.r + 0.1f, normalColor.g + 0.1f, normalColor.b + 0.1f, 1f);
            colors.pressedColor = new Color(normalColor.r - 0.1f, normalColor.g - 0.1f, normalColor.b - 0.1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            
            // Set minimum height
            LayoutElement layoutElement = buttonRef.GameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = buttonRef.GameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 35;
        }
        
        public override void SetActive(bool active)
        {
            base.SetActive(active);
            
            // Refresh the business list when the panel is activated
            if (active)
            {
                RefreshBusinessList();
            }
        }
        
        // Refresh the business list
        private void RefreshBusinessList()
        {
            try
            {
                Plugin.Logger.LogInfo("Refreshing business list...");
                
                // Get available businesses
                availableBusinesses = BusinessManager.Instance.GetBusinessByAddressId();
                
                // Update the business list text
                if (availableBusinesses == null || availableBusinesses.Count == 0)
                {
                    businessListText.text = "No businesses found. Try refreshing the list.";
                    purchaseButton.Component.interactable = false;
                    return;
                }
                
                // Build the business list text with clickable options
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("Available Businesses (Click to select):\n");
                
                for (int i = 0; i < availableBusinesses.Count; i++)
                {
                    var business = availableBusinesses[i];
                    string name = business.name?.ToString() ?? "Unknown";
                    string id = business.id.ToString();
                    string prefix = (i == selectedBusinessIndex) ? "► " : "  ";
                    
                    sb.AppendLine($"{prefix}{i + 1}. {name} (ID: {id})");
                }
                
                sb.AppendLine($"\nTotal Businesses Found: {availableBusinesses.Count}");
                sb.AppendLine("\nInstructions:");
                sb.AppendLine("• Use the number buttons below to select a business");
                sb.AppendLine("• Click 'Purchase Selected' to buy the selected business");
                sb.AppendLine("• Click 'Refresh List' to update the list");
                
                businessListText.text = sb.ToString();
                
                // Create selection buttons
                CreateBusinessSelectionButtons();
                
                Plugin.Logger.LogInfo($"Found {availableBusinesses.Count} businesses");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error refreshing business list: {ex.Message}\n{ex.StackTrace}");
                businessListText.text = "Error loading businesses. Check logs.";
            }
        }
        
        private void CreateBusinessSelectionButtons()
        {
            // Remove existing selection buttons
            Transform buttonContainer = ContentRoot.transform.Find("ButtonContainer");
            if (buttonContainer == null) return;
            
            // Find or create selection button container
            GameObject selectionContainer = UIFactory.CreateHorizontalGroup(buttonContainer.gameObject, "SelectionButtons",
                true, false, true, true, 5);
            
            // Create number buttons for each business (limit to first 10 for UI space)
            int buttonCount = Il2CppSystem.Math.Min(availableBusinesses.Count, 10);
            for (int i = 0; i < buttonCount; i++)
            {
                int businessIndex = i; // Capture for closure
                
                ButtonRef selectButton = UIFactory.CreateButton(selectionContainer, $"SelectButton{i}", (i + 1).ToString());
                selectButton.OnClick += () => SelectBusiness(businessIndex);
                selectButton.ButtonText.fontSize = 12;
                
                // Set button size
                LayoutElement layoutElement = selectButton.GameObject.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = selectButton.GameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 40;
                layoutElement.preferredHeight = 30;
                
                // Style the button
                SetupButton(selectButton, new Color(0.7f, 0.7f, 0.7f, 1f));
            }
            
            if (availableBusinesses.Count > 10)
            {
                Text moreText = UIFactory.CreateLabel(selectionContainer, "MoreText", "...", TextAnchor.MiddleCenter);
                moreText.fontSize = 12;
                moreText.color = Color.gray;
            }
        }
        
        private void SelectBusiness(int index)
        {
            if (index >= 0 && index < availableBusinesses.Count)
            {
                selectedBusinessIndex = index;
                purchaseButton.Component.interactable = true;
                
                // Update the business list display
                RefreshBusinessListDisplay();
                
                var selectedBusiness = availableBusinesses[index];
                string name = selectedBusiness.name?.ToString() ?? "Unknown";
                Plugin.Logger.LogInfo($"Selected business: {name} (Index: {index})");
            }
        }
        
        private void RefreshBusinessListDisplay()
        {
            if (availableBusinesses == null || availableBusinesses.Count == 0) return;
            
            // Rebuild the display text with updated selection
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Available Businesses (Click to select):\n");
            
            for (int i = 0; i < availableBusinesses.Count; i++)
            {
                var business = availableBusinesses[i];
                string name = business.name?.ToString() ?? "Unknown";
                string id = business.id.ToString();
                string prefix = (i == selectedBusinessIndex) ? "► " : "  ";
                
                sb.AppendLine($"{prefix}{i + 1}. {name} (ID: {id})");
            }
            
            sb.AppendLine($"\nTotal Businesses Found: {availableBusinesses.Count}");
            if (selectedBusinessIndex >= 0)
            {
                var selected = availableBusinesses[selectedBusinessIndex];
                sb.AppendLine($"\nSelected: {selected.name?.ToString() ?? "Unknown"}");
            }
            sb.AppendLine("\nInstructions:");
            sb.AppendLine("• Use the number buttons below to select a business");
            sb.AppendLine("• Click 'Purchase Selected' to buy the selected business");
            sb.AppendLine("• Click 'Refresh List' to update the list");
            
            businessListText.text = sb.ToString();
        }
        
        private void PurchaseSelectedBusiness()
        {
            if (selectedBusinessIndex < 0 || selectedBusinessIndex >= availableBusinesses.Count)
            {
                Plugin.Logger.LogWarning("No business selected for purchase");
                return;
            }
            
            try
            {
                var selectedBusiness = availableBusinesses[selectedBusinessIndex];
                string businessName = selectedBusiness.name?.ToString() ?? "Unknown";
                
                Plugin.Logger.LogInfo($"Attempting to purchase business: {businessName}");
                
                // Call your business purchase logic here
                // BusinessManager.Instance.PurchaseBusiness(selectedBusiness);
                
                // For now, just log the action
                Plugin.Logger.LogInfo($"Purchase initiated for business: {businessName} (ID: {selectedBusiness.id})");
                
                // Reset selection and refresh list
                selectedBusinessIndex = -1;
                purchaseButton.Component.interactable = false;
                RefreshBusinessList();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error purchasing business: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}