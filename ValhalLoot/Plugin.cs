using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using ValhalLoot;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using UnityEngine.UI;
using ItemManager;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using PrefabManager = ItemManager.PrefabManager;
using BepInPaths = BepInEx.Paths;

namespace ValhalLoot
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class ValhalLootPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ValhalLoot";
        internal const string ModVersion = "0.0.1";
        internal const string Author = "ruijven";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = BepInPaths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ValhalLootLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        // ValhalLoot specific components
        private static ValhalLootConfig? _valhalLootConfig;
        private static GameObject? _valhalLootUIObject;

        // Location Manager variables
        public Texture2D? tex = null;

        // Use only if you need them
        //private Sprite mySprite = null!;
        //private SpriteRenderer sr = null!;

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            // Using Jotunn's localization system instead of custom Localizer
            // Register translations in RegisterTranslations() method

            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet =
                false; // This and the variable above are used to prevent the config from saving on startup for each config entry. This is speeds up the startup process.

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            // Initialize ValhalLoot configuration
            _valhalLootConfig = new ValhalLootConfig(ConfigSync, Config);
            
            // Register console commands
            // Temporarily commented out until test command class is properly referenced
            // CommandManager.Instance.AddConsoleCommand(new ValhalLootTestCommand());
            
            // Load asset bundle for ValhalLoot chest using Jotunn's AssetUtils
            try
            {
                // Try to load from embedded resources first (preferred method)
                AssetBundle? assetBundle = Jotunn.Utils.AssetUtils.LoadAssetBundleFromResources("lootbundle_ru", Assembly.GetExecutingAssembly());
                
                // If not found in resources, try loading from file
                if (assetBundle == null)
                {
                    string assetBundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "assets", "lootbundle_ru");
                    assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
                }
                
                if (assetBundle == null)
                {
                    ValhalLootLogger.LogError("Failed to load lootbundle_ru asset bundle from resources or file path");
                    ValhalLootLogger.LogInfo("Creating placeholder chest without custom assets");
                }
                else
                {
                    ValhalLootLogger.LogInfo($"Successfully loaded asset bundle: {assetBundle.name}");
                }
            }
            catch (Exception ex)
            {
                ValhalLootLogger.LogError($"Error loading asset bundle: {ex.Message}");
            }
            
            // Create ValhalLoot manager and UI objects, but defer initialization until player is in world
            GameObject valhalLootManagerObj = new GameObject("ValhalLootManager");
            DontDestroyOnLoad(valhalLootManagerObj);
            ValhalLootManager manager = valhalLootManagerObj.AddComponent<ValhalLootManager>();
            
            // Initialize ValhalLoot UI
            _valhalLootUIObject = new GameObject("ValhalLootUI");
            DontDestroyOnLoad(_valhalLootUIObject);
            ValhalLootUI ui = _valhalLootUIObject.AddComponent<ValhalLootUI>();
            
            // Initialize components with configuration
            manager.Initialize(_valhalLootConfig);
            ui.Initialize();
            
            // Register VFX prefabs
            // VFX registration removed as requested by user
            
            // Apply Harmony patches
            _harmony.PatchAll(typeof(ValhalLootPlugin));
            
            ValhalLootPlugin.ValhalLootLogger.LogInfo("ValhalLoot components created - initialization will complete when player enters world");
            ValhalLootPlugin.ValhalLootLogger.LogInfo("Harmony patches applied for item usage detection");

            #region PieceManager Example Code
            /* Commented out unused example code
            // Globally turn off configuration options for your pieces, omit if you don't want to do this.
            BuildPiece.ConfigurationEnabled = false;

            // Format: new("AssetBundleName", "PrefabName", "FolderName");
            BuildPiece examplePiece1 = new("funwardBundle", "funwardPrefabName", "FunWardFolder");

            examplePiece1.Name
                .English("Fun Ward"); // Localize the name and description for the building piece for a language. Use this if you're not using the LocalizationManager.
            examplePiece1.Description.English("Ward For testing the Piece Manager");
            examplePiece1.RequiredItems.Add("FineWood", 20,
                false); // Set the required items to build. Format: ("PrefabName", Amount, Recoverable)
            examplePiece1.RequiredItems.Add("SurtlingCore", 20, false);
            examplePiece1.Category.Set(PieceManager.BuildPieceCategory.Misc);
            examplePiece1.Crafting.Set(PieceManager.CraftingTable
                .ArtisanTable); // Set a crafting station requirement for the piece.
            examplePiece1.Extension.Set(PieceManager.CraftingTable.Forge,
                2); // Makes this piece a station extension, can change the max station distance by changing the second value. Use strings for custom tables.
            //examplePiece1.Crafting.Set("CUSTOMTABLE"); // If you have a custom table you're adding to the game. Just set it like this.
            //examplePiece1.SpecialProperties.NoConfig = true;  // Do not generate a config for this piece, omit this line of code if you want to generate a config.
            examplePiece1.SpecialProperties =
                new SpecialProperties()
                    { AdminOnly = true, NoConfig = true }; // You can declare multiple properties in one line           


            BuildPiece
                examplePiece2 =
                    new("bamboo",
                        "Bamboo_Wall"); // Note: If you wish to use the default "assets" folder for your assets, you can omit it!
            examplePiece2.Name.English("Bamboo Wall");
            examplePiece2.Description.English("A wall made of bamboo!");
            examplePiece2.RequiredItems.Add("BambooLog", 20, false);
            examplePiece2.Category.Set(PieceManager.BuildPieceCategory.BuildingWorkbench);
            examplePiece2.Crafting
                .Set("CUSTOMTABLE"); // If you have a custom table you're adding to the game. Just set it like this.
            examplePiece2.SpecialProperties.AdminOnly = true; // You can declare these one at a time as well!.


            // If you want to add your item to the cultivator or another hammer with vanilla categories
            // Format: (AssetBundle, "PrefabName", addToCustom, "Item that has a piecetable")
            BuildPiece examplePiece3 = new("bamboo", "Bamboo_Sapling");
            examplePiece3.Name.English("Bamboo Sapling");
            examplePiece3.Description.English("A young bamboo tree, called a sapling");
            examplePiece3.RequiredItems.Add("BambooSeed", 20, false);
            examplePiece3.Tool.Add("Cultivator"); // Format: ("Item that has a piecetable")
            examplePiece3.SpecialProperties.NoConfig = true;

            // If you don't want to make an icon inside unity, but want the PieceManager to snag one for you, simply add .Snapshot() to your piece.
            examplePiece3
                .Snapshot(); // Optionally, you can use the lightIntensity parameter to set the light intensity of the snapshot. Default is 1.3 or the cameraRotation parameter to set the rotation of the camera. Default is null.

            // If you want a more custom piece, below is an example. Including custom category and custom crafting station. Also adding to a custom hammer.
            BuildPiece examplePiece4 = new("bamboo", "Bamboo_Beam_Light");
            examplePiece4.Name.English("Bamboo Beam Light");
            examplePiece4.Description.English("A light made of bamboo!");
            examplePiece4.RequiredItems.Add("BambooLog", 20, false);
            examplePiece4.Category.Set("Custom Category");
            examplePiece4.Crafting.Set("CUSTOMTABLE");
            examplePiece4.Tool.Add("Custom Hammer");
            examplePiece4.SpecialProperties.NoConfig = true;
            examplePiece4
                .Snapshot(); // Optionally, you can use the lightIntensity parameter to set the light intensity of the snapshot. Default is 1.3 or the cameraRotation parameter to set the rotation of the camera. Default is null.

            // Need to add something to ZNetScene but not the hammer, cultivator or other? 
            PiecePrefabManager.RegisterPrefab("bamboo", "Bamboo_Beam_Light");

            // Does your model need to swap materials with a vanilla material? Format: (GameObject, isJotunnMock)
            MaterialReplacer.RegisterGameObjectForMatSwap(examplePiece3.Prefab, false);


            // What if you want to use a custom shader from the game (like Custom/Piece that allows snow!!!) but your unity shader isn't set to Custom/Piece? Format: (GameObject, MaterialReplacer.ShaderType.)
            //MaterialReplacer.RegisterGameObjectForShaderSwap(examplePiece3.Prefab, MaterialReplacer.ShaderType.PieceShader);

            // Detailed instructions on how to use the MaterialReplacer can be found on the current PieceManager Wiki. https://github.com/AzumattDev/PieceManager/wiki
            */
            #endregion

            




            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();

            if (saveOnSet)
            {
                Config.SaveOnConfigSet = saveOnSet;
                Config.Save();
            }
        }

        private void RegisterTranslations()
        {
            Dictionary<string, string> translations = new Dictionary<string, string>
            {
                { "valhalloot_chest_name", "Valhalla Chest" },
                { "valhalloot_chest_description", "A chest containing treasures from Valhalla" },
                { "valhalloot_ui_title", "Valhalla Loot" },
                { "valhalloot_ui_select", "Select one item:" },
                { "valhalloot_ui_close", "Close" },
                // Cooldown-related localization tokens removed as per user request
            };
            
            // Get custom localization instance for this mod
            var localization = Jotunn.Managers.LocalizationManager.Instance.GetLocalization();
            
            // Add translations
            localization.AddTranslation("English", translations);
        }
        
        // VFX registration methods removed as requested by user

        public void OnDestroy()
        {
            Config.Save();
            _harmony.UnpatchSelf();
        }
        
        // Harmony patch to detect when the chest item is used
        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        private static class Player_ConsumeItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
            {
                try
                {
                    // Check if the item being consumed is our chest
                    if (item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == "valhalloot_ru")
                    {
                        ValhalLootLogger.LogInfo($"Player {__instance.GetPlayerName()} is using ValhalLoot chest");
                        
                        // Process the chest usage through our manager
                        if (ValhalLootManager.Instance != null)
                        {
                            ValhalLootManager.Instance.ProcessChestUse(__instance, item);
                            
                            // We've handled this item, so return false to prevent the original method from running
                            // and set __result to true to indicate the item was consumed
                            __result = true;
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ValhalLootLogger.LogError($"Error in ConsumeItem patch: {ex.Message}");
                }
                
                // Continue with the original method for other items
                return true;
            }
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(BepInPaths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ValhalLootLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ValhalLootLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ValhalLootLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    // KeyboardExtensions class removed as it's not needed for this mod
}