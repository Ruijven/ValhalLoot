using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Entities;
using ValhalLoot; // Add this for custom extension methods

namespace ValhalLoot
{
    // Class to handle the loot chest drop confirmation dialog
    public class LootChestDropDialog : MonoBehaviour
    {
        // Static instance for access
        private static LootChestDropDialog? _instance;
        
        // Dialog state
        private bool _isVisible = false;
        private Player? _player;
        private ItemDrop.ItemData? _item;
        
        // Dialog position and size
        private Rect _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 100, 500, 200);
        private int _windowId = 6789; // Random ID to avoid conflicts
        
        // Dialog style
        private GUIStyle _titleStyle = new GUIStyle();
        private GUIStyle _textStyle = new GUIStyle();
        private GUIStyle _buttonStyle = new GUIStyle();
        
        // Initialize the dialog
        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize styles
            _titleStyle = new GUIStyle();
            _titleStyle.fontSize = 18;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = Color.white;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            
            _textStyle = new GUIStyle();
            _textStyle.fontSize = 14;
            _textStyle.normal.textColor = Color.white;
            _textStyle.alignment = TextAnchor.MiddleCenter;
            _textStyle.wordWrap = true;
            
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 14;
            _buttonStyle.padding = new RectOffset(10, 10, 5, 5);
        }
        
        // Show the dialog
        public static void ShowDropConfirmation(Player player, ItemDrop.ItemData item)
        {
            try
            {
                // Create the dialog if it doesn't exist
                if (_instance == null)
                {
                    GameObject dialogObject = new GameObject("LootChestDropDialog");
                    _instance = dialogObject.AddComponent<LootChestDropDialog>();
                }
                
                // Set up the dialog
                _instance._player = player;
                _instance._item = item;
                _instance._isVisible = true;
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo("Showing loot chest drop confirmation dialog");
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error showing drop confirmation dialog: {ex.Message}");
            }
        }
        
        // Draw the GUI
        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }
            
            // Draw the dialog window
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindow, "");
        }
        
        // Draw the window contents
        private void DrawWindow(int windowId)
        {
            // Get localized strings
            string title = Localization.instance.Localize("$valhalloot_drop_title");
            string message = Localization.instance.Localize("$valhalloot_drop_message");
            string confirmText = Localization.instance.Localize("$valhalloot_drop_confirm");
            string cancelText = Localization.instance.Localize("$valhalloot_drop_cancel");
            
            // Fallback to English if localization fails
            if (string.IsNullOrEmpty(title)) title = "Drop Loot Chest?";
            if (string.IsNullOrEmpty(message)) message = "If you throw this Loot Chest away, you will not be able to reclaim it.";
            if (string.IsNullOrEmpty(confirmText)) confirmText = "Okay";
            if (string.IsNullOrEmpty(cancelText)) cancelText = "Cancel";
            
            // Title
            GUILayout.Space(20);
            GUILayout.Label(title, _titleStyle);
            
            // Message
            GUILayout.Space(20);
            GUILayout.Label(message, _textStyle);
            
            // Buttons
            GUILayout.Space(30);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Confirm button
            if (GUILayout.Button(confirmText, _buttonStyle, GUILayout.Width(100)))
            {
                // Confirm drop
                if (_player != null && _item != null)
                {
                    PerformDrop(_player, _item);
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning("Cannot drop item: player or item is null");
                }
                _isVisible = false;
            }
            
            GUILayout.Space(20);
            
            // Cancel button
            if (GUILayout.Button(cancelText, _buttonStyle, GUILayout.Width(100)))
            {
                // Cancel drop
                _isVisible = false;
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            // Make the window draggable
            GUI.DragWindow();
        }
        
        // Perform the actual drop
        private static void PerformDrop(Player player, ItemDrop.ItemData item)
        {
            try
            {
                // Get the inventory
                Inventory inventory = player.GetInventory();
                
                // Remove the item from inventory
                inventory.RemoveItem(item);
                
                // Calculate drop position
                Vector3 position = player.transform.position + player.transform.forward + player.transform.up;
                Quaternion rotation = Quaternion.identity;
                
                // Spawn the item in the world
                ItemDrop component = UnityEngine.Object.Instantiate(item.m_dropPrefab, position, rotation).GetComponent<ItemDrop>();
                component.m_itemData = item.Clone();
                
                // Add velocity to throw it forward
                Rigidbody componentInChildren = component.GetComponentInChildren<Rigidbody>();
                if (componentInChildren)
                {
                    componentInChildren.velocity = (player.transform.forward + Vector3.up) * 5f;
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Player {player.GetPlayerName()} dropped loot chest after confirmation");
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error dropping loot chest: {ex.Message}");
            }
        }
    }
    
    public static class ValhalLootPatches
    {
        [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
        public static class Character_OnDeath_Patch
        {
            private static void Postfix(Character __instance)
            {
                try
                {
                    // Log the death event for debugging
                    if (__instance != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Character death detected: {__instance.m_name}");
                    }
                    else
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogWarning("Character death detected but instance is null");
                        return;
                    }
                    
                    // Skip if this is a player
                    if (__instance.IsPlayer())
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo("Skipping player death");
                        return;
                    }
                    
                    // Check if the character's network view is valid
                    if (!__instance.m_nview.IsValid())
                    {
                        // Instead of skipping, log a warning and continue
                        // Many creatures die with invalid ZDOs but we still want to process them
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Warning: {__instance.m_name} has invalid ZDO but continuing with drop check");
                    }
                    else if (!__instance.m_nview.IsOwner())
                    {
                        // Only skip if we're not the owner of a valid ZDO
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Skipping {__instance.m_name} death: Not the owner");
                        return;
                    }
                    
                    // Find the player who killed this enemy
                    Player killer = GetKillingPlayer(__instance);
                    if (killer == null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Skipping {__instance.m_name} death: No killer found");
                        return;
                    }
                    
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"{__instance.m_name} was killed by player {killer.GetPlayerName()}");
                    
                    // Check if ValhalLootManager is initialized
                    if (ValhalLootManager.Instance == null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogWarning("ValhalLootManager not initialized when processing creature death");
                        return;
                    }
                    
                    // Check if we should drop a chest
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Checking if {__instance.m_name} should drop a chest for {killer.GetPlayerName()}");
                    bool shouldDrop = ValhalLootManager.Instance.ShouldDropChest(__instance, killer);
                    
                    if (shouldDrop)
                    {
                        // Get the chest prefab directly from enemy mapping
                        string chestPrefab = ValhalLootManager.Instance.GetEnemyChestPrefab(__instance);
                        
                        if (string.IsNullOrEmpty(chestPrefab))
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogWarning($"No chest prefab mapping found for {__instance.m_name}, skipping drop");
                            return;
                        }
                        
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Giving {chestPrefab} chest to player {killer.GetPlayerName()} from {__instance.m_name}");
                        ValhalLootManager.Instance.GiveChestToPlayer(killer, chestPrefab);
                    }
                    else
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"No chest will be given to player {killer.GetPlayerName()} from {__instance.m_name}");
                    }
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Error in Character.OnDeath patch: {ex.Message}");
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
                }
            }
            
            // Helper method to get enemy biome directly from config
            private static string GetEnemyBiomeFromConfig(string enemyName)
            {
                if (string.IsNullOrEmpty(enemyName) || ValhalLootManager.Instance?.Config == null)
                {
                    return string.Empty;
                }
                
                // Look up the enemy in the ChestEnemyMap from config
                List<string> biomeList;
                if (ValhalLootManager.Instance.Config.ChestEnemyMap.TryGetValue(enemyName, out biomeList) && biomeList.Count > 0)
                {
                    return biomeList[0]; // Return the first biome in the list
                }
                
                return string.Empty; // No mapping found
            }
            
            // Helper method to find the player who killed the character
            private static Player GetKillingPlayer(Character character)
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo("[KILLER DEBUG] GetKillingPlayer called");
                
                if (character == null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning("[KILLER DEBUG] Character is null");
                    return null!;
                }
                
                // Method 1: Standard m_lastHit approach (most accurate when available)
                if (character.m_lastHit != null)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Character: {character.m_name}, LastHit: {character.m_lastHit}");
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] LastHit attacker ZDOID: {(character.m_lastHit.m_attacker.IsNone() ? "None" : character.m_lastHit.m_attacker.ToString())}");
                    
                    // Check if the last hit was from a player
                    Character attacker = character.m_lastHit.GetAttacker();
                    if (attacker != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Found attacker: {attacker.m_name}, IsPlayer: {attacker.IsPlayer()}");
                        
                        if (attacker.IsPlayer())
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Returning player attacker: {attacker.m_name}");
                            return attacker as Player ?? null!;
                        }
                    }
                    
                    // If the last hit was from a projectile, check if it was fired by a player
                    if (!character.m_lastHit.m_attacker.IsNone())
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo("[KILLER DEBUG] Checking projectile attacker");
                        
                        // Check local player first
                        if (Player.m_localPlayer != null)
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Local player ZDOID: {Player.m_localPlayer.GetZDOID()}");
                            
                            if (Player.m_localPlayer.GetZDOID().UserID == character.m_lastHit.m_attacker.UserID)
                            {
                                ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Matched local player as attacker: {Player.m_localPlayer.GetPlayerName()}");
                                return Player.m_localPlayer;
                            }
                        }
                        
                        // Otherwise check all connected players
                        if (ZNet.instance != null)
                        {
                            ValhalLootPlugin.ValhalLootLogger.LogInfo("[KILLER DEBUG] Checking connected players");
                            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
                            {
                                GameObject playerObj = ZNetScene.instance.FindInstance(peer.m_characterID);
                                if (playerObj != null)
                                {
                                    Player player = playerObj.GetComponent<Player>();
                                    if (player != null)
                                    {
                                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Checking player: {player.GetPlayerName()}");
                                        
                                        if (player.GetZDOID().UserID == character.m_lastHit.m_attacker.UserID)
                                        {
                                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Matched connected player as attacker: {player.GetPlayerName()}");
                                            return player;  // No null warning here as we already checked player != null
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogWarning("[KILLER DEBUG] Character.m_lastHit is null, using fallbacks");
                }
                
                // Method 2: Check if any player has this enemy as hover target (good fallback after session changes)
                if (Player.m_localPlayer != null && Player.m_localPlayer.GetHoverObject() == character.gameObject)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Local player has this enemy as hover target, assuming they're the killer");
                    return Player.m_localPlayer;
                }
                
                // Method 3: Check if any player has a weapon drawn and is near the enemy
                if (Player.m_localPlayer != null)
                {
                    ItemDrop.ItemData rightItem = Player.m_localPlayer.GetRightItem();
                    bool hasWeaponDrawn = rightItem != null && 
                                         (rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
                                          rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                                          rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
                    
                    if (hasWeaponDrawn && rightItem?.m_shared != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Local player has weapon drawn: {rightItem.m_shared.m_name}");
                    }
                    
                    // Method 4: Proximity check fallback - useful after session changes when m_lastHit data might be lost
                    float distance = Vector3.Distance(Player.m_localPlayer.transform.position, character.transform.position);
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Distance to local player: {distance}");
                    
                    // If player is within 25 units and has a weapon drawn, or within 10 units regardless,
                    // and we couldn't find another killer, assume they're the killer
                    if ((hasWeaponDrawn && distance < 25f) || distance < 10f)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Using proximity fallback, returning local player: {Player.m_localPlayer.GetPlayerName()}");
                        return Player.m_localPlayer;
                    }
                }
                
                // Method 5: Check for any nearby players as last resort
                if (Player.GetAllPlayers() != null)
                {
                    Player? closestPlayer = null;
                    float closestDistance = float.MaxValue;
                    
                    foreach (Player player in Player.GetAllPlayers())
                    {
                        if (player != null)
                        {
                            float distance = Vector3.Distance(player.transform.position, character.transform.position);
                            if (distance < closestDistance && distance < 30f)
                            {
                                closestPlayer = player;
                                closestDistance = distance;
                            }
                        }
                    }
                    
                    if (closestPlayer != null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"[KILLER DEBUG] Using closest player fallback: {closestPlayer.GetPlayerName()} at distance {closestDistance}");
                        return closestPlayer!;
                    }
                }
                
                ValhalLootPlugin.ValhalLootLogger.LogWarning("[KILLER DEBUG] No killer found after all fallback methods");
                return null!;
            }
        }
        
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.IsEquipable))]
        public static class ItemData_IsEquipable_Patch
        {
            private static void Postfix(ItemDrop.ItemData __instance, ref bool __result)
            {
                // Make ValhalLoot chest "equippable" (right-clickable)
                if (__instance != null && __instance.m_shared.m_name.Contains("valhalloot"))
                {
                    __result = true;
                }
            }
        }
        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        public static class Humanoid_UseItem_Patch
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                try
                {
                    // Check if this is our ValhalLoot chest item using multiple detection methods
                    if (item != null && __instance is Player player)
                    {
                        bool isLootChest = false;
                        string detectionMethod = "";
                        
                        // Method 1: Try to get ValhalLoot custom data directly (most reliable when present)
                        string customDataKey = "CustomData_ValhalLoot";
                        if (item.m_customData != null && item.m_customData.ContainsKey(customDataKey))
                        {
                            isLootChest = true;
                            detectionMethod = "custom data";
                        }
                        // Method 2: Check item name (reliable across sessions)
                        else if (item.m_shared?.m_name != null && 
                                (item.m_shared.m_name.ToLower().Contains("valhalloot") || 
                                 item.m_shared.m_name.ToLower().Contains("loot_ru") ||
                                 item.m_shared.m_name.ToLower().Contains("loot chest") ||
                                 item.m_shared.m_name.ToLower().Contains("meadows") ||
                                 item.m_shared.m_name.ToLower().Contains("blackforest") ||
                                 item.m_shared.m_name.ToLower().Contains("swamp") ||
                                 item.m_shared.m_name.ToLower().Contains("mountain") ||
                                 item.m_shared.m_name.ToLower().Contains("plains") ||
                                 item.m_shared.m_name.ToLower().Contains("mistland") ||
                                 item.m_shared.m_name.ToLower().Contains("ashland")))
                        {
                            isLootChest = true;
                            detectionMethod = "name";
                        }
                        // Method 3: Check item description (reliable across sessions)
                        else if (item.m_shared?.m_description != null &&
                                (item.m_shared.m_description.ToLower().Contains("valhalloot") ||
                                 item.m_shared.m_description.ToLower().Contains("loot chest")))
                        {
                            isLootChest = true;
                            detectionMethod = "description";
                        }
                        // Method 4: Check icon name as last resort (most persistent)
                        else if (item.m_shared?.m_icons?.Length > 0 && 
                                item.m_shared.m_icons[0]?.name != null &&
                                item.m_shared.m_icons[0].name.ToLower().Contains("lootchest"))
                        {
                            isLootChest = true;
                            detectionMethod = "icon name";
                        }
                        // Method 5: Check item ID (should be persistent)
                        else if (item.m_shared?.m_name != null && item.m_shared.m_name.StartsWith("$item_") && 
                                item.m_shared.m_name.EndsWith("_ru"))
                        {
                            isLootChest = true;
                            detectionMethod = "item ID pattern";
                        }
                        
                        if (isLootChest)
                        {
                            // This is our chest item, handle it
                            ValhalLootPlugin.ValhalLootLogger.LogInfo($"ValhalLoot chest used via {detectionMethod} detection");
                            OnChestItemUsed(item, player);
                            return false; // Skip original method
                        }
                    }
                    
                    return true; // Continue with original method
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Error in Humanoid.UseItem patch: {ex.Message}");
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
                    return true; // Let the original method run on error
                }
            }
            
            private static void OnChestItemUsed(ItemDrop.ItemData item, Player player)
            {
                try
                {
                    ValhalLootPlugin.ValhalLootLogger.LogDebug($"ValhalLoot chest used by {player.GetPlayerName()}");
                    
                    // Check if ValhalLootUI is initialized
                    if (ValhalLootUI.Instance == null)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogWarning("ValhalLootUI not initialized when using chest item");
                        return;
                    }
                    
                    // Process the chest use - the chest name will determine the loot table
                    if (ValhalLootManager.Instance != null)
                    {
                        ValhalLootManager.Instance.ProcessChestUse(player, item);
                    }
                    else
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogWarning("ValhalLootManager not initialized when using chest item");
                    }
                }
                catch (Exception ex)
                {
                    ValhalLootPlugin.ValhalLootLogger.LogError($"Error processing chest item use: {ex.Message}");
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
    public static class Player_DropItem_Patch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, int amount, ref bool __result)
        {
            try
            {
                // Check if this is a ValhalLoot chest item using multiple detection methods
                if (item != null)
                {
                    bool isLootChest = false;
                    string detectionMethod = "";
                    
                    // Method 1: Try to get ValhalLoot custom data directly (most reliable when present)
                    string customDataKey = "CustomData_ValhalLoot";
                    if (item.m_customData != null && item.m_customData.ContainsKey(customDataKey))
                    {
                        isLootChest = true;
                        detectionMethod = "custom data";
                    }
                    // Method 2: Check item name (reliable across sessions)
                    else if (item.m_shared?.m_name != null && 
                            (item.m_shared.m_name.ToLower().Contains("valhalloot") || 
                             item.m_shared.m_name.ToLower().Contains("loot_ru") ||
                             item.m_shared.m_name.ToLower().Contains("loot chest") ||
                             item.m_shared.m_name.ToLower().Contains("meadows") ||
                             item.m_shared.m_name.ToLower().Contains("blackforest") ||
                             item.m_shared.m_name.ToLower().Contains("swamp") ||
                             item.m_shared.m_name.ToLower().Contains("mountain") ||
                             item.m_shared.m_name.ToLower().Contains("plains") ||
                             item.m_shared.m_name.ToLower().Contains("mistland") ||
                             item.m_shared.m_name.ToLower().Contains("ashland")))
                    {
                        isLootChest = true;
                        detectionMethod = "name";
                    }
                    // Method 3: Check item description (reliable across sessions)
                    else if (item.m_shared?.m_description != null &&
                            (item.m_shared.m_description.ToLower().Contains("valhalloot") ||
                             item.m_shared.m_description.ToLower().Contains("loot chest")))
                    {
                        isLootChest = true;
                        detectionMethod = "description";
                    }
                    // Method 4: Check icon name as last resort (most persistent)
                    else if (item.m_shared?.m_icons?.Length > 0 && 
                            item.m_shared.m_icons[0]?.name != null &&
                            item.m_shared.m_icons[0].name.ToLower().Contains("lootchest"))
                    {
                        isLootChest = true;
                        detectionMethod = "icon name";
                    }
                    // Method 5: Check item ID (should be persistent)
                    else if (item.m_shared?.m_name != null && item.m_shared.m_name.StartsWith("$item_") && 
                            item.m_shared.m_name.EndsWith("_ru"))
                    {
                        isLootChest = true;
                        detectionMethod = "item ID pattern";
                    }
                    
                    if (isLootChest)
                    {
                        ValhalLootPlugin.ValhalLootLogger.LogInfo($"Intercepted drop of loot chest item via {detectionMethod} detection");
                    }
                    
                    if (isLootChest)
                    {
                        // Only show confirmation dialog if this is a Player
                        if (__instance is Player playerInstance)
                        {
                            // Show confirmation dialog
                            LootChestDropDialog.ShowDropConfirmation(playerInstance, item);
                            
                            // Return false to prevent the original method from running
                            __result = true; // Indicate success to the caller
                            return false;
                        }
                    }
                }
                
                // Not a loot chest or not a player, proceed with normal drop
                return true;
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error in Player.DropItem patch: {ex.Message}");
                return true; // Let the original method run
            }
        }
    }
    
    // Note: The Inventory_MoveItemToThis_Patch has been removed as it's now handled by Jotunn's item framework
    // We set m_questItem = true in CreateJotunnCustomItem to prevent storage in containers
}
