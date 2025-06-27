using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Jotunn.Managers;
using Random = UnityEngine.Random;
using Jotunn;
using Jotunn.Entities;
using Jotunn.GUI;
using ValhalLoot; // Add this for custom extension methods

namespace ValhalLoot
{
    public class ValhalLootUI : MonoBehaviour
    {
        private static ValhalLootUI? _instance;
        public static ValhalLootUI Instance => _instance!;
        
        // Tooltip UI elements
        private GameObject? _tooltipPanel;
        private Text? _tooltipText;
        
        // UI Elements
        private GameObject? _uiPanel;
        private RectTransform? _uiRect;
        private Text? _titleText;
        private Text? _descriptionText;
        private List<ItemSlot> _itemSlots = new List<ItemSlot>();
        
        // Reference to the player's previous input state
        private bool _wasPlayerInputBlocked = false;
        
        // Current UI state
        private bool _isVisible = false;
        private ItemDrop.ItemData? _chestItem = null;
        private List<LootEntry> _currentLootOptions = new List<LootEntry>();
        private List<ItemDrop.ItemData> _currentItems = new List<ItemDrop.ItemData>();
        private string _currentChestPrefabName = string.Empty; // Store the current chest's prefab name
        
        // Dictionary to store loot options per chest instance
        private Dictionary<string, List<ItemDrop.ItemData>> _persistentLootOptions = new Dictionary<string, List<ItemDrop.ItemData>>();
        
        // Initialization state
        private bool _isInitialized = false;
        
        // UI dimensions
        private const int UIWidth = 600;
        private const int UIHeight = 400;
        
        // UI dimensions and styling
        // Note: Using Jotunn's built-in wood panel styling instead of custom background
        
        public void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
        }
        
        public void Initialize()
        {
            // Start a coroutine to wait for GUIManager.CustomGUIFront to be ready
            StartCoroutine(InitializeWhenReady());
            ValhalLootPlugin.ValhalLootLogger.LogInfo("ValhalLoot UI initialization started");
        }
        
        private IEnumerator InitializeWhenReady()
        {
            // Wait until the player is in the world and GUIManager.CustomGUIFront is ready
            yield return new WaitUntil(() => 
                Player.m_localPlayer != null && 
                Jotunn.Managers.GUIManager.Instance != null && 
                Jotunn.Managers.GUIManager.CustomGUIFront != null);
            
            // Now it's safe to create the UI
            CreateUI();
            _isInitialized = true;
            ValhalLootPlugin.ValhalLootLogger.LogInfo("ValhalLoot UI fully initialized");
        }
        
        private void CreateUI()
        {
            try
            {
                // Check if GUIManager is available
                if (Jotunn.Managers.GUIManager.Instance == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Jotunn.Managers.GUIManager.Instance is null. Cannot create UI.");
                    return;
                }
                
                // Check if CustomGUIFront is available
                if (Jotunn.Managers.GUIManager.CustomGUIFront == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Jotunn.Managers.GUIManager.CustomGUIFront is null. Cannot create UI.");
                    return;
                }
                
                // Create main panel using Jotunn's GUIManager
                _uiPanel = Jotunn.Managers.GUIManager.Instance.CreateWoodpanel(
                    parent: Jotunn.Managers.GUIManager.CustomGUIFront.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, 0),
                    width: UIWidth,
                    height: UIHeight,
                    draggable: true);
                
                if (_uiPanel == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Failed to create UI panel.");
                    return;
                }
                    
                _uiPanel.name = "ValhalLootPanel";
                _uiRect = _uiPanel.GetComponent<RectTransform>();
            
                // The wood panel already provides the background, so we don't need to create one
                
                try
                {
                    // Create title using Jotunn's GUIManager - adjusted for 600x400 UI
                    GameObject titleObj = Jotunn.Managers.GUIManager.Instance.CreateText(
                        text: "$valhalloot_ui_title",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1f),
                        anchorMax: new Vector2(0.5f, 1f),
                        position: new Vector2(0, -40), // Moved up from -50 to save vertical space
                        font: Jotunn.Managers.GUIManager.Instance.AveriaSerif,
                        fontSize: 24, // Reduced from 28 to fit better
                        color: Jotunn.Managers.GUIManager.Instance.ValheimOrange,
                        outline: true,
                        outlineColor: Color.black,
                        width: 500, // Reduced from 700 to fit in 600 width
                        height: 40, // Reduced from 50
                        addContentSizeFitter: false);
                    
                    if (titleObj != null)
                    {
                        _titleText = titleObj.GetComponent<Text>();
                        if (_titleText != null)
                        {
                            _titleText.alignment = TextAnchor.MiddleCenter;
                        }
                    }
                    
                    // Create description using Jotunn's GUIManager - adjusted for 600x400 UI
                    GameObject descObj = Jotunn.Managers.GUIManager.Instance.CreateText(
                        text: "$valhalloot_ui_description",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1f),
                        anchorMax: new Vector2(0.5f, 1f),
                        position: new Vector2(0, -80), // Moved up from -100 to save vertical space
                        font: Jotunn.Managers.GUIManager.Instance.AveriaSerif,
                        fontSize: 16, // Reduced from 18 to fit better
                        color: Color.white,
                        outline: true,
                        outlineColor: Color.black,
                        width: 500, // Reduced from 700 to fit in 600 width
                        height: 35, // Reduced from 40
                        addContentSizeFitter: false);
                    
