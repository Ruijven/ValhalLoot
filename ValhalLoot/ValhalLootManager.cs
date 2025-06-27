using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Entities;
using BepInEx.Configuration;
using UnityEngine.UI;
using TMPro;
using Jotunn.GUI;
using Random = UnityEngine.Random;
using ValhalLoot; // Add this for custom extension methods

namespace ValhalLoot
{
    public class ValhalLootManager : MonoBehaviour
    {
        private static ValhalLootManager? _instance;
        public static ValhalLootManager Instance => _instance!;
        
        // Configuration reference
        private ValhalLootConfig? _config = null;
        public ValhalLootConfig Config { get { return _config!; } set { _config = value; } }
        
        // Initialization flag
        private bool _isInitialized = false;
        
        // Dictionary mapping biome names to their specific chest prefabs
        private Dictionary<string, GameObject> _biomePrefabs = new Dictionary<string, GameObject>();
        
        // Removed legacy chest prefab reference as it's no longer needed
        
        // Dictionary mapping biome names to prefab names
        public Dictionary<string, string> _biomeToPrefabMap = new Dictionary<string, string>()
        {
            { "Meadows", "meadowsloot_ru" },
            { "BlackForest", "blackforestloot_ru" },
            { "Swamp", "swamploot_ru" },
            { "Mountain", "mountainsloot_ru" },
            { "Plains", "plainsloot_ru" },
            { "Mistlands", "mistlandsloot_ru" },
            { "Ashlands", "ashlandsloot_ru" },
            
        };
        
