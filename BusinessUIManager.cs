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


                    Player.Instance.EnablePlayerMouseLook(true, false);
                    Player.Instance.EnableCharacterController(true);
                    SessionData sessionData = SessionData.Instance;
                    InputController.Instance.enabled = true;
                    sessionData.ResumeGame();
                    // Put the mouse back in the game by updating UniverseLib config
                    // Access the config via reflection since we don't have direct access to it
                    try
                    {
                        var configType = typeof(Universe).Assembly.GetType("UniverseLib.Config.UniverseLibConfig");
                        if (configType != null)
                        {
                            var configField = typeof(Universe).GetField("Config", BindingFlags.Public | BindingFlags.Static);
                            if (configField != null)
                            {
                                var config = configField.GetValue(null);
                                if (config != null)
                                {
                                    var forceUnlockField = configType.GetField("Force_Unlock_Mouse");
                                    if (forceUnlockField != null)
                                    {
                                        forceUnlockField.SetValue(config, false);
                                        Plugin.Logger.LogInfo("Mouse control returned to game");
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to update mouse control: {ex.Message}");
                    }
                }
                else
                {
                    Player.Instance.EnablePlayerMouseLook(false, false);
                    Player.Instance.EnableCharacterController(false);
                    SessionData sessionData = SessionData.Instance;
                    sessionData.PauseGame(true, false, false);
                    InputController.Instance.enabled = false;
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
        private GameObject businessListContainer;
        private List<NewAddress> availableBusinesses = new List<NewAddress>();
        private ButtonRef refreshButton;
        private ButtonRef purchaseButton;
        private ButtonRef closeButton;
        private int selectedBusinessIndex = -1;
        private List<ButtonRef> businessButtons = new List<ButtonRef>();
        
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
                    return;
                }
                
                // Create clickable entries for each business
                for (int i = 0; i < availableBusinesses.Count; i++)
                {
                    int index = i; // Capture for closure
                    var business = availableBusinesses[i];
                    string name = business.name?.ToString() ?? "Unknown";
                    string id = business.id.ToString();
                    
                    // Create a container for this business entry
                    GameObject entryContainer = UIFactory.CreateHorizontalGroup(businessListContainer, $"BusinessEntry_{i}", true, false, true, true, 5);
                    
                    // Update the existing background image color (UIFactory.CreateHorizontalGroup already adds an Image)
                    Image bgImage = entryContainer.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.color = (i == selectedBusinessIndex) ? new Color(0.3f, 0.5f, 0.7f, 0.5f) : new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    }
                    
                    // Create a button for the entire entry
                    ButtonRef entryButton = UIFactory.CreateButton(entryContainer, $"BusinessButton_{i}", "");
                    if (entryButton != null)
                    {
                        entryButton.OnClick += () => SelectBusiness(index);
                        
                        // Make button transparent if possible
                        if (entryButton.Component != null)
                        {
                            Image buttonImage = entryButton.Component.GetComponent<Image>();
                            if (buttonImage != null)
                            {
                                buttonImage.color = new Color(0, 0, 0, 0);
                            }
                        }
                        
                        // Store the button reference
                        businessButtons.Add(entryButton);
                    }
                    
                    // Create text for the business details
                    Text entryText = UIFactory.CreateLabel(entryContainer, $"BusinessText_{i}", $"{i + 1}. {name} (ID: {id})", TextAnchor.MiddleLeft);
                    entryText.fontSize = 16;
                    entryText.color = (i == selectedBusinessIndex) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
                    
                    // Set layout element for proper sizing
                    LayoutElement layoutElement = entryContainer.AddComponent<LayoutElement>();
                    layoutElement.minHeight = 40;
                    layoutElement.preferredHeight = 40;
                    layoutElement.flexibleHeight = 0;
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
            Player.Instance.EnablePlayerMouseLook(true, false);
            Player.Instance.EnableCharacterController(true);
            SessionData sessionData = SessionData.Instance;
            InputController.Instance.enabled = true;
            sessionData.ResumeGame();
        }
        
        private void SelectBusiness(int index)
        {
            if (index >= 0 && index < availableBusinesses.Count)
            {
                // Update selection state
                int previousIndex = selectedBusinessIndex;
                selectedBusinessIndex = index;
                purchaseButton.Component.interactable = true;
                
                // Update visual appearance of business entries
                for (int i = 0; i < businessButtons.Count; i++)
                {
                    if (i < availableBusinesses.Count)
                    {
                        // Update background color for selected/deselected items
                        Transform entryTransform = businessButtons[i].GameObject.transform.parent;
                        if (entryTransform != null)
                        {
                            Image bgImage = entryTransform.GetComponent<Image>();
                            if (bgImage != null)
                            {
                                bgImage.color = (i == selectedBusinessIndex) 
                                    ? new Color(0.3f, 0.5f, 0.7f, 0.5f) 
                                    : new Color(0.2f, 0.2f, 0.2f, 0.5f);
                            }
                            
                            // Update text color
                            Transform textTransform = entryTransform.Find($"BusinessText_{i}");
                            if (textTransform != null)
                            {
                                Text entryText = textTransform.GetComponent<Text>();
                                if (entryText != null)
                                {
                                    entryText.color = (i == selectedBusinessIndex) 
                                        ? Color.white 
                                        : new Color(0.9f, 0.9f, 0.9f, 1f);
                                }
                            }
                        }
                    }
                }
                
                var selectedBusiness = availableBusinesses[index];
                string name = selectedBusiness.name?.ToString() ?? "Unknown";
                Plugin.Logger.LogInfo($"Selected business: {name} (Index: {index})");
            }
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