                    if (descObj != null)
                    {
                        _descriptionText = descObj.GetComponent<Text>();
                        if (_descriptionText != null)
                        {
                            _descriptionText.alignment = TextAnchor.MiddleCenter;
                        }
                    }
                    
                    // Create item slots
                    for (int i = 0; i < 3; i++)
                    {
                        if (_uiPanel != null)
                        {
                            CreateItemSlot(i, _uiPanel.transform as RectTransform);
                        }
                    }
                    
                    // Hide the UI initially
                    if (_uiPanel != null)
                    {
                        _uiPanel.SetActive(false);
                    }
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Error creating UI elements: {ex.Message}");
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error creating UI: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private void CreateItemSlot(int index, RectTransform? parent)
        {
            // Create slot container
            GameObject slotObj = new GameObject($"ItemSlot_{index}", typeof(RectTransform));
            if (parent != null)
            {
                slotObj.transform.SetParent(parent, false);
            }
            
            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Position slots horizontally with adjusted spacing for 600x400 UI
            float xPos = (index - 1) * 170f; // Reduced from 220f to fit in 600 width
            slotRect.anchoredPosition = new Vector2(xPos, -20); // Moved down slightly
            slotRect.sizeDelta = new Vector2(160, 240); // Reduced from 200x280
            
            // Create background image
            GameObject slotBg = new GameObject("SlotBackground", typeof(RectTransform));
            slotBg.transform.SetParent(slotRect, false);
            Image slotBgImage = slotBg.AddComponent<Image>();
            slotBgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            RectTransform slotBgRect = slotBg.GetComponent<RectTransform>();
            slotBgRect.anchorMin = Vector2.zero;
            slotBgRect.anchorMax = Vector2.one;
            slotBgRect.sizeDelta = Vector2.zero;
            
            // Create item icon
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(slotRect, false);
            Image iconImage = iconObj.AddComponent<Image>();
            
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.7f);
            iconRect.anchorMax = new Vector2(0.5f, 0.7f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(80, 80); // Reduced from 100x100 for smaller UI
            
            // Create item name
            GameObject nameObj = new GameObject("Name", typeof(RectTransform));
            nameObj.transform.SetParent(slotRect, false);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 16; // Reduced from 18
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.4f);
            nameRect.anchorMax = new Vector2(0.5f, 0.4f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.sizeDelta = new Vector2(150, 50); // Reduced from 180x60
            
            // Create quality stars
            GameObject qualityObj = new GameObject("Quality", typeof(RectTransform));
            qualityObj.transform.SetParent(slotRect, false);
            Text qualityText = qualityObj.AddComponent<Text>();
            qualityText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            qualityText.fontSize = 20; // Reduced from 24
            qualityText.alignment = TextAnchor.MiddleCenter;
            qualityText.color = Color.yellow;
            
            RectTransform qualityRect = qualityObj.GetComponent<RectTransform>();
            qualityRect.anchorMin = new Vector2(0.5f, 0.25f);
            qualityRect.anchorMax = new Vector2(0.5f, 0.25f);
            qualityRect.pivot = new Vector2(0.5f, 0.5f);
            qualityRect.sizeDelta = new Vector2(150, 25); // Reduced from 180x30
            
            // Create select button
            GameObject buttonObj = new GameObject("SelectButton", typeof(RectTransform));
            buttonObj.transform.SetParent(slotRect, false);
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 0.8f);
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            // Add button text
            GameObject buttonTextObj = new GameObject("ButtonText", typeof(RectTransform));
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 16;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            buttonText.text = "$valhalloot_ui_select";
            
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.1f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(80, 30); // Adjusted for new 600x400 UI size
            
            // Store references in a slot object
            int slotIndex = index;
            button.onClick.AddListener(() => OnItemSelected(slotIndex));
            