        // Direct mapping from chest prefab names to biome names
        private readonly Dictionary<string, string> _chestPrefabToBiome = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "meadowsloot_ru", "Meadows" },
            { "blackforestloot_ru", "BlackForest" },
            { "swamploot_ru", "Swamp" },
            { "mountainsloot_ru", "Mountain" },
            { "plainsloot_ru", "Plains" },
            { "mistlandsloot_ru", "Mistlands" },
            { "ashlandsloot_ru", "Ashlands" }
        };
        
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
            
            // Subscribe to Jotunn's PrefabManager.OnVanillaPrefabsAvailable event for proper prefab registration timing
            Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable += RegisterChestItems;
            
            // Register for item use detection via Harmony patch in Plugin.cs
        }
        
        // Process a chest item being used by a player
        public void ProcessChestUse(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning("ProcessChestUse called with null player or item");
                return;
            }
            
            // Enhanced logging to diagnose item state persistence issues
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"Player {player.GetPlayerName()} used ValhalLoot chest");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Item name: {item.m_shared?.m_name ?? "null"}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Item prefab: {item.m_dropPrefab?.name ?? "null"}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Has custom data: {(item.m_customData != null && item.m_customData.Count > 0)}");
            
            if (item.m_customData != null)
            {
                foreach (var key in item.m_customData.Keys)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Custom data key: {key}");
                }
            }
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Icon count: {item.m_shared?.m_icons?.Length ?? 0}");
            if (item.m_shared?.m_icons != null && item.m_shared.m_icons.Length > 0)
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Icon name: {item.m_shared.m_icons[0]?.name ?? "null"}");
            }
            
            // Verify this is actually a loot chest using multiple detection methods
            bool isLootChest = false;
            string biome = "";
            
            // Method 1: Try to get ValhalLoot custom data directly (most reliable when present)
            string customDataKey = "CustomData_ValhalLoot";
            if (item.m_customData != null && item.m_customData.ContainsKey(customDataKey))
            {
                isLootChest = true;
                biome = item.m_customData[customDataKey];
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Detected as loot chest via custom data: {biome}");
            }
            // Method 2: Check item name (reliable across sessions)
            else if (item.m_shared?.m_name != null)
            {
                string itemName = item.m_shared.m_name.ToLower();
                if (itemName.Contains("valhalloot") || 
                    itemName.Contains("loot_ru") ||
                    itemName.Contains("loot chest"))
                {
                    isLootChest = true;
                    
                    // Try to determine biome from name
                    if (itemName.Contains("meadows")) biome = "Meadows";
                    else if (itemName.Contains("blackforest")) biome = "BlackForest";
                    else if (itemName.Contains("swamp")) biome = "Swamp";
                    else if (itemName.Contains("mountain")) biome = "Mountain";
                    else if (itemName.Contains("plains")) biome = "Plains";
                    else if (itemName.Contains("mistland")) biome = "Mistlands";
                    else if (itemName.Contains("ashland")) biome = "Ashlands";
                    else biome = "Meadows"; // Default fallback
                    
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Detected as loot chest via name: {biome}");
                }
            }
            // Method 3: Check item description (reliable across sessions)
            else if (item.m_shared?.m_description != null &&
                    (item.m_shared.m_description.ToLower().Contains("valhalloot") ||
                     item.m_shared.m_description.ToLower().Contains("loot chest")))
            {
                isLootChest = true;
                biome = "Meadows"; // Default fallback
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Detected as loot chest via description: {biome}");
            }
            // Method 4: Check icon name if available
            else if (item.m_shared?.m_icons != null && 
                     item.m_shared.m_icons.Length > 0 && 
                     item.m_shared.m_icons[0] != null &&
                     item.m_shared.m_icons[0].name != null &&
                     item.m_shared.m_icons[0].name.ToLower().Contains("chest"))
            {
                isLootChest = true;
                biome = "Meadows"; // Default fallback
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Detected as loot chest via icon: {biome}");
            }
            
            if (!isLootChest)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning($"[CHEST DEBUG] Item {item.m_shared?.m_name ?? "unknown"} not detected as a loot chest");
                return;
            }
            
            // Show the loot UI with the detected biome
            if (ValhalLootUI.Instance != null)
            {
                // Store biome in custom data for UI to use
                if (item.m_customData == null)
                {
                    item.m_customData = new Dictionary<string, string>();
                }
                
                if (!string.IsNullOrEmpty(biome))
                {
                    item.m_customData[customDataKey] = biome;
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Showing loot UI for biome: {biome}");
                ValhalLootUI.Instance.ShowLootSelection(item);
                
                // Remove the chest item from inventory after use
                player.GetInventory().RemoveItem(item, 1);
            }
        }
        
        // Get the prefab name for a chest item (renamed from DetermineChestBiome for clarity)
        public string GetChestPrefabName(ItemDrop.ItemData chestItem)
        {
            if (chestItem == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning("Cannot get prefab name for null chest item");
                return "meadowsloot_ru"; // Default fallback
            }
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Getting prefab name for chest item with details:");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Item name: {chestItem.m_shared?.m_name ?? "null"}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Item ID: {chestItem.m_shared?.m_name ?? "null"}");
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Has custom data: {(chestItem.m_customData != null && chestItem.m_customData.Count > 0)}");
            
            // Try multiple methods to determine the prefab name
            
            // Method 1: Get the prefab name from the chest item's m_dropPrefab
            string prefabName = chestItem.m_dropPrefab?.name ?? "";
            if (!string.IsNullOrEmpty(prefabName))
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[CHEST DEBUG] Found prefab name from m_dropPrefab: {prefabName}");
                return prefabName;
            }
            
            // Method 2: Try to extract from item name if it contains a biome identifier
            if (chestItem.m_shared?.m_name != null)
            {
                string itemName = chestItem.m_shared.m_name.ToLower();
                
                if (itemName.Contains("meadows"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: meadowsloot_ru");
                    return "meadowsloot_ru";
                }
                else if (itemName.Contains("blackforest"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: blackforestloot_ru");
                    return "blackforestloot_ru";
                }
                else if (itemName.Contains("swamp"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: swamploot_ru");
                    return "swamploot_ru";
                }
                else if (itemName.Contains("mountain"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: mountainsloot_ru");
                    return "mountainsloot_ru";
                }
                else if (itemName.Contains("plains"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: plainsloot_ru");
                    return "plainsloot_ru";
                }
                else if (itemName.Contains("mistland"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: mistlandsloot_ru");
                    return "mistlandsloot_ru";
                }
                else if (itemName.Contains("ashland"))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("[CHEST DEBUG] Determined prefab from item name: ashlandsloot_ru");
                    return "ashlandsloot_ru";
                }
            }
            
            // Fallback to default
            ValhalLootPlugin.ValhalLootLogger.LogWarning("Could not determine chest prefab name, using default fallback");
            return "meadowsloot_ru"; // Default fallback
        }
        
        
        
        public void Initialize(ValhalLootConfig config)
        {
            Config = config;
            
            // Start a coroutine to wait for the player to be in-world before initializing
            StartCoroutine(InitializeWhenReady());
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo("ValhalLoot Manager initialization started");
        }
        
        private IEnumerator InitializeWhenReady()
        {
            ValhalLootPlugin.ValhalLootLogger.LogInfo("Waiting for game systems to be ready before initializing ValhalLoot...");
            
            // Wait until the player is in the world and ObjectDB is ready
            yield return new WaitUntil(() => Player.m_localPlayer != null && ObjectDB.instance != null && ZNetScene.instance != null);
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo("Game systems ready");
            
            // Note: Chest registration is now handled by the PrefabManager.OnVanillaPrefabsAvailable event
            // This ensures proper timing with Jotunn's systems
            
            // Add localization for messages if not already added
            if (Localization.instance != null && !Localization.instance.m_translations.ContainsKey("valhalloot_received"))
            {
                Localization.instance.AddWord("valhalloot_received", "You received a ValhalLoot chest!");
                Localization.instance.AddWord("valhalloot_dropped", "A ValhalLoot chest was dropped nearby (inventory full)");
            }
            
            _isInitialized = true;
            ValhalLootPlugin.ValhalLootLogger.LogInfo("ValhalLoot Manager fully initialized and ready");
        }
        
        private void RegisterChestItems()
        {
            try
            {
                // Get the asset bundle for the chest prefab
                AssetBundle? assetBundle = null;
                
                // DEBUG: Log all asset bundles found
                AssetBundle[] allBundles = Resources.FindObjectsOfTypeAll<AssetBundle>();
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"DEBUG: Found {allBundles.Length} asset bundles in total");
                foreach (var bundle in allBundles)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"DEBUG: Asset bundle: {bundle.name}");
                }
                
                // Try to load from embedded resources first (preferred method)
                assetBundle = Resources.FindObjectsOfTypeAll<AssetBundle>().FirstOrDefault(a => a.name == "lootbundle_ru");
                
                if (assetBundle == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Failed to load lootbundle_ru asset bundle");
                    return;
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"DEBUG: Successfully loaded asset bundle: {assetBundle.name}");
                
                // Load the icon sprite once to share across all prefabs
                ValhalLootPlugin.ValhalLootLogger.LogInfo("Loading chest icon sprite");
                Sprite iconSprite = assetBundle.LoadAsset<Sprite>("valhalloot_ru_icon");
                
                if (iconSprite == null)
                {
                    // If we can't find a specific icon, try to load any sprite as a fallback
                    ValhalLootPlugin.ValhalLootLogger.LogWarning("Could not find valhalloot_ru_icon, looking for any sprite in the bundle");
                    Sprite[] allSprites = assetBundle.LoadAllAssets<Sprite>();
                    if (allSprites != null && allSprites.Length > 0)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Found {allSprites.Length} sprites in the bundle, using the first one");
                        iconSprite = allSprites[0];
                    }
                    else
                    {
                        // Last resort: create a simple 1x1 white texture as a placeholder
                        ValhalLootPlugin.ValhalLootLogger.LogWarning("No sprites found in bundle, creating placeholder sprite");
                        Texture2D placeholderTex = new Texture2D(1, 1);
                        placeholderTex.SetPixel(0, 0, UnityEngine.Color.white);
                        placeholderTex.Apply();
                        iconSprite = Sprite.Create(placeholderTex, new UnityEngine.Rect(0, 0, 1, 1), new UnityEngine.Vector2(0.5f, 0.5f));
                    }
                }
                
                // Define prefab names for each biome chest
                string[] chestPrefabNames = new string[] {
                    "meadowsloot_ru",
                    "blackforestloot_ru",
                    "swamploot_ru",
                    "mountainsloot_ru",
                    "plainsloot_ru",
                    "mistlandsloot_ru",
                    "ashlandsloot_ru"
                };
                
                // Dictionary to store loaded prefabs for verification
                Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();
                
                // Load and register each chest prefab with both systems
                foreach (string prefabName in chestPrefabNames)
                {
                    // Build the full asset path for the prefab
                    string assetPath = $"assets/ruijven/valhalloot/{prefabName}.prefab";
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Loading prefab directly from asset bundle: {assetPath}");
                    
                    // Load the prefab directly from the asset bundle
                    GameObject customPrefab = assetBundle.LoadAsset<GameObject>(assetPath);
                    
                    if (customPrefab == null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogError($"Failed to load prefab {assetPath} from asset bundle");
                        continue;
                    }
                    
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Successfully loaded prefab for {prefabName}");
                    
                    // Store for verification
                    loadedPrefabs[prefabName] = customPrefab;
                    
                    // 1. Register with custom ItemManager (primary registration)
                    try
                    {
                        // Ensure ItemDrop component exists
                        EnsureItemDropComponent(customPrefab);
                        
                        // Register with custom ItemManager
                        new ItemManager.Item(customPrefab);
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Registered {prefabName} with custom ItemManager");
                    }
                    catch (Exception ex)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogError($"Error registering with custom ItemManager: {ex.Message}");
                    }
                    
                    // 2. Also register with Jotunn (for localization and other features)
                    try
                    {
                        CreateJotunnCustomItem(customPrefab, iconSprite, prefabName);
                    }
                    catch (Exception ex)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogError($"Error registering with Jotunn: {ex.Message}");
                    }
                }
                
                // Add localization using Jotunn's LocalizationManager
                ValhalLootPlugin.ValhalLootLogger.LogInfo("Adding localization for chest items and UI elements");
                
                // Add English translations for UI elements (default)
                var translations = new Dictionary<string, string>
                {
                    // UI elements
                    { "valhalloot_ui_title", "ValhalLoot Treasure" },
                    { "valhalloot_ui_description", "Select your reward" },
                    { "valhalloot_ui_select", "Claim" },
                    { "valhalloot_ui_close", "Close" },
                    
                    // Drop confirmation dialog
                    { "valhalloot_drop_title", "Drop Loot Chest?" },
                    { "valhalloot_drop_message", "If you throw this Loot Chest away, you will not be able to reclaim it." },
                    { "valhalloot_drop_confirm", "Okay" },
                    { "valhalloot_drop_cancel", "Cancel" },
                    
                    // Inventory messages
                    { "valhalloot_inventory_full", "Inventory full! Cannot receive loot chest." },
                    { "valhalloot_received", "Received a loot chest!" },
                    
                    // Biome-specific chests
                    { "item_meadowsloot_ru", "Meadows Treasure Chest" },
                    { "item_meadowsloot_ru_description", "A chest containing treasures from the Meadows. Right-click to open." },
                    
                    { "item_blackforestloot_ru", "Black Forest Treasure Chest" },
                    { "item_blackforestloot_ru_description", "A chest containing treasures from the Black Forest. Right-click to open." },
                    
                    { "item_swamploot_ru", "Swamp Treasure Chest" },
                    { "item_swamploot_ru_description", "A chest containing treasures from the Swamp. Right-click to open." },
                    
                    { "item_mountainsloot_ru", "Mountain Treasure Chest" },
                    { "item_mountainsloot_ru_description", "A chest containing treasures from the Mountains. Right-click to open." },
                    
                    { "item_plainsloot_ru", "Plains Treasure Chest" },
                    { "item_plainsloot_ru_description", "A chest containing treasures from the Plains. Right-click to open." },
                    
                    { "item_mistlandsloot_ru", "Mistlands Treasure Chest" },
                    { "item_mistlandsloot_ru_description", "A chest containing treasures from the Mistlands. Right-click to open." },
                    
                    { "item_ashlandsloot_ru", "Ashlands Treasure Chest" },
                    { "item_ashlandsloot_ru_description", "A chest containing treasures from the Ashlands. Right-click to open." }
                };
                
                // Add all translations using the correct Jotunn API
                var localization = Jotunn.Managers.LocalizationManager.Instance.GetLocalization();
                localization.AddTranslation("English", translations);
                
                // Add verification with fallback direct registration
                StartCoroutine(VerifyAndFixRegistration(loadedPrefabs));
                
                // Unsubscribe from the event to ensure it only runs once
                Jotunn.Managers.PrefabManager.OnVanillaPrefabsAvailable -= RegisterChestItems;
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error registering chest items: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        // Helper method to create and register a CustomItem using Jotunn's framework
        private void CreateJotunnCustomItem(GameObject prefab, Sprite iconSprite, string chestPrefabName)
        {
            try
            {
                if (prefab == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Cannot create CustomItem: prefab is null for {chestPrefabName}");
                    return;
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Creating Jotunn CustomItem for {chestPrefabName}");
                
                // Make sure the prefab has an ItemDrop component
                ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"ItemDrop component missing on {prefab.name}, adding it");
                    try
                    {
                        itemDrop = prefab.AddComponent<ItemDrop>();
                        if (itemDrop == null)
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogError($"Failed to add ItemDrop component to {prefab.name}");
                            return;
                        }
                        
                        // Initialize the ItemDrop with default values to prevent null references
                        itemDrop.m_itemData = new ItemDrop.ItemData();
                        itemDrop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
                    }
                    catch (Exception ex)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogError($"Exception adding ItemDrop component: {ex.Message}\n{ex.StackTrace}");
                        return;
                    }
                }
                
                // Verify ItemDrop is properly initialized
                if (itemDrop.m_itemData == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"ItemDrop.m_itemData is null on {prefab.name}");
                    itemDrop.m_itemData = new ItemDrop.ItemData();
                }
                
                if (itemDrop.m_itemData.m_shared == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"ItemDrop.m_itemData.m_shared is null on {prefab.name}");
                    itemDrop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
                }
                
                // Create a CustomItem using Jotunn's framework
                // The fixReference parameter ensures all prefab references are properly set up
                CustomItem? customItem = null;
                try
                {
                    customItem = new CustomItem(prefab, fixReference: true);
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"CustomItem created successfully for {prefab.name}");
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Failed to create CustomItem: {ex.Message}\n{ex.StackTrace}");
                    return;
                }
                
                if (customItem == null || customItem.ItemDrop == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"CustomItem or its ItemDrop is null for {prefab.name}");
                    return;
                }
                
                // Configure the item data
                // Use the prefab's name for naming conventions
                ItemDrop.ItemData.SharedData sharedData = customItem.ItemDrop.m_itemData.m_shared;
                
                // Set basic properties
                sharedData.m_name = $"$item_{prefab.name}";
                sharedData.m_description = $"$item_{prefab.name}_description";
                sharedData.m_maxQuality = 1;
                sharedData.m_maxStackSize = 20;
                sharedData.m_equipDuration = 0.2f;
                sharedData.m_variants = 1;
                
                // Set the item type to Consumable so it can be used (right-clicked)
                sharedData.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                sharedData.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
                
                // CRITICAL: Set quest item flag to prevent storage in containers
                // This replaces our custom Inventory.MoveItemToThis patch
                sharedData.m_questItem = true;
                
                // Disable durability
                sharedData.m_useDurability = false;
                
                // Initialize combat-related fields to prevent null references
                sharedData.m_attack = new Attack();
                sharedData.m_secondaryAttack = new Attack();
                sharedData.m_damages = new HitData.DamageTypes();
                sharedData.m_damagesPerLevel = new HitData.DamageTypes();
                sharedData.m_damageModifiers = new List<HitData.DamageModPair>();
                
                // Assign the icon sprite
                if (iconSprite != null)
                {
                    sharedData.m_icons = new Sprite[1] { iconSprite };
                }
                else
                {
                    // Create a fallback sprite if needed
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"No icon sprite provided for {chestPrefabName}, creating placeholder");
                    Texture2D placeholderTex = new Texture2D(1, 1);
                    placeholderTex.SetPixel(0, 0, UnityEngine.Color.white);
                    placeholderTex.Apply();
                    Sprite placeholderSprite = Sprite.Create(placeholderTex, new UnityEngine.Rect(0, 0, 1, 1), new UnityEngine.Vector2(0.5f, 0.5f));
                    sharedData.m_icons = new Sprite[1] { placeholderSprite };
                }
                
                // Store custom data for our mod
                if (customItem.ItemDrop.m_itemData.m_customData == null)
                {
                    customItem.ItemDrop.m_itemData.m_customData = new Dictionary<string, string>();
                }
                
                // Ensure the custom data key exists
                string customDataKey = "CustomData_ValhalLoot";
                if (!customItem.ItemDrop.m_itemData.m_customData.ContainsKey(customDataKey))
                {
                    customItem.ItemDrop.m_itemData.m_customData[customDataKey] = "{}";
                }
                
                // Store chest prefab information and a unique ID
                customItem.ItemDrop.m_itemData.m_customData["ChestID"] = Guid.NewGuid().ToString();
                customItem.ItemDrop.m_itemData.m_customData["PrefabName"] = chestPrefabName;
                
                // Register the item with Jotunn's ItemManager
                // This handles all the registration with ObjectDB and ZNetScene automatically
                Jotunn.Managers.ItemManager.Instance.AddItem(customItem);
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Successfully registered {prefab.name} with Jotunn ItemManager");
                
                // DEBUG: Verify the prefab is actually in ObjectDB after registration
                if (ObjectDB.instance != null)
                {
                    GameObject verifyPrefab = ObjectDB.instance.GetItemPrefab(prefab.name);
                    if (verifyPrefab != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"DEBUG: Verified {prefab.name} exists in ObjectDB after registration");
                    }
                    else
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogError($"DEBUG: FAILED to find {prefab.name} in ObjectDB after registration!");
                    }
                }
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error creating Jotunn CustomItem: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        // Verification and fallback registration coroutine
        private IEnumerator VerifyAndFixRegistration(Dictionary<string, GameObject> loadedPrefabs)
        {
            // Wait a short time to allow both systems to complete registration
            yield return new WaitForSeconds(2f);
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo("Verifying prefab registration and applying fallback if needed");
            
            if (ObjectDB.instance == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError("ObjectDB.instance is null during verification");
                yield break;
            }
            
            foreach (var kvp in loadedPrefabs)
            {
                string prefabName = kvp.Key;
                GameObject prefab = kvp.Value;
                
                // Check if prefab exists in ObjectDB
                GameObject verifyPrefab = ObjectDB.instance.GetItemPrefab(prefabName);
                
                if (verifyPrefab != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"SUCCESS: Verified {prefabName} exists in ObjectDB");
                }
                else
                {
                    // FALLBACK: Direct registration if both systems failed
                    ValhalLootPlugin.ValhalLootLogger.LogWarning($"Fallback: Directly registering {prefabName} to ObjectDB");
                    
                    // Ensure ItemDrop is properly set up
                    EnsureItemDropComponent(prefab);
                    
                    // Add directly to ObjectDB
                    if (!ObjectDB.instance.m_items.Contains(prefab))
                    {
                        ObjectDB.instance.m_items.Add(prefab);
                        ObjectDB.instance.m_itemByHash[prefab.name.GetStableHashCode()] = prefab;
                        
                        // Register status effects if any
                        ItemDrop.ItemData.SharedData shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                        RegisterStatusEffect(shared.m_attackStatusEffect);
                        RegisterStatusEffect(shared.m_consumeStatusEffect);
                        RegisterStatusEffect(shared.m_equipStatusEffect);
                        RegisterStatusEffect(shared.m_setStatusEffect);
                        
                        ObjectDB.instance.UpdateRegisters();
                        
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Directly added {prefabName} to ObjectDB");
                    }
                }
            }
            
            // Final verification
            foreach (var kvp in loadedPrefabs)
            {
                string prefabName = kvp.Key;
                GameObject verifyPrefab = ObjectDB.instance.GetItemPrefab(prefabName);
                
                if (verifyPrefab != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"FINAL CHECK: {prefabName} exists in ObjectDB");
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"FINAL CHECK FAILED: {prefabName} still not in ObjectDB!");
                }
            }
        }

        // Helper method for status effect registration
        private void RegisterStatusEffect(StatusEffect statusEffect)
        {
            if (statusEffect != null && ObjectDB.instance != null && 
                !ObjectDB.instance.GetStatusEffect(statusEffect.name.GetStableHashCode()))
            {
                ObjectDB.instance.m_StatusEffects.Add(statusEffect);
            }
        }
        
        // Helper method to ensure ItemDrop component exists and is properly configured
        private void EnsureItemDropComponent(GameObject prefab)
        {
            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"ItemDrop component missing on {prefab.name}, adding it");
                itemDrop = prefab.AddComponent<ItemDrop>();
            }
            
            // Initialize ItemDrop if needed
            if (itemDrop.m_itemData == null)
            {
                itemDrop.m_itemData = new ItemDrop.ItemData();
            }
            
            if (itemDrop.m_itemData.m_shared == null)
            {
                itemDrop.m_itemData.m_shared = new ItemDrop.ItemData.SharedData();
            }
            
            // Configure basic properties
            ItemDrop.ItemData.SharedData sharedData = itemDrop.m_itemData.m_shared;
            sharedData.m_name = $"$item_{prefab.name}";
            sharedData.m_description = $"$item_{prefab.name}_description";
            sharedData.m_maxQuality = 1;
            sharedData.m_maxStackSize = 20;
            sharedData.m_equipDuration = 0.2f;
            sharedData.m_variants = 1;
            sharedData.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            sharedData.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
            sharedData.m_questItem = true;
            sharedData.m_useDurability = false;
        }

        // Helper method to get a Player by ID
        private Player? GetPlayerByID(string playerID)
        {
            if (string.IsNullOrEmpty(playerID) || ZNet.instance == null)
            {
                return null;
            }
            
            // Try to parse the player ID as a long
            long playerIDLong;
            bool isNumeric = long.TryParse(playerID, out playerIDLong);
            
            // Use ZNet to get all connected players
            List<ZNetPeer> peers = ZNet.instance.GetPeers();
            
            foreach (ZNetPeer peer in peers)
            {
                // Get player from peer
                GameObject playerObj = ZNetScene.instance.FindInstance(peer.m_characterID);
                if (playerObj != null)
                {
                    Player player = playerObj.GetComponent<Player>();
                    if (player != null)
                    {
                        // Compare as string if playerID is not numeric, otherwise compare as long
                        if ((isNumeric && player.GetPlayerID() == playerIDLong) ||
                            (!isNumeric && player.GetPlayerID().ToString() == playerID))
                        {
                            return player;
                        }
                    }
                }
            }
            
            // Also check local player
            if (Player.m_localPlayer != null)
            {
                if ((isNumeric && Player.m_localPlayer.GetPlayerID() == playerIDLong) ||
                    (!isNumeric && Player.m_localPlayer.GetPlayerID().ToString() == playerID))
                {
                    return Player.m_localPlayer;
                }
            }
            
            return null;
        }
        
        // Determine if a chest should drop based on enemy type and RNG
        // Uses a sequential decision tree: Boss > Miniboss (list) > ChestEnemyMap > Health Check
        public bool ShouldDropChest(Character enemy, Player killer)
        {
            if (enemy == null || killer == null)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning("ShouldDropChest called with null enemy or killer");
                return false;
            }
            
            // Check initialization but log instead of just returning false
            if (!_isInitialized)
            {
                ValhalLootPlugin.ValhalLootLogger.LogWarning($"ValhalLootManager not fully initialized when checking drop for {enemy.m_name}. Checking if we can proceed anyway.");
                
                // Check if we have the minimum requirements to proceed
                if (Config == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Cannot proceed with drop check: Config is null");
                    return false;
                }
            }
            
            // Get the enemy's name and normalize it
            string enemyName = enemy.m_name;
            string normalizedName = enemyName;
            
            // Handle $enemy_ prefix in enemy names
            if (enemyName.StartsWith("$enemy_"))
            {
                normalizedName = enemyName.Substring(7); // Remove "$enemy_" prefix
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Normalized enemy name from {enemyName} to {normalizedName}");
            }
            
            // STEP 1: Check if the enemy is a boss (using Valheim's built-in boss flag)
            bool isBoss = enemy.IsBoss();
            if (isBoss)
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {normalizedName} is a boss");
                
                // Check if this boss has a chest mapping
                string chestPrefab = GetEnemyChestPrefab(enemy);
                if (string.IsNullOrEmpty(chestPrefab))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Boss {normalizedName} not found in any chest mapping, skipping drop");
                    return false;
                }
                
                // Roll for drop using boss drop chance
                float dropChance = Config.BossDropChance.Value;
                float roll = UnityEngine.Random.value;
                bool shouldDrop = roll <= dropChance;
                
                // Log the roll result
                string playerID = killer.GetPlayerID().ToString();
                string playerName = killer.GetPlayerName();
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Boss drop roll for {normalizedName} (killed by {playerName}): {roll} vs chance {dropChance}, shouldDrop: {shouldDrop}");
                
                return shouldDrop;
            }
            
            // STEP 2: Check if the enemy is in our miniboss list
            List<string> minibossList = new List<string>
            {
                "Skeleton_Hildir",
                "Fenring_Cultist_Hildir",
                "GoblinBrute_Hildir",
                "Charred_Melee_Dyrnwyn"
            };
            
            if (minibossList.Contains(normalizedName))
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {normalizedName} is in miniboss list");
                
                // Check if this miniboss has a chest mapping
                string chestPrefab = GetEnemyChestPrefab(enemy);
                if (string.IsNullOrEmpty(chestPrefab))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Miniboss {normalizedName} not found in any chest mapping, skipping drop");
                    return false;
                }
                
                // Roll for drop using miniboss drop chance
                float dropChance = Config.MinibossDropChance.Value;
                float roll = UnityEngine.Random.value;
                bool shouldDrop = roll <= dropChance;
                
                // Log the roll result
                string playerID = killer.GetPlayerID().ToString();
                string playerName = killer.GetPlayerName();
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Miniboss drop roll for {normalizedName} (killed by {playerName}): {roll} vs chance {dropChance}, shouldDrop: {shouldDrop}");
                
                return shouldDrop;
            }
            
            // STEP 3: Check if the enemy is in any chest-enemy mapping
            string regularChestPrefab = GetEnemyChestPrefab(enemy);
            if (!string.IsNullOrEmpty(regularChestPrefab))
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {normalizedName} is in chest-enemy mapping for {regularChestPrefab}");
                
                // STEP 4: Regular creature with mapping (Step 4 health check removed as redundant)
                float regularDropChance = Config.CreatureDropChance.Value;
                float regularRoll = UnityEngine.Random.value;
                bool regularShouldDrop = regularRoll <= regularDropChance;
                
                // Log the roll result
                string regularPlayerID = killer.GetPlayerID().ToString();
                string regularPlayerName = killer.GetPlayerName();
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Regular creature drop roll for {normalizedName} (killed by {regularPlayerName}): {regularRoll} vs chance {regularDropChance}, shouldDrop: {regularShouldDrop}");
                
                return regularShouldDrop;
            }
            
            // If we get here, it's a creature not in any mapping
            ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {normalizedName} is not in any chest mapping, skipping drop");
            return false;
        }
        
        // Get the chest prefab for an enemy based on config mapping
        public string GetEnemyChestPrefab(Character enemy)
        {
            if (enemy == null || !_isInitialized || Config == null)
            {
                return string.Empty; // No chest
            }
            
            try
            {
                // Get the enemy's name for simple mapping
                string enemyName = enemy.m_name;
                string prefabName = enemy.gameObject.name;
                
                // Simple logging
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Mapping enemy: name={enemyName}, prefab={prefabName}");
                
                // Handle $enemy_ prefix in enemy names
                string normalizedEnemyName = enemyName;
                if (enemyName.StartsWith("$enemy_"))
                {
                    normalizedEnemyName = enemyName.Substring(7); // Remove "$enemy_" prefix
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Normalized enemy name from {enemyName} to {normalizedEnemyName}");
                }
                
                // Direct lookup in EnemyToChestMap - this is the only lookup we'll keep
                if (Config.EnemyToChestMap.TryGetValue(normalizedEnemyName, out string chestPrefab))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {enemyName} directly mapped to chest {chestPrefab}");
                    return chestPrefab;
                }
                
                
                
                // If we get here, the enemy wasn't found in any mapping
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Enemy {enemyName} not found in any chest mapping, no chest will drop");
                return string.Empty;
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error in GetEnemyChestPrefab: {ex.Message}");
                return string.Empty;
            }
        }
        
        // Give a chest item to a player
        public void GiveChestToPlayer(Player player, string chestName)
        {
            try
            {
                if (player == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Cannot give chest to null player");
                    return;
                }
                
                // Check if player's inventory exists
                Inventory inventory = player.GetInventory();
                if (inventory == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Player inventory is null");
                    return;
                }
                
                // Log requested chest name
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Requested chest: '{chestName}'");
                
                // Get the chest prefab directly by name from ObjectDB
                if (string.IsNullOrEmpty(chestName) || ObjectDB.instance == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError("Invalid chest name or ObjectDB not initialized");
                    return;
                }
                
                // Try to find the prefab in the game's object database
                GameObject chestPrefab = ObjectDB.instance.GetItemPrefab(chestName);
                if (chestPrefab == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Prefab for '{chestName}' not found in ObjectDB");
                    return;
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Using chest prefab: {chestPrefab.name}");
                
                // Get the ItemDrop component from the prefab
                ItemDrop itemDrop = chestPrefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"ItemDrop component not found on chest prefab {chestPrefab.name}");
                    return;
                }
                
                // Create a new ItemDrop.ItemData instance using the prefab's data
                ItemDrop.ItemData itemData;
                try
                {
                    itemData = itemDrop.m_itemData.Clone();
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("Successfully cloned ItemData");
                    
                    // Configure the item for stacking (up to 50) with zero weight
                    itemData.m_shared.m_maxStackSize = 50;
                    itemData.m_shared.m_weight = 0.0f;
                    itemData.m_stack = 1;
                    
                    // Store the prefab name in custom data for persistence after relog
                    if (itemData.m_customData == null)
                    {
                        itemData.m_customData = new Dictionary<string, string>();
                    }
                    itemData.m_customData["prefabName"] = chestName;
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Stored prefab name '{chestName}' in item custom data for persistence");
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Error cloning ItemData: {ex.Message}\n{ex.StackTrace}");
                    return;
                }
                
                // Check if inventory has room before attempting to add
                if (!inventory.CanAddItem(itemData))
                {
                    // Inventory is full, show message to player
                    player.Message(MessageHud.MessageType.Center, "$valhalloot_inventory_full");
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Could not give chest to player {player.GetPlayerName()}: Inventory full");
                    return;
                }
                
                // Add the item to the player's inventory
                ValhalLootPlugin.ValhalLootLogger.LogInfo("Adding chest to player inventory");
                bool wasAdded = inventory.AddItem(itemData);
                
                if (wasAdded)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("Chest added to player inventory successfully");
                    player.Message(MessageHud.MessageType.Center, "$valhalloot_received");
                }
                else
                {
                    // This should not happen since we checked with CanAddItem first
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Failed to add chest to inventory despite CanAddItem check");
                    player.Message(MessageHud.MessageType.Center, "$valhalloot_inventory_full");
                }
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error giving chest to player: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        
    }
}
