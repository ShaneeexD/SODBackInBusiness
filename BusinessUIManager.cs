using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using Il2CppInterop.Runtime;
using Il2CppSystem;
using UniverseLib.Config;
using System.Runtime.InteropServices;
using SOD.Common;

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
        
        // Static reference to track if UI has been created
        private static bool uiInitialized = false;
        
        public static bool uiEnabled = false;
        // Create the business UI
        public void CreateBusinessUI()
        {
            try
            {
                // Check if UI already exists
                if (uiInitialized)
                {
                    // Just toggle visibility
                    ToggleBusinessUI();
                    return;
                }
                
                Plugin.Logger.LogInfo("Creating business UI using UniverseLib...");
                
                // Simply try to register the UI - if it fails, we'll catch the exception
                try {
                    uiBase = UniversalUI.RegisterUI("com.backinbusiness.ui", null);
                } catch (System.Exception registerEx) {
                    // If it's already registered, this is fine - we'll just use the existing one
                    Plugin.Logger.LogInfo($"UI already registered: {registerEx.Message}");
                    
                    // We can't get a reference to an existing UI in UniverseLib, so we'll create a new ID
                    string uniqueId = $"com.backinbusiness.ui.{System.DateTime.Now.Ticks}";
                    uiBase = UniversalUI.RegisterUI(uniqueId, null);
                }
                
                // Create the panel
                businessPanel = new BusinessPanel(uiBase);
                
                // Show the panel
                businessPanel.SetActive(true);
                
                // Mark as initialized
                uiInitialized = true;
                
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
            try
            {
                if (businessPanel != null)
                {
                    bool newState = !businessPanel.Enabled;
                    businessPanel.SetActive(newState);
                    
                    // Log the state change
                    Plugin.Logger.LogInfo($"UI is now {(newState ? "visible" : "hidden")}");
                    
                    Plugin.Logger.LogInfo($"Toggled business UI visibility to {businessPanel.Enabled}");
                }
                else
                {
                    // If panel doesn't exist yet, create it
                    CreateBusinessUI();
                }

                if(!businessPanel.Enabled)
                {
                    try
                    {
                        uiEnabled = false;
                        
                        // Force any UniverseLib panel resizing to end
                        UniverseLib.UI.Panels.PanelManager.ForceEndResize();
                        
                        // Make sure the panel is fully closed
                        if (businessPanel != null)
                        {
                            businessPanel.SetActive(false);
                        }
                        
                        // Reset the event system
                        if (UnityEngine.EventSystems.EventSystem.current != null)
                        {
                            // First clear selection
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                            
                            // Then set focus back to game canvas if it exists
                            var gameCanvas = UnityEngine.GameObject.Find("GameCanvas");
                            if (gameCanvas != null)
                            {
                                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(gameCanvas);
                            }
                        }
                        
                        // Reset input through UniverseLib's InputManager instead
                        UniverseLib.Input.InputManager.ResetInputAxes();
                        // Re-enable game controls in the correct order
                        InputController.Instance.enabled = true;
                        Player.Instance.EnableCharacterController(true);
                        Player.Instance.EnablePlayerMouseLook(true, true);
                        
                        // Resume the game
                        SessionData sessionData = SessionData.Instance;
                        sessionData.ResumeGame();
                        
                        // Lock cursor for game control
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                        
                        // Log the input state
                        Plugin.Logger.LogInfo("Game input restored, UI closed");
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to update input control: {ex.Message}");
                    }
                }
                else
                {
                    uiEnabled = true;
                    
                    // Disable game controls
                    Player.Instance.EnablePlayerMouseLook(false, false);
                    Player.Instance.EnableCharacterController(false);
                    InputController.Instance.enabled = false;
                    
                    // Pause the game
                    SessionData sessionData = SessionData.Instance;
                    sessionData.PauseGame(false, false, true);
                    
                    // Show cursor for UI interaction
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    
                    // Log the input state
                    Plugin.Logger.LogInfo("Game input disabled, UniverseLib input blocking enabled");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error toggling business UI: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
    
    // Custom panel class for business UI
    public class BusinessPanel : PanelBase
    {
        private List<BusinessInfo> availableBusinesses;
        private List<ButtonRef> businessButtons = new List<ButtonRef>();
        private int selectedBusinessIndex = -1;
        private GameObject businessListContainer;
        private ButtonRef purchaseButton;
        private ButtonRef closeButton;
        private ButtonRef refreshButton;
        
        // Required abstract properties
        public override string Name => "Business Management";
        public override int MinWidth => 800;
        public override int MinHeight => 500;
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
            
            // Create instructions text
            Text instructionsText = UIFactory.CreateLabel(ContentRoot, "InstructionsText", "Click on a business to select it", TextAnchor.MiddleCenter);
            instructionsText.fontSize = 14;
            instructionsText.color = Color.yellow;
            RectTransform instructionsRect = instructionsText.GetComponent<RectTransform>();
            instructionsRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.85f);
            instructionsRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.9f);
            instructionsRect.offsetMin = UnityEngine.Vector2.zero;
            instructionsRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create a scroll view for the business list
            GameObject scrollObj = UIFactory.CreateScrollView(ContentRoot, "BusinessListScroll", out GameObject scrollContent, out _);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.25f);
            scrollRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = UnityEngine.Vector2.zero;
            scrollRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the business list container
            businessListContainer = UIFactory.CreateVerticalGroup(scrollContent, "BusinessListContainer", true, false, true, true, 5);
            businessListContainer.GetComponent<RectTransform>().sizeDelta = new UnityEngine.Vector2(0, 0);
            
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
            closeButton.OnClick += CloseBusinessUI;
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
        
        // Track the panel's active state to prevent unnecessary refreshes
        private bool wasActiveLastFrame = false;
        
        public override void SetActive(bool active)
        {
            // Call base method first
            base.SetActive(active);
            
            // Only refresh the business list when the panel is first activated
            // This prevents refreshing when clicking on businesses
            if (active && !wasActiveLastFrame)
            {
                Plugin.Logger.LogInfo("Panel activated for the first time, refreshing business list");
                RefreshBusinessList();
            }
            // Update the active state for next frame
            wasActiveLastFrame = active;
        }
        
        // Refresh the business list
        private void RefreshBusinessList()
        {
            try
            {
                // Add stack trace to see where this method is being called from
                System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
                Plugin.Logger.LogInfo($"RefreshBusinessList called from: {stackTrace.ToString()}");
                Plugin.Logger.LogInfo("Refreshing business list...");
                
                // Get available businesses
                availableBusinesses = BusinessManager.Instance.GetBusinessByAddressId();
                
                // Clear existing business buttons
                businessButtons.Clear();
                
                // Clear existing business entries
                if (businessListContainer != null && businessListContainer.transform != null)
                {
                    // Use a manual approach to clear children since GetEnumerator might not be available in IL2CPP
                    int childCount = businessListContainer.transform.childCount;
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        Transform child = businessListContainer.transform.GetChild(i);
                        if (child != null && child.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }
                
                // Update the business list
                if (availableBusinesses == null || availableBusinesses.Count == 0)
                {
                    // Create a "No businesses found" label
                    Text noBusinessesText = UIFactory.CreateLabel(businessListContainer, "NoBusinessesText", "No businesses found. Try refreshing the list.", TextAnchor.MiddleCenter);
                    noBusinessesText.fontSize = 16;
                    noBusinessesText.color = Color.yellow;
                    purchaseButton.Component.interactable = false;
                    
                    // Set layout for the text
                    LayoutElement textLayout = noBusinessesText.gameObject.AddComponent<LayoutElement>();
                    textLayout.minHeight = 40;
                    textLayout.preferredHeight = 40;
                    textLayout.flexibleHeight = 0;
                }
                else
                {
                    // Create buttons for each business
                    for (int i = 0; i < availableBusinesses.Count; i++)
                    {
                        var businessData = availableBusinesses[i];
                        var business = businessData.Address; // Get the actual NewAddress object
                        string name = business.name?.ToString() ?? "Unknown";
                        string id = business.id.ToString();
                        string buttonText = null;
                        // IMPORTANT: Store the index in a local variable to avoid closure issues
                        int capturedIndex = i;
                        
                        CompanyPresets companyPresets = new CompanyPresets();
                        // Initialize with default text
                        buttonText = $"{i + 1}. {name} (ID: {id})";
                        
                        // Search for matching preset name in the CompanyPresetsList
                        string presetName = business.company.preset.name;
                        for (int j = 0; j < companyPresets.CompanyPresetsList.Length; j++)
                        {
                            if (presetName == companyPresets.CompanyPresetsList[j].Item1)
                            {
                                // Found matching preset, calculate cost with employee count
                                int baseCost = companyPresets.CompanyPresetsList[j].Item2;
                                int employeeCount = businessData.EmployeeCount;
                                int employeeCost = employeeCount * 250; // Each employee adds $250
                                
                                // Apply floor multiplier for office and laboratory type businesses
                                int floorMultiplier = 0;
                                string floorName = businessData.FloorName;
                                string presetType = companyPresets.CompanyPresetsList[j].Item1;
                                string businessType = "Unknown";
                                int floorNumber = 0;
                                
                                // Get the friendly business type name from the mapping
                                for (int k = 0; k < companyPresets.CompanyPresetsMapping.Length; k++)
                                {
                                    if (presetType == companyPresets.CompanyPresetsMapping[k].Item1)
                                    {
                                        businessType = companyPresets.CompanyPresetsMapping[k].Item2;
                                        break;
                                    }
                                }
                                
                                // Check if this is an office or laboratory type that should have floor multiplier
                                if (presetType == "IndustrialOffice" || presetType == "MediumOffice" || presetType == "Laboratory")
                                {
                                    // Extract floor number from the floor name (in parentheses at the end)
                                    if (floorName.Contains("(Floor "))
                                    {
                                        int startIndex = floorName.LastIndexOf("(Floor ") + 7;
                                        int endIndex = floorName.LastIndexOf(")");
                                        if (endIndex > startIndex)
                                        {
                                            string floorNumberStr = floorName.Substring(startIndex, endIndex - startIndex);
                                            if (int.TryParse(floorNumberStr, out floorNumber))
                                            {
                                                // Apply multiplier based on floor number
                                                // Higher floors cost more (positive floors)
                                                // Lower floors (basement) cost less (negative floors)
                                                floorMultiplier = floorNumber * 500; // $500 per floor level
                                                
                                                // Ensure minimum floor cost is 0 (don't give discounts for basements)
                                                floorMultiplier = System.Math.Max(0, floorMultiplier);
                                                
                                                Plugin.Logger.LogInfo($"Applied floor multiplier of {floorMultiplier} for {presetType} on floor {floorNumber}");
                                            }
                                        }
                                    }
                                }
                                
                                int totalCost = baseCost + employeeCost + floorMultiplier;
                                
                                // Format the button text with all relevant information
                                buttonText = $"{i + 1}. {name} | Type: {businessType} | Employees: {employeeCount} | Cost: {totalCost} Crows";
                                break; // Exit loop once we find a match
                            }
                        }
                        // Create a button for this business
                        ButtonRef entryButton = UIFactory.CreateButton(businessListContainer, $"BusinessButton_{i}", buttonText);
                        
                        // Set up the click handler with a more direct approach
                        // Store the index in the button's name to avoid closure issues                        entryButton.GameObject.name = $"BusinessButton_{capturedIndex}";
                        
                        // Create a completely separate method to handle the click
                        // This prevents any potential issues with the UI system
                        int buttonIndex = capturedIndex; // Store in local variable
                        
                        // Use a simple action that just updates the selection state and button colors
                        entryButton.OnClick += () => {
                            // Log the click with detailed information
                            Plugin.Logger.LogInfo($"Business button clicked for index {buttonIndex}, button name: {entryButton.GameObject.name}");
                            
                            // IMPORTANT: Only update the selection state and button colors
                            // Do not call any methods that might refresh the list
                            if (buttonIndex >= 0 && buttonIndex < availableBusinesses.Count)
                            {
                                // Update selection state
                                selectedBusinessIndex = buttonIndex;
                                
                                // Enable purchase button
                                if (purchaseButton != null && purchaseButton.Component != null)
                                {
                                    purchaseButton.Component.interactable = true;
                                }
                                
                                // Update button colors directly
                                UpdateButtonColors();
                                
                                // Log the selection
                                var selectedBusinessData = availableBusinesses[buttonIndex];
                                var selectedBusiness = selectedBusinessData.Address;
                                string businessName = selectedBusiness.name?.ToString() ?? "Unknown";
                                Plugin.Logger.LogInfo($"Selected business: {businessName} (ID: {selectedBusiness.id}, Employees: {selectedBusinessData.EmployeeCount}, Index: {buttonIndex})");
                            }
                        };
                        
                        // Style the button
                        entryButton.ButtonText.alignment = TextAnchor.MiddleLeft;
                        entryButton.ButtonText.fontSize = 16;
                        entryButton.ButtonText.color = (i == selectedBusinessIndex) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
                        
                        // Set button colors based on selection state
                        Button button = entryButton.Component;
                        if (button != null)
                        {
                            ColorBlock colors = button.colors;
                            
                            if (i == selectedBusinessIndex)
                            {
                                // Selected button colors (blue)
                                colors.normalColor = new Color(0.3f, 0.5f, 0.7f, 1f);
                            }
                            else
                            {
                                // Normal button colors (dark gray)
                                colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                            }
                            
                            // Set highlight colors
                            colors.highlightedColor = new Color(colors.normalColor.r + 0.1f, colors.normalColor.g + 0.1f, colors.normalColor.b + 0.1f, 1f);
                            colors.pressedColor = new Color(colors.normalColor.r - 0.1f, colors.normalColor.g - 0.1f, colors.normalColor.b - 0.1f, 1f);
                            button.colors = colors;
                        }
                        
                        // Set button layout
                        LayoutElement buttonLayout = entryButton.GameObject.AddComponent<LayoutElement>();
                        buttonLayout.minHeight = 40;
                        buttonLayout.preferredHeight = 40;
                        buttonLayout.flexibleHeight = 0;
                        buttonLayout.flexibleWidth = 1;
                        
                        // Store the button reference
                        businessButtons.Add(entryButton);
                    }
                }
                
                // Update purchase button state
                purchaseButton.Component.interactable = (selectedBusinessIndex >= 0);
                
                Plugin.Logger.LogInfo($"Found {availableBusinesses.Count} businesses");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error refreshing business list: {ex.Message}\n{ex.StackTrace}");
                
                // Clear and show error message
                if (businessListContainer != null && businessListContainer.transform != null)
                {
                    // Use a manual approach to clear children since GetEnumerator might not be available in IL2CPP
                    int childCount = businessListContainer.transform.childCount;
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        Transform child = businessListContainer.transform.GetChild(i);
                        if (child != null && child.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }
                
                Text errorText = UIFactory.CreateLabel(businessListContainer, "ErrorText", "Error loading businesses. Check logs.", TextAnchor.MiddleCenter);
                errorText.fontSize = 16;
                errorText.color = Color.red;
            }
        }

        private void CloseBusinessUI()
        {
            SetActive(false);
            Player.Instance.EnableCharacterController(true);
            Player.Instance.EnablePlayerMouseLook(true, true);
            InputController.Instance.enabled = true;
            SessionData sessionData = SessionData.Instance;
            sessionData.ResumeGame();
            Cursor.lockState = CursorLockMode.Locked;

        }
        
        private void SelectBusiness(int index)
        {
            // Add detailed logging to track the selection process
            Plugin.Logger.LogInfo($"SelectBusiness called with index {index}");
            
            if (index >= 0 && index < availableBusinesses.Count)
            {
                // Log selection for debugging
                Plugin.Logger.LogInfo($"Selecting business at index {index}, current selection: {selectedBusinessIndex}");
                
                // Update selection state
                int previousIndex = selectedBusinessIndex;
                selectedBusinessIndex = index;
                
                // Enable purchase button
                if (purchaseButton != null && purchaseButton.Component != null)
                {
                    purchaseButton.Component.interactable = true;
                }
                
                // Update button colors directly without refreshing the list
                for (int i = 0; i < businessButtons.Count; i++)
                {
                    if (i < availableBusinesses.Count)
                    {
                        ButtonRef button = businessButtons[i];
                        if (button != null && button.Component != null)
                        {
                            // Update button text color
                            if (button.ButtonText != null)
                            {
                                button.ButtonText.color = (i == selectedBusinessIndex) 
                                    ? Color.white 
                                    : new Color(0.9f, 0.9f, 0.9f, 1f);
                            }
                            
                            // Update button background color
                            ColorBlock colors = button.Component.colors;
                            if (i == selectedBusinessIndex)
                            {
                                // Selected button (blue)
                                colors.normalColor = new Color(0.3f, 0.5f, 0.7f, 1f);
                            }
                            else
                            {
                                // Normal button (dark gray)
                                colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                            }
                            
                            // Update highlight colors
                            colors.highlightedColor = new Color(colors.normalColor.r + 0.1f, colors.normalColor.g + 0.1f, colors.normalColor.b + 0.1f, 1f);
                            colors.pressedColor = new Color(colors.normalColor.r - 0.1f, colors.normalColor.g - 0.1f, colors.normalColor.b - 0.1f, 1f);
                            button.Component.colors = colors;
                        }
                    }
                }
                
                // Log the selection for verification
                var selectedBusinessData = availableBusinesses[index];
                var selectedBusiness = selectedBusinessData.Address;
                string name = selectedBusiness.name?.ToString() ?? "Unknown";
                Plugin.Logger.LogInfo($"Selected business: {name} (ID: {selectedBusiness.id}, Employees: {selectedBusinessData.EmployeeCount}, Index: {index})");
            }
        }
        
        // Update button colors without refreshing the list
        private void UpdateButtonColors()
        {
            // Update button colors directly without refreshing the list
            for (int i = 0; i < businessButtons.Count; i++)
            {
                if (i < availableBusinesses.Count)
                {
                    ButtonRef button = businessButtons[i];
                    if (button != null && button.Component != null)
                    {
                        // Update button text color
                        if (button.ButtonText != null)
                        {
                            button.ButtonText.color = (i == selectedBusinessIndex) 
                                ? Color.white 
                                : new Color(0.9f, 0.9f, 0.9f, 1f);
                        }
                        
                        // Update button background color
                        ColorBlock colors = button.Component.colors;
                        if (i == selectedBusinessIndex)
                        {
                            // Selected button (blue)
                            colors.normalColor = new Color(0.3f, 0.5f, 0.7f, 1f);
                        }
                        else
                        {
                            // Normal button (dark gray)
                            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                        }
                        
                        // Update highlight colors
                        colors.highlightedColor = new Color(colors.normalColor.r + 0.1f, colors.normalColor.g + 0.1f, colors.normalColor.b + 0.1f, 1f);
                        colors.pressedColor = new Color(colors.normalColor.r - 0.1f, colors.normalColor.g - 0.1f, colors.normalColor.b - 0.1f, 1f);
                        button.Component.colors = colors;
                    }
                }
            }
        }
        
        // Custom method to display the business purchase notification
        private void DisplayBusinessPurchase()
        {
            try
            {
                // Play the purchase sound
                AudioController.Instance.Play2DSound(AudioControls.Instance.newApartment, null, 1f);
                
                // Set the text directly
                InterfaceControls.Instance.caseSolvedText.text = "New Business";
                
                // Setup for animation
                CanvasRenderer rend = InterfaceControls.Instance.caseSolvedText.canvasRenderer;
                rend.SetAlpha(0); // Start fully transparent
                
                // Make the text visible
                InterfaceControls.Instance.caseSolvedText.gameObject.SetActive(true);
                
                // Enable other UI elements that are part of the notification
                foreach (CanvasRenderer renderer in InterfaceControls.Instance.screenMessageFadeRenderers)
                {
                    renderer.SetAlpha(0); // Start fully transparent
                }
                
                Plugin.Logger.LogInfo("Displayed business purchase notification");
                
                // Use UniverseLib's Runtime Coroutine to animate and hide the text
                UniverseLib.RuntimeHelper.StartCoroutine(AnimateBusinessPurchaseText());
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error displaying business purchase notification: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        // Coroutine to animate and hide the business purchase text
        private System.Collections.IEnumerator AnimateBusinessPurchaseText()
        {
            float duration = 5.5f; // Total animation duration
            float timer = duration;
            CanvasRenderer rend = InterfaceControls.Instance.caseSolvedText.canvasRenderer;
            
            // Animation loop
            while (timer > 0f)
            {
                timer -= UnityEngine.Time.deltaTime;
                float progress = (duration - timer) / duration; // 0 to 1
                
                // Fade in during first 40% of animation
                if (progress <= 0.4f)
                {
                    foreach (CanvasRenderer renderer in InterfaceControls.Instance.screenMessageFadeRenderers)
                    {
                        renderer.SetAlpha(progress / 0.4f); // 0 to 1
                    }
                }
                // Fade out during last 20% of animation
                else if (progress >= 0.8f)
                {
                    foreach (CanvasRenderer renderer in InterfaceControls.Instance.screenMessageFadeRenderers)
                    {
                        renderer.SetAlpha(1f - (progress - 0.8f) / 0.2f); // 1 to 0
                    }
                }
                
                // Apply text animations from the game's animation curves
                rend.SetAlpha(InterfaceControls.Instance.caseSolvedAlphaAnim.Evaluate(1f - progress));
                InterfaceControls.Instance.caseSolvedText.characterSpacing = 
                    InterfaceControls.Instance.caseSolvedKerningAnim.Evaluate(progress);
                
                yield return null; // Wait for next frame
            }
            
            // Hide the text when animation is complete
            InterfaceControls.Instance.caseSolvedText.gameObject.SetActive(false);
            
            // Reset all renderers to invisible
            foreach (CanvasRenderer renderer in InterfaceControls.Instance.screenMessageFadeRenderers)
            {
                renderer.SetAlpha(0);
            }
            
            Plugin.Logger.LogInfo("Hidden business purchase notification");
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
                var selectedBusinessData = availableBusinesses[selectedBusinessIndex];
                NewAddress selectedBusiness = selectedBusinessData.Address;
                
                Plugin.Logger.LogInfo($"Attempting to purchase business: {selectedBusiness.name}");

                // Get the cost from CompanyPresets array
                CompanyPresets companyPresets = new CompanyPresets();
                int baseCost = 0;
                string presetName = selectedBusiness.company.preset.name;
                string businessType = "Unknown";
                
                // Find the matching preset and get its base cost
                for (int i = 0; i < companyPresets.CompanyPresetsList.Length; i++)
                {
                    if (presetName == companyPresets.CompanyPresetsList[i].Item1)
                    {
                        baseCost = companyPresets.CompanyPresetsList[i].Item2;
                        break;
                    }
                }
                
                // Get the friendly business type name from the mapping
                for (int i = 0; i < companyPresets.CompanyPresetsMapping.Length; i++)
                {
                    if (presetName == companyPresets.CompanyPresetsMapping[i].Item1)
                    {
                        businessType = companyPresets.CompanyPresetsMapping[i].Item2;
                        break;
                    }
                }
                
                Plugin.Logger.LogInfo($"Business type: {businessType} (preset: {presetName})");
                
                // Calculate final cost based on employee count and floor number
                int employeeCount = selectedBusinessData.EmployeeCount;
                int employeeCost = employeeCount * 250; // Each employee adds $250 to the cost
                
                // Apply floor multiplier for office and laboratory type businesses
                int floorMultiplier = 0;
                string floorName = selectedBusinessData.FloorName;
                
                // Check if this is an office or laboratory type that should have floor multiplier
                if (presetName == "IndustrialOffice" || presetName == "MediumOffice" || presetName == "Laboratory")
                {
                    // Extract floor number from the floor name (in parentheses at the end)
                    if (floorName.Contains("(Floor "))
                    {
                        int startIndex = floorName.LastIndexOf("(Floor ") + 7;
                        int endIndex = floorName.LastIndexOf(")");
                        if (endIndex > startIndex)
                        {
                            string floorNumberStr = floorName.Substring(startIndex, endIndex - startIndex);
                            if (int.TryParse(floorNumberStr, out int floorNumber))
                            {
                                // Apply multiplier based on floor number
                                // Higher floors cost more (positive floors)
                                // Lower floors (basement) cost less (negative floors)
                                floorMultiplier = floorNumber * 1000; // $1000 per floor level
                                
                                // Ensure minimum floor cost is 0 (don't give discounts for basements)
                                floorMultiplier = System.Math.Max(0, floorMultiplier);
                                
                                Plugin.Logger.LogInfo($"Applied floor multiplier of {floorMultiplier} for {presetName} on floor {floorNumber}");
                            }
                        }
                    }
                }
                
                int finalCost = baseCost + employeeCost + floorMultiplier;
                
                Plugin.Logger.LogInfo($"Business cost calculation: Base cost {baseCost} + Employee cost ({employeeCost} for {employeeCount} employees) + Floor multiplier ({floorMultiplier}) = {finalCost} Crows");
                
                // Deduct the cost from player's money
                if(GameplayController.Instance.money >= finalCost)
                {
                    GameplayController.Instance.AddMoney(-finalCost, true, $"Purchased business: {selectedBusiness.name}");
                    availableBusinesses.RemoveAt(selectedBusinessIndex);
                    businessButtons.RemoveAt(selectedBusinessIndex);
                    Player.Instance.AddLocationOfAuthorty(selectedBusiness);
                    Player.Instance.apartmentsOwned.Add(selectedBusiness);
                    Player.Instance.AddToKeyring(selectedBusiness, false);

                    // Display business purchase notification
                    DisplayBusinessPurchase();

                    Plugin.Logger.LogInfo($"Deducted {finalCost} Crows for purchasing {selectedBusiness.name}");
                    //Lib.GameMessage.ShowPlayerSpeech($"Successfully purchased {selectedBusiness.name} for {finalCost} Crows.", 3, true);
                    Lib.GameMessage.Broadcast($"New business purchased: {selectedBusiness.name}", InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.green);
                }
                else
                {
                    Lib.GameMessage.Broadcast($"You don't have enough money to purchase {selectedBusiness.name}. Cost: {finalCost} Crows", InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.white);
                }

                // Log the action
                Plugin.Logger.LogInfo($"Purchase initiated for business: {selectedBusiness.name} (ID: {selectedBusiness.id})");
                
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