            // Add event trigger for tooltip handling
            EventTrigger eventTrigger = slotObj.AddComponent<EventTrigger>();
            
            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { OnPointerEnterSlot(slotIndex); });
            eventTrigger.triggers.Add(enterEntry);
            
            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { OnPointerExitSlot(); });
            eventTrigger.triggers.Add(exitEntry);
            
            ItemSlot slot = new ItemSlot
            {
                Container = slotObj,
                Background = slotBgImage,
                Icon = iconImage,
                NameText = nameText,
                QualityText = qualityText,
                SelectButton = button,
                EventTrigger = eventTrigger,
                SlotIndex = slotIndex
            };
            
            _itemSlots.Add(slot);
        }
        
        // Item slot class to store UI references
        private class ItemSlot
        {
            public GameObject Container = null!;
            public Image Background = null!;
            public Image Icon = null!;
            public Text NameText = null!;
            public Text QualityText = null!;
            public Button SelectButton = null!;
            public EventTrigger EventTrigger = null!;
            public int SlotIndex;
        }
        
        // Show the loot selection UI with items determined directly from chest prefab name
        public void ShowLootSelection(ItemDrop.ItemData chestItem, string prefabNameOverride = "")
        {
            if (_isVisible || !_isInitialized)
            {
                return;
            }
            
            _chestItem = chestItem;
            
            // Debug: Log chest details
            string prefabName = prefabNameOverride;
            if (string.IsNullOrEmpty(prefabName))
            {
                // Enhanced chest detection - try multiple methods to get the prefab name
                prefabName = GetChestPrefabName(chestItem);
            }
            string itemName = chestItem?.m_shared?.m_name ?? "unknown";
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] ShowLootSelection called for chest: {prefabName} ({itemName})");
            
            // Use the exact prefab name as the loot table key
            _currentChestPrefabName = prefabName;
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Using chest prefab name directly as loot table key: {_currentChestPrefabName}");
            
            // Generate a unique key for this chest instance
            string chestKey = chestItem != null ? GetChestInstanceKey(chestItem) : "unknown_chest";
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Chest instance key: {chestKey}");
            
            // Debug: Log custom data
            if (chestItem?.m_customData != null)
            {
                string customDataKeys = string.Join(", ", chestItem.m_customData.Keys);
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Chest custom data keys: {customDataKeys}");
            }
            
            // Check if we already have loot options for this chest
            if (_persistentLootOptions.TryGetValue(chestKey, out var savedItems))
            {
                // Use the saved loot options
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Using saved loot options for chest {chestKey}, count: {savedItems.Count}");
                _currentItems = savedItems;
                
                // Debug: Log the saved items
                for (int i = 0; i < savedItems.Count; i++)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Saved item {i+1}: {savedItems[i].m_shared.m_name}");
                }
            }
            else
            {
                // Generate new loot options for this chest
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Generating new loot options for chest {chestKey} with prefab name: {_currentChestPrefabName}");
                GenerateLootOptions(_currentChestPrefabName);
                
                // Save the generated options for future use
                _persistentLootOptions[chestKey] = new List<ItemDrop.ItemData>(_currentItems);
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Saved {_currentItems.Count} new loot options for chest {chestKey}");
            }
            
            // Update UI elements
            UpdateUIElements();
            
            // Show the UI
            if (_uiPanel != null)
            {
                _uiPanel.SetActive(true);
                _isVisible = true;
            }
            
            // Store current player input state and block gameplay input
            if (Player.m_localPlayer != null)
            {
                _wasPlayerInputBlocked = Player.m_localPlayer.InPlaceMode() || Player.m_localPlayer.InCutscene();
                // Block input using Jotunn's GUIManager
                GUIManager.BlockInput(true);
            }
        }
        
        // Get the chest prefab name using multiple fallback methods for resilience after relog
        private string GetChestPrefabName(ItemDrop.ItemData chestItem)
        {
            if (chestItem == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning("[LOOT DEBUG] GetChestPrefabName called with null chestItem");
                return "unknown";
            }
            
            // Method 1: Try to get from m_dropPrefab directly (most reliable when available)
            if (chestItem.m_dropPrefab != null && !string.IsNullOrEmpty(chestItem.m_dropPrefab.name))
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Found chest prefab name from m_dropPrefab: {chestItem.m_dropPrefab.name}");
                return chestItem.m_dropPrefab.name;
            }
            
            // Method 2: Try to get from custom data
            if (chestItem.m_customData != null)
            {
                string prefabName;
                if (chestItem.m_customData.TryGetValue("prefabName", out prefabName) && !string.IsNullOrEmpty(prefabName))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Found chest prefab name from custom data: {prefabName}");
                    return prefabName;
                }
            }
            
            // Method 3: Try to infer from item name
            string itemName = chestItem.m_shared?.m_name ?? "";
            foreach (string biome in new[] { "meadows", "blackforest", "swamp", "mountains", "plains", "mistlands", "ashlands" })
            {
                if (itemName.ToLower().Contains(biome))
                {
                    string inferredName = $"{biome}loot_ru";
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Inferred chest prefab name from item name: {inferredName}");
                    return inferredName;
                }
            }
            
            // Method 4: Try to infer from shared name or description
            string description = chestItem.m_shared?.m_description ?? "";
            foreach (string biome in new[] { "meadows", "blackforest", "swamp", "mountains", "plains", "mistlands", "ashlands" })
            {
                if (description.ToLower().Contains(biome))
                {
                    string inferredName = $"{biome}loot_ru";
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Inferred chest prefab name from description: {inferredName}");
                    return inferredName;
                }
            }
            
            // Last resort: Try to find a matching prefab in the ObjectDB
            foreach (string biome in new[] { "meadowsloot_ru", "blackforestloot_ru", "swamploot_ru", "mountainsloot_ru", "plainsloot_ru", "mistlandsloot_ru", "ashlandsloot_ru" })
            {
                if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(biome) != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Found matching chest prefab in ObjectDB: {biome}");
                    
                    // Store this for future reference
                    if (chestItem.m_customData == null)
                    {
                        chestItem.m_customData = new Dictionary<string, string>();
                    }
                    chestItem.m_customData["prefabName"] = biome;
                    
                    return biome;
                }
            }
            
            // If all else fails, default to meadows (lowest tier)
            ValhalLootPlugin.ValhalLootLogger.LogWarning("[LOOT DEBUG] Could not determine chest prefab name, defaulting to meadowsloot_ru");
            return "meadowsloot_ru";
        }
        
        // Generate a unique key for a chest instance using our custom item data extensions
        private string GetChestInstanceKey(ItemDrop.ItemData chestItem)
        {
            if (chestItem == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning("[CHEST KEY DEBUG] GetChestInstanceKey called with null chestItem");
                return "unknown_chest";
            }
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST KEY DEBUG] Generating key for chest: {chestItem.m_shared?.m_name ?? "unknown"}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST KEY DEBUG] Stack: {chestItem.m_stack}, Quality: {chestItem.m_quality}, Durability: {chestItem.m_durability}");
            
            // Try to get existing UID from custom data
            string customDataKey = "CustomData_ValhalLoot";
            if (chestItem.m_customData != null && chestItem.m_customData.ContainsKey(customDataKey))
            {
                string uid = "";
                if (chestItem.m_customData.TryGetValue("uid", out uid) && 
                    !string.IsNullOrEmpty(uid))
                {
                    return uid;
                }
            }
            
            // Generate a new UID if none exists
            string newUid = Guid.NewGuid().ToString();
            
            // Store the UID in custom data
            if (chestItem.m_customData == null)
            {
                chestItem.m_customData = new Dictionary<string, string>();
            }
            
            // Ensure the custom data key exists
            if (!chestItem.m_customData.ContainsKey(customDataKey))
            {
                chestItem.m_customData[customDataKey] = "{}";
            }
            
            chestItem.m_customData["uid"] = newUid;
            
            return newUid;
        }
        
        // Generate random loot options based on chest prefab name
        private void GenerateLootOptions(string chestPrefabName)
        {
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT GEN DEBUG] Generating loot options for chest prefab: {chestPrefabName}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT GEN DEBUG] ObjectDB initialized: {(ObjectDB.instance != null)}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT GEN DEBUG] ValhalLootManager initialized: {(ValhalLootManager.Instance != null)}");
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT GEN DEBUG] Clearing previous loot options");
            _currentLootOptions.Clear();
            _currentItems.Clear();
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] GenerateLootOptions for chest prefab: {chestPrefabName}");
            
            // Get the loot table for this chest prefab
            if (ValhalLootManager.Instance == null || ValhalLootManager.Instance.Config == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError("Cannot generate loot options: ValhalLootManager or Config is null");
                return;
            }
            
            // Get the loot table directly using the chest prefab name as the key
            List<LootEntry> lootEntries = new List<LootEntry>();
            
            if (ValhalLootManager.Instance.Config.ChestLoot.TryGetValue(chestPrefabName, out var chestLoot))
            {
                lootEntries = chestLoot;
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Found loot table for chest prefab {chestPrefabName} with {lootEntries.Count} entries");
            }
            else
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"[LOOT DEBUG] No loot table found for chest prefab {chestPrefabName}");
                return;
            }
            
            const int optionsCount = 3;
            
            // The YAML file already contains only armor and weapons, so no filtering needed
            // Select random items from the biome loot table based on weights
            for (int i = 0; i < optionsCount; i++)
            {
                // Select a random item based on weights
                LootEntry? selectedEntry = SelectRandomWeightedEntry(lootEntries);
                if (selectedEntry != null)
                {
                    _currentLootOptions.Add(selectedEntry);
                    
                    // Create the item
                    ItemDrop.ItemData? item = CreateItemFromLootEntry(selectedEntry);
                    if (item != null)
                    {
                        _currentItems.Add(item);
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Added loot option {i+1}: {selectedEntry.ItemName} (Quality: {item.m_quality})");
                    }
                    else
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogWarning($"[LOOT DEBUG] Failed to create item for loot entry: {selectedEntry.ItemName}");
                        // Try again with another random entry
                        i--;
                    }
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"[LOOT DEBUG] Failed to select a loot entry for option {i+1}");
                    // Try again with another random entry if possible
                    if (lootEntries.Count > 0)
                    {
                        i--;
                    }
                }
            }
            
            // Ensure we have exactly 3 items
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[LOOT DEBUG] Generated {_currentItems.Count} loot options for chest prefab {chestPrefabName}");
            
            // If we somehow have less than 3 items, add duplicates from the existing items
            while (_currentItems.Count < optionsCount && _currentItems.Count > 0)
            {
                // Clone an existing item
                ItemDrop.ItemData existingItem = _currentItems[0];
                ItemDrop.ItemData duplicateItem = existingItem.Clone();
                _currentItems.Add(duplicateItem);
                ValhalLootPlugin.ValhalLootLogger.LogWarning($"[LOOT DEBUG] Added duplicate item to ensure 3 options: {existingItem.m_shared.m_name}");
            }
        }
        
        // Select a random entry from the loot table based on weights
        private LootEntry? SelectRandomWeightedEntry(List<LootEntry> lootTable)
        {
            if (lootTable == null || lootTable.Count == 0)
            {
                return null;
            }
            
            // Calculate total weight
            float totalWeight = lootTable.Sum(entry => entry.Weight);
            
            // Select a random point within the total weight
            float randomPoint = Random.Range(0f, totalWeight);
            
            // Find the entry that contains the random point
            float currentWeight = 0f;
            foreach (var entry in lootTable)
            {
                currentWeight += entry.Weight;
                if (randomPoint <= currentWeight)
                {
                    return entry;
                }
            }
            
            // Fallback to the first entry
            return lootTable[0];
        }
        
        // Create an item from a loot entry
        private ItemDrop.ItemData? CreateItemFromLootEntry(LootEntry entry)
        {
            try
            {
                // Skip if entry is invalid
                if (entry == null || string.IsNullOrEmpty(entry.ItemName))
                {
                    return null;
                }
                
                // Find the item prefab
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(entry.ItemName);
                if (itemPrefab == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"Item prefab not found for {entry.ItemName}");
                    return null;
                }
                
                // Create the item
                ItemDrop component = itemPrefab.GetComponent<ItemDrop>();
                if (component == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"ItemDrop component not found on {entry.ItemName}");
                    return null;
                }
                
                // Clone the item data
                ItemDrop.ItemData itemData = component.m_itemData.Clone();
                
                // Set the quality
                if (entry.MaxQuality > 1)
                {
                    itemData.m_quality = Random.Range(entry.MinQuality, entry.MaxQuality + 1);
                }
                
                
                return itemData;
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error creating item from loot entry: {ex.Message}");
                return null;
            }
        }
        
        // Update UI elements with current items
        private void UpdateUIElements()
        {
            // Update title and description with localized text
            if (_titleText != null)
            {
                _titleText.text = Localization.instance.Localize("$valhalloot_ui_title");
            }
            
            if (_descriptionText != null)
            {
                _descriptionText.text = Localization.instance.Localize("$valhalloot_ui_description");
            }
            
            // Update item slots
            for (int i = 0; i < _itemSlots.Count; i++)
            {
                if (i < _currentItems.Count)
                {
                    // Show the slot
                    _itemSlots[i].Container.SetActive(true);
                    
                    // Update the slot with item data
                    UpdateItemSlot(_itemSlots[i], _currentItems[i]);
                }
                else
                {
                    // Hide unused slots
                    _itemSlots[i].Container.SetActive(false);
                }
            }
        }
        
        // Update a single item slot with item data
        private void UpdateItemSlot(ItemSlot slot, ItemDrop.ItemData item)
        {
            // Set the item icon
            slot.Icon.sprite = item.GetIcon();
            
            // Set the item name
            slot.NameText.text = Localization.instance.Localize(item.m_shared.m_name);
            
            // Set the quality stars or stack size for coins
            if (item.m_shared.m_name.Contains("Coins"))
            {
                slot.QualityText.text = item.m_stack.ToString();
            }
            else if (item.m_quality > 1)
            {
                string stars = new string('★', item.m_quality - 1);
                slot.QualityText.text = stars;
            }
            else
            {
                slot.QualityText.text = "";
            }
            
            // Update button text
            Transform buttonTextTransform = slot.SelectButton.transform.Find("ButtonText");
            if (buttonTextTransform != null)
            {
                Text buttonText = buttonTextTransform.GetComponent<Text>();
                if (buttonText != null)
                {
                    buttonText.text = Localization.instance.Localize("$valhalloot_ui_select");
                }
            }
        }
        // Handle item selection
        private void OnItemSelected(int index)
        {
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] OnItemSelected called with index: {index}");
            
            if (index < 0 || index >= _currentItems.Count)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning($"[DEBUG] Invalid item index: {index}, _currentItems.Count: {_currentItems.Count}");
                return;
            }
            
            try
            {
                // Get the selected item
                ItemDrop.ItemData selectedItem = _currentItems[index];
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Selected item: {selectedItem.m_shared.m_name}, Quality: {selectedItem.m_quality}");
                
                // Get the prefab name from the item
                string prefabName = selectedItem.m_shared.m_name;
                if (prefabName.StartsWith("$"))
                {
                    prefabName = prefabName.Substring(1); // Remove the $ prefix
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Removed $ prefix, prefabName: {prefabName}");
                }
                
                // Try to get the actual prefab name from custom data first
                string actualPrefabName = "";
                if (selectedItem.m_customData != null && selectedItem.m_customData.TryGetValue("PrefabName", out string? storedPrefabName) && !string.IsNullOrEmpty(storedPrefabName))
                {
                    actualPrefabName = storedPrefabName;
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Using stored prefab name from custom data: {actualPrefabName}");
                }
                else
                {
                    // If no stored prefab name, use the internal name from shared data
                    actualPrefabName = selectedItem.m_shared.m_name;
                    
                    // Remove the localization prefix if present
                    if (actualPrefabName.StartsWith("$"))
                    {
                        actualPrefabName = actualPrefabName.Substring(1);
                    }
                    
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Using internal name from shared data: {actualPrefabName}");
                }
                
                // Initialize the prefab lookup
                GameObject? finalItemPrefab = null;
                
                // STEP 1: Try direct lookup with the exact name from the loot entry
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Attempting direct ObjectDB lookup with name: {actualPrefabName}");
                finalItemPrefab = ObjectDB.instance.GetItemPrefab(actualPrefabName);
                
                if (finalItemPrefab != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Found prefab via direct ObjectDB lookup: {finalItemPrefab.name}");
                }
                else
                {
                    // STEP 2: Try with Valheim's actual naming conventions
                    // Valheim uses the pattern: <item_type>_<material> for most items
                    // Example: ShieldBronzeBuckler for a bronze shield
                    
                    // First, check if the name follows the item_<type>_<material> pattern
                    string itemName = actualPrefabName;
                    if (itemName.StartsWith("item_"))
                    {
                        itemName = itemName.Substring(5); // Remove "item_" prefix
                    }
                    if (itemName.StartsWith("$"))
                    {
                        itemName = itemName.Substring(1); // Remove "$" prefix
                    }
                    
                    // Split by underscore to get type and material
                    string[] parts = itemName.Split('_');
                    if (parts.Length >= 2)
                    {
                        string itemType = parts[0]; // e.g., "shield"
                        string material = parts[1]; // e.g., "bronze"
                        
                        // Try common Valheim naming patterns
                        string[] valheimPatterns = new string[] {
                            // CamelCase with material first: BronzeShield
                            $"{char.ToUpper(material[0])}{material.Substring(1)}{char.ToUpper(itemType[0])}{itemType.Substring(1)}",
                            // CamelCase with type first: ShieldBronze
                            $"{char.ToUpper(itemType[0])}{itemType.Substring(1)}{char.ToUpper(material[0])}{material.Substring(1)}",
                            // CamelCase with type first and additional descriptor: ShieldBronzeBuckler
                            $"{char.ToUpper(itemType[0])}{itemType.Substring(1)}{char.ToUpper(material[0])}{material.Substring(1)}Buckler",
                            // Original name
                            actualPrefabName
                        };
                        
                        foreach (string pattern in valheimPatterns)
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Trying Valheim pattern: {pattern}");
                            GameObject? patternPrefab = ObjectDB.instance.GetItemPrefab(pattern);
                            
                            if (patternPrefab != null)
                            {
                                finalItemPrefab = patternPrefab;
                                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Found prefab with Valheim pattern: {pattern}");
                                break;
                            }
                        }
                    }
                    
                    // STEP 3: If still not found, try a more exhaustive search through ObjectDB
                    if (finalItemPrefab == null && ObjectDB.instance != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Pattern matching failed, searching all items in ObjectDB");
                        
                        // Extract key terms from the item name for fuzzy matching
                        string searchTerm = itemName.ToLower();
                        
                        // Search through all items in the ObjectDB
                        foreach (GameObject itemPrefab in ObjectDB.instance.m_items)
                        {
                            if (itemPrefab != null)
                            {
                                string itemPrefabName = itemPrefab.name.ToLower();
                                
                                // Check if the prefab name contains our search terms
                                if (itemPrefabName.Contains(searchTerm) || 
                                    (parts.Length >= 2 && itemPrefabName.Contains(parts[0].ToLower()) && itemPrefabName.Contains(parts[1].ToLower())))
                                {
                                    finalItemPrefab = itemPrefab;
                                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Found prefab via ObjectDB search: {itemPrefab.name}");
                                    break;
                                }
                            }
                        }
                    }
                    
                    // STEP 4: If still not found, try Jotunn's ItemManager for modded items
                    if (finalItemPrefab == null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] ObjectDB search failed, trying Jotunn ItemManager");
                        
                        CustomItem? jotunnItem = Jotunn.Managers.ItemManager.Instance.GetItem(actualPrefabName);
                        
                        if (jotunnItem != null && jotunnItem.ItemPrefab != null)
                        {
                            finalItemPrefab = jotunnItem.ItemPrefab;
                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Found prefab via Jotunn ItemManager: {finalItemPrefab.name}");
                        }
                    }
                }
                
                if (finalItemPrefab == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"[DEBUG] CRITICAL ERROR: Failed to find prefab: {actualPrefabName}");
                    return;
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Successfully found final prefab: {finalItemPrefab.name}");
                }
                
                // Get the ItemDrop component
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Getting ItemDrop component from prefab");
                ItemDrop? component = finalItemPrefab.GetComponent<ItemDrop>();
                if (component == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"[DEBUG] CRITICAL ERROR: Prefab {actualPrefabName} does not have an ItemDrop component");
                    return;
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Successfully got ItemDrop component");
                }
                
                // Create a new item data instance
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Creating new item data instance by cloning");
                ItemDrop.ItemData newItem = component.m_itemData.Clone();
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Clone created successfully: {newItem.m_shared.m_name}");
                
                // CRITICAL: Set the m_dropPrefab reference for visual equipment
                newItem.m_dropPrefab = finalItemPrefab;
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Set m_dropPrefab reference: {newItem.m_dropPrefab?.name ?? "null"}");
                
                // Copy important properties from the selected item
                newItem.m_quality = selectedItem.m_quality;
                newItem.m_stack = selectedItem.m_stack;
                newItem.m_durability = selectedItem.m_durability;
                
                // Ensure custom data dictionary exists
                if (newItem.m_customData == null)
                {
                    newItem.m_customData = new Dictionary<string, string>();
                }
                
                // Ensure the custom data key exists
                string customDataKey = "CustomData_ValhalLoot";
                if (!newItem.m_customData.ContainsKey(customDataKey))
                {
                    newItem.m_customData[customDataKey] = "{}";
                }
                
                // Store the prefab name and a unique ID for persistence
                newItem.m_customData["PrefabName"] = finalItemPrefab.name;
                newItem.m_customData["uid"] = Guid.NewGuid().ToString();
                
                // IMPORTANT: Remove ValhalLoot-specific data to prevent the item from being used as a chest again
                // This ensures the item is just a regular item without chest functionality
                if (newItem.m_customData.ContainsKey("CustomData_ValhalLoot"))
                {
                    newItem.m_customData.Remove("CustomData_ValhalLoot");
                }
                
                // Add a flag to mark this as a claimed item (not a chest)
                newItem.m_customData["ValhalLoot_Claimed"] = "true";
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Removed ValhalLoot chest data from claimed item");
                
                // Add the item to player's inventory
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Attempting to add item to player inventory: {newItem.m_shared.m_name}");
                bool inventoryAddResult = Player.m_localPlayer.GetInventory().AddItem(newItem);
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] AddItem result: {inventoryAddResult}");
                
                if (inventoryAddResult)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] SUCCESS: Added {newItem.m_shared.m_name} (quality: {newItem.m_quality}) to inventory");
                    
                    // Play pickup sound
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"$item_found: {newItem.m_shared.m_name}");
                    

                    
                    // Remove the chest item from inventory
                    if (_chestItem != null)
                    {
                        var inventory = Player.m_localPlayer?.GetInventory();
                        if (inventory != null)
                        {
                            inventory.RemoveItem(_chestItem, 1);
                        }
                    }
                    
                    // Remove this chest's loot options from the persistent dictionary
                    if (_chestItem != null)
                    {
                        string chestKey = GetChestInstanceKey(_chestItem);
                        if (_persistentLootOptions.ContainsKey(chestKey))
                        {
                            _persistentLootOptions.Remove(chestKey);
                        }
                    }
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"[DEBUG] FAILED to add {newItem.m_shared.m_name} to inventory - inventory might be full");
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Player inventory space: {Player.m_localPlayer.GetInventory().GetEmptySlots()} empty slots");
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$valhalloot_inventory_full");
                }
                
                // Close the UI
                CloseUI();
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"[DEBUG] EXCEPTION in OnItemSelected: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                
                // Additional debug info in case of exception
                if (Player.m_localPlayer != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[DEBUG] Player exists, inventory has {Player.m_localPlayer.GetInventory()?.GetEmptySlots() ?? -1} empty slots");
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("[DEBUG] Player.m_localPlayer is null!");
                }
            }
        }    
        
        // Close the UI
        private void CloseUI()
        {
            if (!_isVisible)
            {
                return;
            }
            
            // Hide the UI
            if (_uiPanel != null)
            {
                _uiPanel.SetActive(false);
            }
            _isVisible = false;
            
            // Hide tooltip when UI is closed
            HideTooltip();
            
            // Only clear the loot options, keep the items for persistence
            // This ensures the UI won't be blank when reopened
            _currentLootOptions.Clear();
            // Do NOT clear _currentItems here to preserve them for the next open
            // _currentItems.Clear(); - removed to fix UI blanking issue
            
            // Reset chest item reference to ensure clean state for next open
            _chestItem = null;
            
            // Restore player input state - only use one method to unblock input
            // to avoid inconsistent state
            if (Player.m_localPlayer != null)
            {
                // Unblock input using Jotunn's GUIManager
                Jotunn.Managers.GUIManager.BlockInput(false);
            }
        }
        
        // Handle pointer enter on item slot for tooltip
        private void OnPointerEnterSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _currentItems.Count)
            {
                return;
            }
            
            // Show tooltip for the item
            ItemDrop.ItemData item = _currentItems[slotIndex];
            ShowTooltip(item);
        }
        
        // Handle pointer exit from item slot
        private void OnPointerExitSlot()
        {
            // Hide tooltip
            HideTooltip();
        }
        
        // Show tooltip for an item
        private void ShowTooltip(ItemDrop.ItemData item)
        {
            // Use the vanilla game's tooltip system
            if (InventoryGui.instance != null)
            {
                // Position the tooltip to the right of the UI panel to avoid buttons
                Vector3 tooltipPosition = _uiPanel != null ? 
                    _uiPanel.transform.position + new Vector3(_uiPanel.GetComponent<RectTransform>().rect.width / 2 + 200, 0, 0) : 
                    Input.mousePosition + new Vector3(150, 0, 0);
                
                // Show the tooltip using a custom method since vanilla ShowItemTooltip isn't available
                ShowCustomItemTooltip(item, tooltipPosition);
            }
        }
        
        // Custom tooltip method to replace the missing ShowItemTooltip functionality
        private void ShowCustomItemTooltip(ItemDrop.ItemData item, Vector3 position)
        {
            // Create tooltip if it doesn't exist
            if (_tooltipPanel == null)
            {
                CreateTooltipPanel();
            }
            
            if (_tooltipPanel != null)
            {
                // Position the tooltip
                _tooltipPanel.transform.position = position;
                
                // Update tooltip content
                if (_tooltipText != null)
                {
                    // Get tooltip text and ensure it's properly localized
                    string tooltipText = item.GetTooltip();
                    
                    // Replace any remaining localization keys in the tooltip
                    if (Localization.instance != null)
                    {
                        tooltipText = Localization.instance.Localize(tooltipText);
                    }
                    
                    _tooltipText.text = tooltipText;
                }
                
                // Show the tooltip
                _tooltipPanel.SetActive(true);
            }
        }
        
        // Hide tooltip
        private void HideTooltip()
        {
            if (_tooltipPanel != null)
            {
                _tooltipPanel.SetActive(false);
            }
        }
        
        // Create tooltip panel
        private void CreateTooltipPanel()
        {
            // Create tooltip panel using Jotunn's GUIManager
            _tooltipPanel = Jotunn.Managers.GUIManager.Instance.CreateWoodpanel(
                parent: Jotunn.Managers.GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0, 0),
                anchorMax: new Vector2(0, 0),
                position: new Vector2(20, 20),
                width: 300,
                height: 200,
                draggable: false);
                
            _tooltipPanel.name = "ValhalLootTooltipPanel";
            
            // Add tooltip text
            GameObject textObj = Jotunn.Managers.GUIManager.Instance.CreateText(
                text: "",
                parent: _tooltipPanel.transform,
                anchorMin: new Vector2(0, 0),
                anchorMax: new Vector2(1, 1),
                position: new Vector2(0, 0),
                font: Jotunn.Managers.GUIManager.Instance.AveriaSerif,
                fontSize: 14,
                color: Color.white,
                outline: true,
                outlineColor: Color.black,
                width: 280,
                height: 180,
                addContentSizeFitter: false);
                
            _tooltipText = textObj.GetComponent<Text>();
            _tooltipText.alignment = TextAnchor.UpperLeft;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
            
            // Hide initially
            _tooltipPanel.SetActive(false);
        }
        
        // Update method to handle input
        private void Update()
        {
            if (_isVisible)
            {
                // Close UI on escape key
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseUI();
                }
            }
        }
    }
}
