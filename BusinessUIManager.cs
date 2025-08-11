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
using SOD.Common.Extensions;

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
                // After toggling/creating, manage game pause & input state based on panel visibility
                if (businessPanel != null && !businessPanel.Enabled)
                {
                    uiEnabled = false;
                    try
                    {
                        // Force any UniverseLib panel resizing to end
                        UniverseLib.UI.Panels.PanelManager.ForceEndResize();
                        
                        // Ensure the panel is fully closed
                        businessPanel.SetActive(false);
                        
                        // Reset the event system focus
                        if (UnityEngine.EventSystems.EventSystem.current != null)
                        {
                            // Clear selection
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                            
                            // Optionally set focus back to game canvas if it exists
                            var gameCanvas = UnityEngine.GameObject.Find("GameCanvas");
                            if (gameCanvas != null)
                            {
                                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(gameCanvas);
                            }
                        }
                        
                        // Reset input through UniverseLib
                        UniverseLib.Input.InputManager.ResetInputAxes();
                        
                        // Re-enable game controls (order matters)
                        InputController.Instance.enabled = true;
                        Player.Instance.EnableCharacterController(true);
                        Player.Instance.EnablePlayerMouseLook(true, true);
                        
                        // Resume the game
                        SessionData sessionData = SessionData.Instance;
                        sessionData.ResumeGame();
                        
                        // Lock cursor for game control
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                        
                        Plugin.Logger.LogInfo("Game input restored, UI closed");
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to update input control: {ex.Message}");
                    }
                }
                else if (businessPanel != null && businessPanel.Enabled)
                {
                    uiEnabled = true;
                    
                    // Disable game controls while UI is open
                    Player.Instance.EnablePlayerMouseLook(false, false);
                    Player.Instance.EnableCharacterController(false);
                    InputController.Instance.enabled = false;
                    
                    // Pause the game
                    SessionData sessionData = SessionData.Instance;
                    sessionData.PauseGame(false, false, true);
                    
                    // Show cursor for UI interaction
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    
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
        // Panel states
        private enum PanelState
        {
            MainMenu,
            PurchaseBusiness,
            OwnedBusinesses,
            BusinessDetails,
            EmployeeDetails
        }
        
        private PanelState currentState = PanelState.MainMenu;
        
        // UI containers for different states
        private GameObject mainMenuContainer;
        private GameObject purchaseBusinessContainer;
        private GameObject ownedBusinessesContainer;
        private GameObject employeeDetailsContainer;
        
        // Purchase business elements
        private List<BusinessInfo> availableBusinesses;
        private List<ButtonRef> businessButtons = new List<ButtonRef>();
        private int selectedBusinessIndex = -1;
        private GameObject businessListContainer;
        private ButtonRef purchaseButton;
        private ButtonRef closeButton;
        private ButtonRef refreshButton;
        
        // Owned businesses elements
        public static List<OwnedBusinessData> ownedBusinesses;
        private GameObject ownedBusinessListContainer;
        // Business details elements
        private GameObject businessDetailsContainer;
        private GameObject employeeListContainer;
        private Text businessDetailsTitle;
        private Text businessDetailsInfo;
        private OwnedBusinessData selectedOwnedBusiness;
        private EmployeeDetailsView employeeDetailsView;
        private Citizen selectedEmployeeCitizen;
        private Occupation selectedEmployeeOccupation;
        // Multi-window employee details support
        private readonly List<GameObject> openEmployeeWindowContainers = new List<GameObject>();
        private readonly System.Collections.Generic.Dictionary<GameObject, EmployeeDetailsView> employeeWindowViews = new System.Collections.Generic.Dictionary<GameObject, EmployeeDetailsView>();
        private GameObject employeeWindowsOverlay; // overlay parent for floating windows (ignored by layout)
        
        // Required abstract properties
        public override string Name => "Business Management";
        public override int MinWidth => 800;
        public override int MinHeight => 500;
        public override UnityEngine.Vector2 DefaultAnchorMin => new UnityEngine.Vector2(0.25f, 0.25f);
        public override UnityEngine.Vector2 DefaultAnchorMax => new UnityEngine.Vector2(0.75f, 0.75f);
        
        public BusinessPanel(UIBase owner) : base(owner) { }
        
        protected override void ConstructPanelContent()
        {
            try
            {
                Plugin.Logger.LogInfo("Constructing panel content...");
                
                // Create main title
                Text titleText = UIFactory.CreateLabel(ContentRoot, "TitleText", "Manage Businesses", TextAnchor.MiddleCenter);
                titleText.fontSize = 24;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
                RectTransform titleRect = titleText.GetComponent<RectTransform>();
                titleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
                titleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
                titleRect.offsetMin = UnityEngine.Vector2.zero;
                titleRect.offsetMax = UnityEngine.Vector2.zero;
                
                // Create containers for different panel states
                mainMenuContainer = UIFactory.CreateUIObject("MainMenuContainer", ContentRoot);
                purchaseBusinessContainer = UIFactory.CreateUIObject("PurchaseBusinessContainer", ContentRoot);
                ownedBusinessesContainer = UIFactory.CreateUIObject("OwnedBusinessesContainer", ContentRoot);
                businessDetailsContainer = UIFactory.CreateUIObject("BusinessDetailsContainer", ContentRoot);
                employeeDetailsContainer = UIFactory.CreateUIObject("EmployeeDetailsContainer", ContentRoot);
                
                // Set up the main menu container
                RectTransform mainMenuRect = mainMenuContainer.GetComponent<RectTransform>();
                mainMenuRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
                mainMenuRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
                mainMenuRect.offsetMin = UnityEngine.Vector2.zero;
                mainMenuRect.offsetMax = UnityEngine.Vector2.zero;
                LayoutElement mainMenuLayout = mainMenuContainer.AddComponent<LayoutElement>();
                mainMenuLayout.flexibleWidth = 1;
                mainMenuLayout.flexibleHeight = 1;
                
                // Set up the purchase business container
                RectTransform purchaseRect = purchaseBusinessContainer.GetComponent<RectTransform>();
                purchaseRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
                purchaseRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
                purchaseRect.offsetMin = UnityEngine.Vector2.zero;
                purchaseRect.offsetMax = UnityEngine.Vector2.zero;
                LayoutElement purchaseLayout = purchaseBusinessContainer.AddComponent<LayoutElement>();
                purchaseLayout.flexibleWidth = 1;
                purchaseLayout.flexibleHeight = 1;
                
                // Set up the owned businesses container
                RectTransform ownedRect = ownedBusinessesContainer.GetComponent<RectTransform>();
                ownedRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
                ownedRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
                ownedRect.offsetMin = UnityEngine.Vector2.zero;
                ownedRect.offsetMax = UnityEngine.Vector2.zero;
                LayoutElement ownedLayout = ownedBusinessesContainer.AddComponent<LayoutElement>();
                ownedLayout.flexibleWidth = 1;
                ownedLayout.flexibleHeight = 1;

                // Set up the business details container
                RectTransform detailsRect = businessDetailsContainer.GetComponent<RectTransform>();
                detailsRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
                detailsRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
                detailsRect.offsetMin = UnityEngine.Vector2.zero;
                detailsRect.offsetMax = UnityEngine.Vector2.zero;
                LayoutElement detailsLayout = businessDetailsContainer.AddComponent<LayoutElement>();
                detailsLayout.flexibleWidth = 1;
                detailsLayout.flexibleHeight = 1;

                // Set up the employee details container (separate panel)
                RectTransform empDetailsRect = employeeDetailsContainer.GetComponent<RectTransform>();
                empDetailsRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
                empDetailsRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
                empDetailsRect.offsetMin = UnityEngine.Vector2.zero;
                empDetailsRect.offsetMax = UnityEngine.Vector2.zero;
                LayoutElement empDetailsLayout = employeeDetailsContainer.AddComponent<LayoutElement>();
                empDetailsLayout.flexibleWidth = 1;
                empDetailsLayout.flexibleHeight = 1;

                // Create overlay for floating employee windows OUTSIDE the main panel, so it truly floats
                GameObject uiRoot = null;
                try { uiRoot = this.Owner != null ? this.Owner.RootObject : null; } catch { uiRoot = null; }
                if (uiRoot == null) uiRoot = ContentRoot; // fallback
                employeeWindowsOverlay = UIFactory.CreateUIObject("EmployeeWindowsOverlay", uiRoot);
                RectTransform overlayRt = employeeWindowsOverlay.GetComponent<RectTransform>();
                overlayRt.anchorMin = new UnityEngine.Vector2(0f, 0f);
                overlayRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
                overlayRt.offsetMin = UnityEngine.Vector2.zero;
                overlayRt.offsetMax = UnityEngine.Vector2.zero;
                var overlayLe = employeeWindowsOverlay.AddComponent<LayoutElement>();
                overlayLe.ignoreLayout = true;
                var overlayCanvas = employeeWindowsOverlay.AddComponent<Canvas>();
                overlayCanvas.overrideSorting = true;
                // Sort above the entire UI root to avoid being clipped by the panel
                overlayCanvas.sortingOrder = GetTopCanvasOrder(uiRoot) + 50;
                if (employeeWindowsOverlay.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    employeeWindowsOverlay.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                try { employeeWindowsOverlay.transform.SetAsLastSibling(); } catch { }
                employeeWindowsOverlay.SetActive(false);
                
                // Set up the main menu
                SetupMainMenu();
                
                // Set up the purchase business panel
                SetupPurchaseBusinessPanel();
                
                // Set up the owned businesses panel
                SetupOwnedBusinessesPanel();
                
                // Set up the business details panel
                SetupBusinessDetailsPanel();

                // Set up the employee details panel
                SetupEmployeeDetailsPanel();
                
                // Show the main menu initially
                SwitchPanel(PanelState.MainMenu);
                
                Plugin.Logger.LogInfo("Panel content constructed successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error constructing panel content: {ex.Message}\n{ex.StackTrace}");
            }
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
            
            // Only refresh when the panel is first activated
            if (active && !wasActiveLastFrame)
            {
                Plugin.Logger.LogInfo("Panel activated for the first time");
                
                // If we're coming back to the panel, go to the main menu
                if (currentState != PanelState.MainMenu)
                {
                    SwitchPanel(PanelState.MainMenu);
                }
            }
            
            // Update the active state for next frame
            wasActiveLastFrame = active;
        }
        
        // Method to switch between different panel states
        private void SwitchPanel(PanelState state)
        {
            Plugin.Logger.LogInfo($"Switching to panel state: {state}");
            currentState = state;
            
            // Hide all containers first
            mainMenuContainer.SetActive(false);
            purchaseBusinessContainer.SetActive(false);
            ownedBusinessesContainer.SetActive(false);
            businessDetailsContainer.SetActive(false);
            if (employeeDetailsContainer != null)
                employeeDetailsContainer.SetActive(false);
            // Manage visibility of any floating employee windows based on target state
            try
            {
                bool showEmpWindows = (state == PanelState.BusinessDetails);
                if (employeeWindowsOverlay != null) employeeWindowsOverlay.SetActive(showEmpWindows);
                for (int i = 0; i < openEmployeeWindowContainers.Count; i++)
                {
                    var go = openEmployeeWindowContainers[i];
                    if (go != null) go.SetActive(showEmpWindows);
                }
            }
            catch { }
            
            // Show the appropriate container
            switch (state)
            {
                case PanelState.MainMenu:
                    mainMenuContainer.SetActive(true);
                    break;
                    
                case PanelState.PurchaseBusiness:
                    purchaseBusinessContainer.SetActive(true);
                    RefreshBusinessList();
                    break;
                    
                case PanelState.OwnedBusinesses:
                    ownedBusinessesContainer.SetActive(true);
                    RefreshOwnedBusinessesList();
                    break;
                
                case PanelState.BusinessDetails:
                    businessDetailsContainer.SetActive(true);
                    RefreshBusinessDetails();
                    // Reset selected employee when returning to list
                    selectedEmployeeCitizen = null;
                    selectedEmployeeOccupation = null;
                    if (employeeDetailsView != null)
                    {
                        try { employeeDetailsView.Hide(); } catch { }
                    }
                    break;
                
                case PanelState.EmployeeDetails:
                    if (employeeDetailsContainer != null)
                    {
                        employeeDetailsContainer.SetActive(true);
                        // bring to front in hierarchy
                        try { employeeDetailsContainer.transform.SetAsLastSibling(); } catch { }
                        try { Canvas.ForceUpdateCanvases(); } catch { }
                    }
                    // Auto-show last selected employee, if available
                    if (employeeDetailsView != null && (selectedEmployeeCitizen != null || selectedEmployeeOccupation != null))
                    {
                        try
                        {
                            Plugin.Logger.LogInfo($"Showing employee details: {(selectedEmployeeCitizen != null ? selectedEmployeeCitizen.GetCitizenName() : "<null>")}");
                            employeeDetailsView.Show(selectedEmployeeCitizen, selectedEmployeeOccupation);
                            // Ensure the view's canvas is sorted above any siblings
                            try { employeeDetailsView.BringToFront(); } catch { }
                        }
                        catch (System.Exception ex)
                        {
                            Plugin.Logger.LogError($"Error auto-showing employee details: {ex.Message}");
                        }
                    }
                    break;
            }
            
            Plugin.Logger.LogInfo($"Panel switched to {state}");
        }
        
        // Set up the main menu panel
        private void SetupMainMenu()
        {
            // Create subtitle
            Text subtitleText = UIFactory.CreateLabel(mainMenuContainer, "SubtitleText", "Select an option:", TextAnchor.MiddleCenter);
            subtitleText.fontSize = 18;
            subtitleText.color = Color.white;
            RectTransform subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new UnityEngine.Vector2(0.1f, 0.8f);
            subtitleRect.anchorMax = new UnityEngine.Vector2(0.9f, 0.9f);
            subtitleRect.offsetMin = UnityEngine.Vector2.zero;
            subtitleRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create button container
            GameObject buttonContainer = UIFactory.CreateVerticalGroup(mainMenuContainer, "MainMenuButtonContainer", true, false, true, true, 20);
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new UnityEngine.Vector2(0.2f, 0.3f);
            buttonContainerRect.anchorMax = new UnityEngine.Vector2(0.8f, 0.8f);
            buttonContainerRect.offsetMin = UnityEngine.Vector2.zero;
            buttonContainerRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create main menu buttons
            ButtonRef purchaseBusinessButton = UIFactory.CreateButton(buttonContainer, "PurchaseBusinessButton", "Purchase a Business");
            purchaseBusinessButton.OnClick += () => SwitchPanel(PanelState.PurchaseBusiness);
            purchaseBusinessButton.ButtonText.fontSize = 18;
            SetupButton(purchaseBusinessButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            
            ButtonRef ownedBusinessesButton = UIFactory.CreateButton(buttonContainer, "OwnedBusinessesButton", "Manage Owned Businesses");
            ownedBusinessesButton.OnClick += () => SwitchPanel(PanelState.OwnedBusinesses);
            ownedBusinessesButton.ButtonText.fontSize = 18;
            SetupButton(ownedBusinessesButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            
            // Add layout elements to buttons for proper sizing
            // Use a for loop instead of foreach to avoid IL2CPP issues with GetEnumerator
            int childCount = buttonContainer.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = buttonContainer.transform.GetChild(i);
                LayoutElement layout = child.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 60;
                layout.preferredHeight = 60;
            }
            
            // Create close button at the bottom
            GameObject closeButtonContainer = UIFactory.CreateHorizontalGroup(mainMenuContainer, "CloseButtonContainer", true, false, true, true, 10);
            RectTransform closeButtonRect = closeButtonContainer.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new UnityEngine.Vector2(0.3f, 0.1f);
            closeButtonRect.anchorMax = new UnityEngine.Vector2(0.7f, 0.2f);
            closeButtonRect.offsetMin = UnityEngine.Vector2.zero;
            closeButtonRect.offsetMax = UnityEngine.Vector2.zero;
            
            ButtonRef mainCloseButton = UIFactory.CreateButton(closeButtonContainer, "MainCloseButton", "Close");
            mainCloseButton.OnClick += CloseBusinessUI;
            mainCloseButton.ButtonText.fontSize = 16;
            SetupButton(mainCloseButton, new Color(0.8f, 0.2f, 0.2f, 1f));
        }
        
        // Set up the purchase business panel
        private void SetupPurchaseBusinessPanel()
        {
            // Create title for purchase panel
            Text purchaseTitle = UIFactory.CreateLabel(purchaseBusinessContainer, "PurchaseTitle", "Available Businesses", TextAnchor.MiddleCenter);
            purchaseTitle.fontSize = 20;
            purchaseTitle.fontStyle = FontStyle.Bold;
            purchaseTitle.color = Color.white;
            RectTransform purchaseTitleRect = purchaseTitle.GetComponent<RectTransform>();
            purchaseTitleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
            purchaseTitleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
            purchaseTitleRect.offsetMin = UnityEngine.Vector2.zero;
            purchaseTitleRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create instructions text
            Text instructionsText = UIFactory.CreateLabel(purchaseBusinessContainer, "InstructionsText", "Click on a business to select it", TextAnchor.MiddleCenter);
            instructionsText.fontSize = 14;
            instructionsText.color = Color.yellow;
            RectTransform instructionsRect = instructionsText.GetComponent<RectTransform>();
            instructionsRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.85f);
            instructionsRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.9f);
            instructionsRect.offsetMin = UnityEngine.Vector2.zero;
            instructionsRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create a scroll view for the business list
            GameObject scrollObj = UIFactory.CreateScrollView(purchaseBusinessContainer, "BusinessListScroll", out GameObject scrollContent, out _);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.25f);
            scrollRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = UnityEngine.Vector2.zero;
            scrollRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the business list container
            businessListContainer = UIFactory.CreateVerticalGroup(scrollContent, "BusinessListContainer", true, false, true, true, 5);
            businessListContainer.GetComponent<RectTransform>().sizeDelta = new UnityEngine.Vector2(0, 0);
            
            // Create button container
            GameObject buttonContainer = UIFactory.CreateHorizontalGroup(purchaseBusinessContainer, "ButtonContainer", true, false, true, true, 10);
            
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
            buttonContainerRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.2f);
            buttonContainerRect.offsetMin = UnityEngine.Vector2.zero;
            buttonContainerRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the back button
            ButtonRef backButton = UIFactory.CreateButton(buttonContainer, "BackButton", "Back to Menu");
            backButton.OnClick += () => SwitchPanel(PanelState.MainMenu);
            backButton.ButtonText.fontSize = 14;
            SetupButton(backButton, new Color(0.6f, 0.6f, 0.6f, 1f));
            
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
        }
        
        // Set up the owned businesses panel
        private void SetupOwnedBusinessesPanel()
        {
            // Create title for owned businesses panel
            Text ownedTitle = UIFactory.CreateLabel(ownedBusinessesContainer, "OwnedTitle", "Your Businesses", TextAnchor.MiddleCenter);
            ownedTitle.fontSize = 20;
            ownedTitle.fontStyle = FontStyle.Bold;
            ownedTitle.color = Color.white;
            RectTransform ownedTitleRect = ownedTitle.GetComponent<RectTransform>();
            ownedTitleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
            ownedTitleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
            ownedTitleRect.offsetMin = UnityEngine.Vector2.zero;
            ownedTitleRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create a scroll view for the owned business list
            GameObject scrollObj = UIFactory.CreateScrollView(ownedBusinessesContainer, "OwnedBusinessListScroll", out GameObject scrollContent, out _);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.25f);
            scrollRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = UnityEngine.Vector2.zero;
            scrollRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the owned business list container
            ownedBusinessListContainer = UIFactory.CreateVerticalGroup(scrollContent, "OwnedBusinessListContainer", true, false, true, true, 5);
            ownedBusinessListContainer.GetComponent<RectTransform>().sizeDelta = new UnityEngine.Vector2(0, 0);
            
            // Create placeholder text
            Text placeholderText = UIFactory.CreateLabel(ownedBusinessListContainer, "PlaceholderText", "This panel will show your owned businesses", TextAnchor.MiddleCenter);
            placeholderText.fontSize = 16;
            placeholderText.color = Color.white;
            
            // Create button container
            GameObject buttonContainer = UIFactory.CreateHorizontalGroup(ownedBusinessesContainer, "OwnedButtonContainer", true, false, true, true, 10);
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
            buttonContainerRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.2f);
            buttonContainerRect.offsetMin = UnityEngine.Vector2.zero;
            buttonContainerRect.offsetMax = UnityEngine.Vector2.zero;
            
            // Create the back button
            ButtonRef backButton = UIFactory.CreateButton(buttonContainer, "OwnedBackButton", "Back to Menu");
            backButton.OnClick += () => SwitchPanel(PanelState.MainMenu);
            backButton.ButtonText.fontSize = 14;
            SetupButton(backButton, new Color(0.6f, 0.6f, 0.6f, 1f));
            
            // Create the refresh button
            ButtonRef ownedRefreshButton = UIFactory.CreateButton(buttonContainer, "OwnedRefreshButton", "Refresh List");
            ownedRefreshButton.OnClick += RefreshOwnedBusinessesList;
            ownedRefreshButton.ButtonText.fontSize = 14;
            SetupButton(ownedRefreshButton, new Color(0.2f, 0.6f, 0.9f, 1f));
            
            // Create the close button
            ButtonRef ownedCloseButton = UIFactory.CreateButton(buttonContainer, "OwnedCloseButton", "Close");
            ownedCloseButton.OnClick += CloseBusinessUI;
            ownedCloseButton.ButtonText.fontSize = 14;
            SetupButton(ownedCloseButton, new Color(0.8f, 0.2f, 0.2f, 1f));
            RefreshOwnedBusinessesList();
        }
        
        // Select an owned business and open details
        private void SelectOwnedBusiness(int index)
        {
            try
            {
                if (ownedBusinesses == null || index < 0 || index >= ownedBusinesses.Count)
                    return;

                selectedOwnedBusiness = ownedBusinesses[index];
                Plugin.Logger.LogInfo($"Selected owned business: {selectedOwnedBusiness.BusinessName} (ID: {selectedOwnedBusiness.AddressId})");
                SwitchPanel(PanelState.BusinessDetails);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error selecting owned business: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Set up the business details panel UI
        private void SetupBusinessDetailsPanel()
        {
            // Title
            Text title = UIFactory.CreateLabel(businessDetailsContainer, "DetailsTitle", "Business Details", TextAnchor.MiddleCenter);
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
            titleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
            titleRect.offsetMin = UnityEngine.Vector2.zero;
            titleRect.offsetMax = UnityEngine.Vector2.zero;

            // Business name
            businessDetailsTitle = UIFactory.CreateLabel(businessDetailsContainer, "DetailsBusinessName", "", TextAnchor.MiddleCenter);
            businessDetailsTitle.fontSize = 18;
            businessDetailsTitle.color = Color.white;
            RectTransform nameRect = businessDetailsTitle.GetComponent<RectTransform>();
            nameRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.83f);
            nameRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
            nameRect.offsetMin = UnityEngine.Vector2.zero;
            nameRect.offsetMax = UnityEngine.Vector2.zero;

            // Info line
            businessDetailsInfo = UIFactory.CreateLabel(businessDetailsContainer, "DetailsInfo", "", TextAnchor.MiddleCenter);
            businessDetailsInfo.fontSize = 14;
            businessDetailsInfo.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            RectTransform infoRectHdr = businessDetailsInfo.GetComponent<RectTransform>();
            infoRectHdr.anchorMin = new UnityEngine.Vector2(0.05f, 0.78f);
            infoRectHdr.anchorMax = new UnityEngine.Vector2(0.95f, 0.83f);
            infoRectHdr.offsetMin = UnityEngine.Vector2.zero;
            infoRectHdr.offsetMax = UnityEngine.Vector2.zero;

            // (Employee details are now in their own panel.)

            // Employees list scroll view
            GameObject scrollObj = UIFactory.CreateScrollView(businessDetailsContainer, "EmployeeListScroll", out GameObject scrollContent, out _);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.25f);
            // Full height up to info row since details view moved to its own panel
            scrollRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.76f);
            scrollRect.offsetMin = UnityEngine.Vector2.zero;
            scrollRect.offsetMax = UnityEngine.Vector2.zero;


            employeeListContainer = UIFactory.CreateVerticalGroup(scrollContent, "EmployeeListContainer", true, false, true, true, 5);
            employeeListContainer.GetComponent<RectTransform>().sizeDelta = new UnityEngine.Vector2(0, 0);

            // Bottom buttons
            GameObject buttonContainer = UIFactory.CreateHorizontalGroup(businessDetailsContainer, "DetailsButtonContainer", true, false, true, true, 10);
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
            buttonContainerRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.2f);
            buttonContainerRect.offsetMin = UnityEngine.Vector2.zero;
            buttonContainerRect.offsetMax = UnityEngine.Vector2.zero;

            ButtonRef backButton = UIFactory.CreateButton(buttonContainer, "DetailsBackButton", "Back to Owned Businesses");
            backButton.OnClick += () => SwitchPanel(PanelState.OwnedBusinesses);
            backButton.ButtonText.fontSize = 14;
            SetupButton(backButton, new Color(0.6f, 0.6f, 0.6f, 1f));

            ButtonRef closeButton = UIFactory.CreateButton(buttonContainer, "DetailsCloseButton", "Close");
            closeButton.OnClick += CloseBusinessUI;
            closeButton.ButtonText.fontSize = 14;
            SetupButton(closeButton, new Color(0.8f, 0.2f, 0.2f, 1f));
        }

        // Set up the separate employee details panel (modal-like)
        private void SetupEmployeeDetailsPanel()
        {
            // Ensure container has its own canvas above others
            var empCanvas = employeeDetailsContainer.GetComponent<Canvas>();
            if (empCanvas == null) empCanvas = employeeDetailsContainer.AddComponent<Canvas>();
            empCanvas.overrideSorting = true;
            // Compute a safe sorting order above any sibling canvases to ensure visibility
            int empDesired = 5000;
            try
            {
                int maxOrder = empDesired;
                var canvases = ContentRoot.GetComponentsInChildren<Canvas>(true);
                if (canvases != null)
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        if (canvases[i] != null)
                        {
                            int ord = canvases[i].sortingOrder;
                            if (ord > maxOrder) maxOrder = ord;
                        }
                    }
                }
                if (maxOrder < 0) maxOrder = 20000;
                empDesired = maxOrder + 5;
            }
            catch { }
            if (empDesired > 32760) empDesired = 32760;
            if (empDesired < -32760) empDesired = -32760;
            empCanvas.sortingOrder = empDesired;
            try { Plugin.Logger.LogInfo($"EmpDetails Container canvas set to order={empCanvas.sortingOrder}"); } catch { }
            if (employeeDetailsContainer.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                employeeDetailsContainer.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            // Ensure container is visible and interactable
            var empCg = employeeDetailsContainer.GetComponent<CanvasGroup>();
            if (empCg == null) empCg = employeeDetailsContainer.AddComponent<CanvasGroup>();
            empCg.alpha = 1f;
            empCg.interactable = true;
            empCg.blocksRaycasts = true;
            // Add a subtle background to visually confirm rendering; do not block clicks outside
            try
            {
                GameObject empBg = UIFactory.CreateUIObject("EmpPanelBG", employeeDetailsContainer);
                var empBgImg = empBg.GetComponent<Image>();
                if (empBgImg == null) empBgImg = empBg.AddComponent<Image>();
                empBgImg.color = new Color(0f, 0f, 0f, 0.6f);
                empBgImg.raycastTarget = false;
                RectTransform empBgRt = empBg.GetComponent<RectTransform>();
                empBgRt.anchorMin = new UnityEngine.Vector2(0f, 0f);
                empBgRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
                empBgRt.offsetMin = UnityEngine.Vector2.zero;
                empBgRt.offsetMax = UnityEngine.Vector2.zero;
                empBg.transform.SetAsFirstSibling();
            }
            catch { }

            // Title
            Text title = UIFactory.CreateLabel(employeeDetailsContainer, "EmpDetailsTitle", "Employee Details", TextAnchor.MiddleCenter);
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.9f);
            titleRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.97f);
            titleRect.offsetMin = UnityEngine.Vector2.zero;
            titleRect.offsetMax = UnityEngine.Vector2.zero;

            // Optional info line (can be used later for hints)
            Text info = UIFactory.CreateLabel(employeeDetailsContainer, "EmpDetailsInfo", "", TextAnchor.MiddleCenter);
            info.fontSize = 14;
            info.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            RectTransform infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.84f);
            infoRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.89f);
            infoRect.offsetMin = UnityEngine.Vector2.zero;
            infoRect.offsetMax = UnityEngine.Vector2.zero;

            // Build the details view in this container and expand for panel layout
            employeeDetailsView = new EmployeeDetailsView();
            employeeDetailsView.Build(employeeDetailsContainer);
            employeeDetailsView.ConfigureAsPanelLayout();
            // Make absolutely sure the details view renders above siblings in this container
            try { employeeDetailsView.BringToFront(); } catch { }

            // Bottom buttons
            GameObject buttonRow = UIFactory.CreateHorizontalGroup(employeeDetailsContainer, "EmpDetailsButtons", true, false, true, true, 10);
            RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
            buttonRowRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.05f);
            buttonRowRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.12f);
            buttonRowRect.offsetMin = UnityEngine.Vector2.zero;
            buttonRowRect.offsetMax = UnityEngine.Vector2.zero;

            ButtonRef backBtn = UIFactory.CreateButton(buttonRow, "EmpBackButton", "Back to Business Details");
            backBtn.OnClick += () => SwitchPanel(PanelState.BusinessDetails);
            backBtn.ButtonText.fontSize = 14;
            SetupButton(backBtn, new Color(0.6f, 0.6f, 0.6f, 1f));

            ButtonRef closeBtn = UIFactory.CreateButton(buttonRow, "EmpCloseButton", "Close");
            closeBtn.OnClick += CloseBusinessUI;
            closeBtn.ButtonText.fontSize = 14;
            SetupButton(closeBtn, new Color(0.8f, 0.2f, 0.2f, 1f));
        }

        // Populate the business details and employee list
        private void RefreshBusinessDetails()
        {
            try
            {
                if (selectedOwnedBusiness == null)
                {
                    Plugin.Logger.LogWarning("No owned business selected for details view");
                    return;
                }

                // Update headers
                string formattedDate = GetPurchaseDate(selectedOwnedBusiness.PurchaseDate);
                businessDetailsTitle.text = selectedOwnedBusiness.BusinessName ?? "Unknown Business";
                businessDetailsInfo.text = $"Type: {selectedOwnedBusiness.Type} | Income: {selectedOwnedBusiness.DailyIncome} Crows/day | Employees: {selectedOwnedBusiness.EmployeeCount} | Floor: {selectedOwnedBusiness.FloorName} | Purchased: {formattedDate}";

                // Hide details until a row is clicked
                if (employeeDetailsView != null)
                {
                    employeeDetailsView.Hide();
                }

                // Clear existing rows
                if (employeeListContainer != null && employeeListContainer.transform != null)
                {
                    int childCount = employeeListContainer.transform.childCount;
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        Transform child = employeeListContainer.transform.GetChild(i);
                        if (child != null && child.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }

                // Try to resolve the apartment/company roster from the game
                NewAddress apartment = FindApartmentById(selectedOwnedBusiness.AddressId);
                if (apartment != null && apartment.company != null && apartment.company.companyRoster != null && apartment.company.companyRoster.Count > 0)
                {
                    // Build a row for each occupation/employee
                    for (int i = 0; i < apartment.company.companyRoster.Count; i++)
                    {
                        Occupation occ = apartment.company.companyRoster[i];
                        string empName = "Vacant";
                        try
                        {
                            if (occ != null && occ.employee != null)
                            {
                                Citizen citizen = occ.employee as Citizen;
                                if (citizen != null)
                                {
                                    empName = citizen.GetCitizenName();
                                }
                                else
                                {
                                    empName = occ.employee.name?.ToString() ?? "Employee";
                                }
                            }
                        }
                        catch { }

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

                        GameObject row = UIFactory.CreateUIObject($"Employee_{i}", employeeListContainer);
                        // Increase row height to better fit a square portrait
                        var rowLE = row.AddComponent<LayoutElement>();
                        rowLE.minHeight = 64;
                        rowLE.preferredHeight = 64;
                        row.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.85f);

                        // Add employee photo on the left, if available
                        try
                        {
                            GameObject photoGO = UIFactory.CreateUIObject($"EmployeePhoto_{i}", row);
                            RawImage photoImg = photoGO.AddComponent<RawImage>();
                            RectTransform photoRect = photoGO.GetComponent<RectTransform>();
                            // Fixed square size aligned to left
                            photoRect.anchorMin = new UnityEngine.Vector2(0f, 0.5f);
                            photoRect.anchorMax = new UnityEngine.Vector2(0f, 0.5f);
                            photoRect.pivot = new UnityEngine.Vector2(0f, 0.5f);
                            photoRect.anchoredPosition = new UnityEngine.Vector2(8f, 0f);
                            photoRect.sizeDelta = new UnityEngine.Vector2(48f, 48f);

                            // Try to fetch the citizen's evidence photo texture
                            Citizen citizenForPhoto = null;
                            try { citizenForPhoto = occ != null ? occ.employee as Citizen : null; } catch { }
                            if (citizenForPhoto != null && citizenForPhoto.evidenceEntry != null)
                            {
                                // Build IL2CPP List<Evidence.DataKey> for GetPhoto
                                var photoKeys = new Il2CppSystem.Collections.Generic.List<Evidence.DataKey>();
                                photoKeys.Add(Evidence.DataKey.photo);
                                Texture2D tex = citizenForPhoto.evidenceEntry.GetPhoto(photoKeys);
                                if (tex != null)
                                {
                                    photoImg.texture = tex;
                                    photoImg.color = Color.white;
                                }
                            }
                        }
                        catch { }

                        Text rowText = UIFactory.CreateLabel(row, $"EmployeeText_{i}", $"{i + 1}. {empName} - {role}", TextAnchor.MiddleLeft);
                        rowText.fontSize = 14;
                        rowText.color = Color.white;
                        RectTransform rowRect = rowText.GetComponent<RectTransform>();
                        // Shift text right to leave space for the fixed-size photo
                        rowRect.anchorMin = new UnityEngine.Vector2(0.18f, 0f);
                        rowRect.anchorMax = new UnityEngine.Vector2(0.95f, 1f);
                        rowRect.offsetMin = UnityEngine.Vector2.zero;
                        rowRect.offsetMax = UnityEngine.Vector2.zero;

                        // Clickable overlay to open the details view
                        try
                        {
                            var occLocal = occ;
                            Citizen citizenLocal = null;
                            try { citizenLocal = occLocal != null ? occLocal.employee as Citizen : null; } catch { }

                            ButtonRef rowBtn = UIFactory.CreateButton(row, $"EmployeeRowBtn_{i}", "");
                            // Clear text and make the button background invisible but still clickable
                            if (rowBtn.ButtonText != null)
                                rowBtn.ButtonText.text = string.Empty;
                            var btnImg = rowBtn.Component.GetComponent<Image>();
                            if (btnImg != null)
                                btnImg.color = new Color(0f, 0f, 0f, 0.001f);
                            var btnRt = rowBtn.Component.GetComponent<RectTransform>();
                            btnRt.anchorMin = new UnityEngine.Vector2(0f, 0f);
                            btnRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
                            btnRt.offsetMin = UnityEngine.Vector2.zero;
                            btnRt.offsetMax = UnityEngine.Vector2.zero;
                            rowBtn.OnClick += () =>
                            {
                                try
                                {
                                    CreateEmployeeWindow(citizenLocal, occLocal);
                                }
                                catch (System.Exception ex)
                                {
                                    Plugin.Logger.LogError($"Error opening employee window: {ex.Message}");
                                }
                            };
                        }
                        catch { }
                    }
                }
                else
                {
                    // Fallback to any cached employees from extensions, or show a message
                    var fallbackEmployees = selectedOwnedBusiness.GetEmployees();
                    if (fallbackEmployees != null && fallbackEmployees.Count > 0)
                    {
                        for (int i = 0; i < fallbackEmployees.Count; i++)
                        {
                            var emp = fallbackEmployees[i];
                            GameObject row = UIFactory.CreateUIObject($"Employee_{i}", employeeListContainer);
                            row.AddComponent<LayoutElement>().preferredHeight = 40;
                            row.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.85f);

                            Text rowText = UIFactory.CreateLabel(row, $"EmployeeText_{i}", $"{i + 1}. {emp.Name} - {emp.Role}", TextAnchor.MiddleLeft);
                            rowText.fontSize = 14;
                            rowText.color = Color.white;
                            RectTransform rowRect = rowText.GetComponent<RectTransform>();
                            rowRect.anchorMin = new UnityEngine.Vector2(0.05f, 0f);
                            rowRect.anchorMax = new UnityEngine.Vector2(0.95f, 1f);
                            rowRect.offsetMin = UnityEngine.Vector2.zero;
                            rowRect.offsetMax = UnityEngine.Vector2.zero;
                        }
                    }
                    else
                    {
                        Text none = UIFactory.CreateLabel(employeeListContainer, "NoEmployeesText", "No employees found for this business.", TextAnchor.MiddleCenter);
                        none.fontSize = 16;
                        none.color = Color.yellow;
                        LayoutElement textLayout = none.gameObject.AddComponent<LayoutElement>();
                        textLayout.minHeight = 40;
                        textLayout.preferredHeight = 40;
                        textLayout.flexibleHeight = 0;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error refreshing business details: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private NewAddress FindApartmentById(int id)
        {
            try
            {
                if (Player.Instance == null || Player.Instance.apartmentsOwned == null)
                    return null;

                for (int i = 0; i < Player.Instance.apartmentsOwned.Count; i++)
                {
                    var apt = Player.Instance.apartmentsOwned[i];
                    if (apt != null && apt.id == id)
                        return apt;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error finding apartment by ID {id}: {ex.Message}");
            }
            return null;
        }
        
        
        // Method to refresh the owned businesses list
        private void RefreshOwnedBusinessesList()
        {
            try
            {
                Plugin.Logger.LogInfo("Refreshing owned businesses list...");
                
                // Get owned businesses using our method
                GetOwnedBusinesses();
                
                // Clear existing owned business entries
                if (ownedBusinessListContainer != null && ownedBusinessListContainer.transform != null)
                {
                    int childCount = ownedBusinessListContainer.transform.childCount;
                    for (int i = childCount - 1; i >= 0; i--)
                    {
                        Transform child = ownedBusinessListContainer.transform.GetChild(i);
                        if (child != null && child.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }
                }
                
                // Update the owned business list
                if (ownedBusinesses == null || ownedBusinesses.Count == 0)
                {
                    // Create a "No owned businesses" label
                    Text noBusinessesText = UIFactory.CreateLabel(ownedBusinessListContainer, "NoOwnedBusinessesText", "You don't own any businesses yet.", TextAnchor.MiddleCenter);
                    noBusinessesText.fontSize = 16;
                    noBusinessesText.color = Color.yellow;
                }
                else
                {
                    // Create entries for each owned business
                    for (int i = 0; i < ownedBusinesses.Count; i++)
                    {
                        var business = ownedBusinesses[i];
                        
                        // Create a panel for this business
                        GameObject businessPanel = UIFactory.CreateUIObject($"OwnedBusiness_{i}", ownedBusinessListContainer);
                        businessPanel.AddComponent<LayoutElement>().preferredHeight = 60;
                        businessPanel.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                        
                        // Add business name
                        Text businessName = UIFactory.CreateLabel(businessPanel, $"BusinessName_{i}", $"{i+1}. {business.BusinessName}", TextAnchor.MiddleLeft);
                        businessName.fontSize = 16;
                        businessName.color = Color.white;
                        RectTransform nameRect = businessName.GetComponent<RectTransform>();
                        nameRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.5f);
                        nameRect.anchorMax = new UnityEngine.Vector2(0.95f, 1f);
                        nameRect.offsetMin = UnityEngine.Vector2.zero;
                        nameRect.offsetMax = UnityEngine.Vector2.zero;
                        
                        // Get business type name from CompanyPresets mapping
                        string businessTypeName = "Office"; // Default

                        if (business.AddressId != null)
                        {
                            // Try to find the business in Player.Instance.apartmentsOwned
                            for (int j = 0; j < Player.Instance.apartmentsOwned.Count; j++)
                            {
                                var apartment = Player.Instance.apartmentsOwned[j];
                                if (apartment != null && apartment.id == business.AddressId)
                                {
                                    if (apartment.company != null && apartment.company.preset != null && !string.IsNullOrEmpty(apartment.company.preset.name))
                                    {
                                        CompanyPresets companyPresets = new CompanyPresets();
                                        string presetName = apartment.company.preset.name;
                                        
                                        // Find the matching preset in the mapping
                                        for (int k = 0; k < companyPresets.CompanyPresetsMapping.Length; k++)
                                        {
                                            if (presetName == companyPresets.CompanyPresetsMapping[k].Item1)
                                            {
                                                businessTypeName = companyPresets.CompanyPresetsMapping[k].Item2;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        
                        // Add business info with formatted purchase date
                        string formattedDate = GetPurchaseDate(business.PurchaseDate);
                        string infoText = $"Type: {businessTypeName} | Income: {business.DailyIncome} Crows/day | Employees: {business.EmployeeCount} | Purchase Date: {formattedDate}";
                        Text businessInfo = UIFactory.CreateLabel(businessPanel, $"BusinessInfo_{i}", infoText, TextAnchor.MiddleLeft);
                        businessInfo.fontSize = 12;
                        businessInfo.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                        RectTransform infoRect = businessInfo.GetComponent<RectTransform>();
                        infoRect.anchorMin = new UnityEngine.Vector2(0.05f, 0f);
                        infoRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.5f);
                        infoRect.offsetMin = UnityEngine.Vector2.zero;
                        infoRect.offsetMax = UnityEngine.Vector2.zero;

                        // Make the entire row clickable to view details
                        int capturedIndex = i;
                        Button rowButton = businessPanel.AddComponent<Button>();
                        ColorBlock rowColors = rowButton.colors;
                        rowColors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                        rowColors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
                        rowColors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                        rowColors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                        rowButton.colors = rowColors;
                        rowButton.onClick.AddListener(() => SelectOwnedBusiness(capturedIndex));
                    }
                }
                
                Plugin.Logger.LogInfo($"Displayed {ownedBusinesses?.Count ?? 0} owned businesses");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error refreshing owned business list: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Get owned businesses from Player.Instance.apartmentsOwned
        private void GetOwnedBusinesses()
        {
            try
            {
                // Get the list of owned businesses directly from BusinessManager
                var businessManagerList = BusinessManager.Instance.OwnedBusinesses;
                
                if (businessManagerList != null && businessManagerList.Count > 0)
                {
                    // Make a copy of the list to avoid reference issues
                    ownedBusinesses = new List<OwnedBusinessData>(businessManagerList);
                    Plugin.Logger.LogInfo($"Found {ownedBusinesses.Count} owned businesses in BusinessManager");
                    
                    // Log each business for debugging
                    for (int i = 0; i < ownedBusinesses.Count; i++)
                    {
                        var business = ownedBusinesses[i];
                        Plugin.Logger.LogInfo($"Owned Business {i+1}: {business.BusinessName} (ID: {business.AddressId})");
                    }
                }
                else
                {
                    // If no businesses found in BusinessManager, create a new empty list
                    ownedBusinesses = new List<OwnedBusinessData>();
                    Plugin.Logger.LogWarning("No businesses found in BusinessManager");
                    
                    // As a fallback, check if there are any valid businesses in Player.apartmentsOwned
                    if (Player.Instance.apartmentsOwned != null && Player.Instance.apartmentsOwned.Count > 0)
                    {
                        Plugin.Logger.LogInfo($"Checking {Player.Instance.apartmentsOwned.Count} owned apartments as fallback");
                        
                        // Convert each apartment to OwnedBusinessData, but only if it's in CompanyPresets
                        for (int i = 0; i < Player.Instance.apartmentsOwned.Count; i++)
                        {
                            var apartment = Player.Instance.apartmentsOwned[i];
                            if (apartment != null && IsValidBusiness(apartment))
                            {
                                string purchaseDate = SessionData.Instance.TimeAndDate(SessionData.Instance.gameTime, true, true, true);
                                
                                // Create a new OwnedBusinessData object for each business
                                OwnedBusinessData business = new OwnedBusinessData
                                {
                                    AddressId = apartment.id,
                                    BusinessName = apartment.name?.ToString() ?? "Unknown Business",
                                    Type = GetBusinessType(apartment),
                                    PurchaseDate = purchaseDate,
                                    DailyIncome = 500, // Default income
                                    UpgradeLevel = 0,
                                    EmployeeCount = GetEmployeeCount(apartment),
                                    CustomData = new Dictionary<string, object>()
                                };
                                
                                ownedBusinesses.Add(business);
                                Plugin.Logger.LogInfo($"Fallback - Added business {i+1}: {business.BusinessName} (ID: {business.AddressId})");
                                
                                // Also add to BusinessManager for future reference
                                BusinessManager.Instance.AddOwnedBusiness(apartment, business.BusinessName, 0, purchaseDate);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error getting owned businesses: {ex.Message}\n{ex.StackTrace}");
                ownedBusinesses = new List<OwnedBusinessData>();
            }
        }

        private static int GetEmployeeCount(NewAddress apartment)
        {
            try
            {
                // Check for null references
                if (apartment == null || apartment.company == null || apartment.company.companyRoster == null)
                {
                    return 0;
                }
                
                return apartment.company.companyRoster.Count;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error getting employee count: {ex.Message}");
                return 0;
            }
        }

        private static string GetBusinessType(NewAddress apartment)
        {
            try
            {
                if (apartment == null || apartment.company == null || apartment.company.preset == null)
                {
                    return "Unknown";
                }
                
                string presetName = apartment.company.preset.name;
                
                // Find the matching preset in the mapping
                CompanyPresets companyPresets = new CompanyPresets();
                for (int j = 0; j < companyPresets.CompanyPresetsMapping.Length; j++)
                {
                    if (presetName == companyPresets.CompanyPresetsMapping[j].Item1)
                    {
                        return companyPresets.CompanyPresetsMapping[j].Item2;
                    }
                }
                
                return "Unknown";
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error getting business type: {ex.Message}");
                return "Unknown";
            }
        }

        // Convert the stored game time value back to a formatted date string
        private static string GetPurchaseDate(string purchaseDate)
        {
            try
            {
                if (float.TryParse(purchaseDate, out float gameTime))
                {
                    return SessionData.Instance.TimeAndDate(gameTime, true, true, true);
                }
                return purchaseDate; // If it's already a formatted date string, return as is
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error converting purchase date: {ex.Message}");
                return purchaseDate;
            }
        }
        
        // Check if an apartment is a valid business (exists in CompanyPresets)
        private static bool IsValidBusiness(NewAddress apartment)
        {
            try
            {
                // Check for null references
                if (apartment == null || apartment.company == null || apartment.company.preset == null)
                {
                    return false;
                }
                
                string presetName = apartment.company.preset.name;
                if (string.IsNullOrEmpty(presetName))
                {
                    return false;
                }
                
                // Check if the preset exists in CompanyPresets
                CompanyPresets companyPresets = new CompanyPresets();
                for (int i = 0; i < companyPresets.CompanyPresetsList.Length; i++)
                {
                    if (presetName == companyPresets.CompanyPresetsList[i].Item1)
                    {
                        return true; // Found in the presets list
                    }
                }
                
                return false; // Not found in the presets list
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error checking if valid business: {ex.Message}");
                return false;
            }
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
                                int socialCreditLevel = companyPresets.CompanyPresetsList[j].Item3;

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
                                buttonText = $"{i + 1}. {name} | Type: {businessType} | Employees: {employeeCount} | Cost: {totalCost} Crows | SCL: {socialCreditLevel}";
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
            // Close any floating employee windows first
            try { CloseAllEmployeeWindows(); } catch { }
            SetActive(false);
            Player.Instance.EnableCharacterController(true);
            Player.Instance.EnablePlayerMouseLook(true, true);
            InputController.Instance.enabled = true;
            SessionData sessionData = SessionData.Instance;
            sessionData.ResumeGame();
            Cursor.lockState = CursorLockMode.Locked;

        }

        // Create a floating employee detail window on the right side
        private void CreateEmployeeWindow(Citizen citizen, Occupation occ)
        {
            if (ContentRoot == null) return;
            // Ensure the overlay is visible when opening a window
            try { if (employeeWindowsOverlay != null && !employeeWindowsOverlay.activeSelf) employeeWindowsOverlay.SetActive(true); } catch { }
            string name = citizen != null ? citizen.GetCitizenName() : (occ != null && occ.employee != null ? occ.employee.name?.ToString() : "Employee");
            string goName = $"EmpWindow_{openEmployeeWindowContainers.Count + 1}_{name}";
            // Parent to overlay so layout groups on ContentRoot can't affect it
            GameObject parentForWindows = employeeWindowsOverlay != null ? employeeWindowsOverlay : ContentRoot;
            GameObject win = UIFactory.CreateUIObject(goName, parentForWindows);
            RectTransform rt = win.GetComponent<RectTransform>();
            // Ensure this window is ignored by any parent layout system
            var winLe = win.GetComponent<LayoutElement>();
            if (winLe == null) winLe = win.AddComponent<LayoutElement>();
            winLe.ignoreLayout = true;
            rt.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
            // Cascade windows vertically on the right side
            int idx = openEmployeeWindowContainers.Count;
            float topStart = 0.95f - (idx * 0.38f);
            float bottom = topStart - 0.35f;
            if (bottom < 0.06f) { bottom = 0.06f; topStart = bottom + 0.35f; }
            rt.anchorMin = new UnityEngine.Vector2(0.60f, bottom);
            rt.anchorMax = new UnityEngine.Vector2(0.98f, topStart);
            rt.offsetMin = UnityEngine.Vector2.zero;
            rt.offsetMax = UnityEngine.Vector2.zero;
            try { win.transform.SetAsLastSibling(); } catch { }

            // Canvas and sorting
            var canvas = win.GetComponent<Canvas>();
            if (canvas == null) canvas = win.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            int overlayBase = employeeWindowsOverlay != null ? GetTopCanvasOrder(employeeWindowsOverlay) : GetTopCanvasOrder(ContentRoot);
            int baseOrder = overlayBase + 10 + (idx * 2);
            if (baseOrder > 32760) baseOrder = 32760;
            if (baseOrder < -32760) baseOrder = -32760;
            canvas.sortingOrder = baseOrder;
            if (win.GetComponent<GraphicRaycaster>() == null) win.AddComponent<GraphicRaycaster>();
            var cg = win.GetComponent<CanvasGroup>();
            if (cg == null) cg = win.AddComponent<CanvasGroup>();
            cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true;

            // Background
            Image bg = win.GetComponent<Image>();
            if (bg == null) bg = win.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.10f, 0.95f);
            bg.raycastTarget = true;

            // Title bar
            Text title = UIFactory.CreateLabel(win, $"EmpWinTitle_{idx}", $"{name} Details", TextAnchor.MiddleLeft);
            title.fontSize = 16; title.fontStyle = FontStyle.Bold; title.color = Color.white;
            RectTransform titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new UnityEngine.Vector2(0.03f, 0.92f);
            titleRt.anchorMax = new UnityEngine.Vector2(0.85f, 0.98f);
            titleRt.offsetMin = UnityEngine.Vector2.zero; titleRt.offsetMax = UnityEngine.Vector2.zero;

            // Close button
            ButtonRef closeBtn = UIFactory.CreateButton(win, $"EmpWinClose_{idx}", "Close");
            closeBtn.ButtonText.fontSize = 12;
            RectTransform closeRt = closeBtn.Component.GetComponent<RectTransform>();
            closeRt.anchorMin = new UnityEngine.Vector2(0.86f, 0.92f);
            closeRt.anchorMax = new UnityEngine.Vector2(0.97f, 0.98f);
            closeRt.offsetMin = UnityEngine.Vector2.zero; closeRt.offsetMax = UnityEngine.Vector2.zero;
            SetupButton(closeBtn, new Color(0.7f, 0.2f, 0.2f, 1f));
            closeBtn.OnClick += () => { try { CloseEmployeeWindow(win); } catch { } };

            // Details view root (fills window below title bar)
            GameObject content = UIFactory.CreateUIObject($"EmpWinContent_{idx}", win);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new UnityEngine.Vector2(0.02f, 0.04f);
            contentRt.anchorMax = new UnityEngine.Vector2(0.98f, 0.90f);
            contentRt.offsetMin = UnityEngine.Vector2.zero; contentRt.offsetMax = UnityEngine.Vector2.zero;
            var contentLe = content.GetComponent<LayoutElement>();
            if (contentLe == null) contentLe = content.AddComponent<LayoutElement>();
            contentLe.ignoreLayout = true;

            // Build and show details view inside the window
            var view = new EmployeeDetailsView();
            view.Build(content); // Fill parent; no ConfigureAsPanelLayout in floating window
            view.Show(citizen, occ);
            try { view.BringToFront(); } catch { }

            // Track window and view
            openEmployeeWindowContainers.Add(win);
            if (!employeeWindowViews.ContainsKey(win)) employeeWindowViews.Add(win, view);

            // Ensure visible
            win.SetActive(true);
            try { Canvas.ForceUpdateCanvases(); } catch { }
        }

        private int GetTopCanvasOrder(GameObject root)
        {
            int maxOrder = 0;
            try
            {
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                if (canvases != null)
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        if (canvases[i] != null && canvases[i].sortingOrder > maxOrder)
                            maxOrder = canvases[i].sortingOrder;
                    }
                }
            }
            catch { }
            if (maxOrder < 0) maxOrder = 20000;
            return maxOrder;
        }

        private void CloseEmployeeWindow(GameObject win)
        {
            if (win == null) return;
            try
            {
                if (employeeWindowViews.ContainsKey(win))
                {
                    var view = employeeWindowViews[win];
                    try { if (view != null) view.Hide(); } catch { }
                    employeeWindowViews.Remove(win);
                }
                openEmployeeWindowContainers.Remove(win);
                UnityEngine.Object.Destroy(win);
            }
            catch { }
        }

        private void CloseAllEmployeeWindows()
        {
            for (int i = openEmployeeWindowContainers.Count - 1; i >= 0; i--)
            {
                var go = openEmployeeWindowContainers[i];
                try { CloseEmployeeWindow(go); } catch { }
            }
            openEmployeeWindowContainers.Clear();
            employeeWindowViews.Clear();
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
        
        // Custom method to display the business purchase notification with a callback for the broadcast
        private void DisplayBusinessPurchaseWithCallback(string businessName)
        {
            try
            {
                // Play the purchase sound
                AudioController.Instance.Play2DSound(AudioControls.Instance.newApartment, null, 1f);
                
                // Set the text directly
                InterfaceControls.Instance.caseSolvedText.text ="New Business";
                
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
                
                // Use UniverseLib's Runtime Coroutine to animate and hide the text, then show the broadcast
                UniverseLib.RuntimeHelper.StartCoroutine(AnimateBusinessPurchaseTextWithBroadcast(businessName));
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error displaying business purchase notification: {ex.Message}\n{ex.StackTrace}");
                
                // If there's an error, still show the broadcast as a fallback
                Lib.GameMessage.Broadcast($"New business purchased: {businessName}", 
                    InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.white);
            }
        }
        
        // Original method kept for compatibility
        private void DisplayBusinessPurchase()
        {
            DisplayBusinessPurchaseWithCallback("Unknown");
        }
        
        // Coroutine to animate the business purchase text and then show the broadcast notification
        private System.Collections.IEnumerator AnimateBusinessPurchaseTextWithBroadcast(string businessName)
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
            
            // Now that the animation is complete, show the broadcast notification
            Lib.GameMessage.Broadcast($"New business purchased: {businessName}", 
                InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.white);
            
            Plugin.Logger.LogInfo($"Showed broadcast notification for {businessName}");
        }
        
        // Original animation method kept for compatibility
        private System.Collections.IEnumerator AnimateBusinessPurchaseText()
        {
            return AnimateBusinessPurchaseTextWithBroadcast("Unknown");
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
                int socialCreditLevel = 0;

                // Find the matching preset and get its base cost
                for (int i = 0; i < companyPresets.CompanyPresetsList.Length; i++)
                {
                    if (presetName == companyPresets.CompanyPresetsList[i].Item1)
                    {
                        socialCreditLevel = companyPresets.CompanyPresetsList[i].Item3;
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
                if (presetName == "IndustrialOffice" || presetName == "MediumOffice" || presetName == "Laboratory" || presetName == "EnforcerBranch")
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
                
                if(GameplayController.Instance.money >= finalCost)
                {
                    if(Plugin.useSocialCreditLevel.Value == true && socialCreditLevel <= GameplayController.Instance.GetCurrentSocialCreditLevel())
                    {
                        BuyBusiness(finalCost, selectedBusiness, selectedBusinessIndex);
                    }
                    else if(Plugin.useSocialCreditLevel.Value == true && socialCreditLevel > GameplayController.Instance.GetCurrentSocialCreditLevel())
                    {
                        BusinessUIManager.Instance.ToggleBusinessUI();
                        Lib.GameMessage.Broadcast($"You don't have enough Social Credit to purchase {selectedBusiness.name}. Required SCL: {socialCreditLevel}", InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.white);
                    }
                    else if(Plugin.useSocialCreditLevel.Value == false)
                    {
                        BuyBusiness(finalCost, selectedBusiness, selectedBusinessIndex);
                    }
                }
                else if (GameplayController.Instance.money < finalCost)
                {
                        BusinessUIManager.Instance.ToggleBusinessUI();
                        Lib.GameMessage.Broadcast($"You don't have enough money to purchase {selectedBusiness.name}. Required Crows: {finalCost}", InterfaceController.GameMessageType.notification, InterfaceControls.Icon.building, Color.white);
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


        
        private void BuyBusiness(int finalCost, NewAddress selectedBusiness, int selectedBusinessIndex)
        {
            GameplayController.Instance.AddMoney(-finalCost, true, $"Purchased business: {selectedBusiness.name}");
            availableBusinesses.RemoveAt(selectedBusinessIndex);
            businessButtons.RemoveAt(selectedBusinessIndex);
            Player.Instance.AddLocationOfAuthorty(selectedBusiness);
            try
            {
                CityData cityData = CityData.Instance;
                var building = selectedBusiness.building;
                string buildingName = building.name;
                string presetName = selectedBusiness.company.preset.name;
                
                if(presetName == "IndustrialOffice" || presetName == "IndustrialPlant" || presetName == "WorkplaceCanteen")
                {
                    foreach (var gameLocation in cityData.gameLocationDirectory)
                    {
                        if (gameLocation != null && gameLocation.building != null && 
                        gameLocation.building.name == buildingName)
                        {
                            if(gameLocation.name.Contains("landing") || gameLocation.name.Contains("lobby"))
                            {
                                // Add all rooms in this building
                                Player.Instance.AddLocationOfAuthorty(gameLocation);
                                Plugin.Logger.LogInfo($"Added location from building: {gameLocation.name}");
                            }
                        }              
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"Error in comprehensive lobby authorization: {ex.Message}");
            }
            
            Player.Instance.apartmentsOwned.Add(selectedBusiness);
            Player.Instance.AddToKeyring(selectedBusiness, false);

            string purchasedBusinessName = selectedBusiness.name?.ToString() ?? "Unknown";

            Plugin.Logger.LogInfo($"Deducted {finalCost} Crows for purchasing {purchasedBusinessName}");

            DisplayBusinessPurchaseWithCallback(purchasedBusinessName);

            string purchaseDate = SessionData.Instance.TimeAndDate(SessionData.Instance.gameTime, true, true, true);

            BusinessManager.Instance.AddOwnedBusiness(selectedBusiness, purchasedBusinessName, finalCost, purchaseDate);

            Plugin.Logger.LogInfo($"Business {purchasedBusinessName} purchased and added to owned apartments");
                    
            BusinessUIManager.Instance.ToggleBusinessUI();

            // Set jobs for all occupants in the business that are citizens
            foreach (Actor actor in selectedBusiness.thisAsAddress.currentOccupants)
            {
                try
                {
                    // Check if the actor is a Citizen (SetJob only works on Citizens)
                    Citizen citizen = actor as Citizen;
                    if (citizen == null)
                    {
                        Plugin.Logger.LogInfo($"Skipping job assignment for non-citizen occupant in {selectedBusiness.name}");
                        continue;
                    }
                    
                    // Create a new Occupation object manually
                    Occupation newJob = new Occupation();
                    
                    // Find an appropriate preset for office work from loaded resources
                    OccupationPreset officePreset = null;
                    OccupationPresets presetsMap = new OccupationPresets();
                    foreach (OccupationPreset preset in Resources.FindObjectsOfTypeAll<OccupationPreset>())
                    {
                        if (preset.work == OccupationPreset.workType.Office)
                        {
                            officePreset = preset;
                            break;
                        }
                        // Also allow match by known preset name mapping
                        for (int m = 0; m < presetsMap.OccupationPresetsMapping.Length; m++)
                        {
                            if (preset.presetName == presetsMap.OccupationPresetsMapping[m].Item1)
                            {
                                officePreset = preset;
                                break;
                            }
                        }
                        if (officePreset != null)
                            break;
                    }
                    
                    if (officePreset == null)
                    {
                        Plugin.Logger.LogWarning($"Could not find Office job preset for {citizen.GetCitizenName()}");
                        continue;
                    }
                    
                    // Set up the job properties
                    newJob.preset = officePreset;
                    newJob.employer = selectedBusiness.thisAsAddress.company;
                    newJob.name = Strings.Get("jobs", officePreset.name, Strings.Casing.asIs, false, false, false, null);
                    newJob.paygrade = 0.5f;
                    newJob.work = OccupationPreset.workType.Office;
                    // Set up work hours (9 AM to 5 PM, Monday to Friday)
                    newJob.startTimeDecimalHour = 7.35f;
                    newJob.endTimeDecialHour = 15.20f;
                    newJob.workHours = newJob.endTimeDecialHour - newJob.startTimeDecimalHour;
                    // Set up work days (Monday to Friday)
                    newJob.workDaysList = new Il2CppSystem.Collections.Generic.List<SessionData.WeekDay>();
                    newJob.workDaysList.Add(SessionData.WeekDay.monday);
                    newJob.workDaysList.Add(SessionData.WeekDay.tuesday);
                    newJob.workDaysList.Add(SessionData.WeekDay.wednesday);
                    newJob.workDaysList.Add(SessionData.WeekDay.thursday);
                    newJob.workDaysList.Add(SessionData.WeekDay.friday);
                    
                    // Set up salary
                    float salaryAmount = 30.0f; // Base salary
                    newJob.salary = salaryAmount;
                    newJob.salaryString = CityControls.Instance.cityCurrency + Mathf.RoundToInt(salaryAmount * 1000f).ToString();
                    
                    // Set the job for the citizen
                    citizen.SetJob(newJob);
                    
                    // Add the job to the city's job directory
                    if (!CityData.Instance.assignedJobsDirectory.Contains(newJob))
                    {
                        CityData.Instance.assignedJobsDirectory.Add(newJob);
                    }
                    
                    Plugin.Logger.LogInfo($"Set job for {citizen.GetCitizenName()} at {selectedBusiness.name}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Error setting job for occupant in {selectedBusiness.name}: {ex.Message}");
                }
            }
            
        }
    }
}