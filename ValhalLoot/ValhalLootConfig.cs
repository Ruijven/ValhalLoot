using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ValhalLoot
{
    public class ValhalLootConfig
    {
        private static readonly string ConfigFileName = $"{ValhalLootPlugin.ModName}.yml";
        private static readonly string ConfigFilePath = Path.Combine(Paths.ConfigPath, ConfigFileName);
        
        // Configuration entries
        public ConfigEntry<float> CreatureDropChance { get; private set; }
        public ConfigEntry<float> BossDropChance { get; private set; }
        public ConfigEntry<float> MinibossDropChance { get; private set; }
        public ConfigEntry<int> LootOptionsCount { get; private set; }
        
        // YAML-based loot tables - direct mapping from chest prefab to loot entries
        public Dictionary<string, List<LootEntry>> ChestLoot { get; private set; } = new Dictionary<string, List<LootEntry>>();
        
        // Compatibility property for backward compatibility
        public Dictionary<string, List<LootEntry>> BiomeLoot => ChestLoot;
        
        // Direct mapping from chest prefab to list of enemies that drop it
        public Dictionary<string, List<string>> ChestEnemyMap { get; private set; } = new Dictionary<string, List<string>>();
        
        // Compatibility property for backward compatibility
        public Dictionary<string, List<string>> EnemyBiomeMap => ChestEnemyMap;
        
        // Reverse lookup: enemy name to chest prefab
        public Dictionary<string, string> EnemyToChestMap { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Reference to the config sync
        private readonly ConfigSync _configSync;
        
        public ValhalLootConfig(ConfigSync configSync, ConfigFile config)
        {
            _configSync = configSync;
            
            // Register configuration options
            CreatureDropChance = config.Bind("Drop Chances", "RegularCreatures", 1.00f, 
                new ConfigDescription("Chance for regular creatures to drop a ValhalLoot chest (0-1)", 
                    new AcceptableValueRange<float>(0f, 1f)));
            
            BossDropChance = config.Bind("Drop Chances", "Bosses", 0.30f, 
                new ConfigDescription("Chance for bosses to drop a ValhalLoot chest (0-1)", 
                    new AcceptableValueRange<float>(0f, 1f)));
            
            MinibossDropChance = config.Bind("Drop Chances", "Minibosses", 0.50f, 
                new ConfigDescription("Chance for minibosses to drop a ValhalLoot chest (0-1)", 
                    new AcceptableValueRange<float>(0f, 1f)));
            
            LootOptionsCount = config.Bind("General", "LootOptionsCount", 3, 
                new ConfigDescription("Number of loot options to present to the player", 
                    new AcceptableValueRange<int>(1, 5)));
            
            // Sync all configs
            _configSync.AddConfigEntry(CreatureDropChance);
            _configSync.AddConfigEntry(BossDropChance);
            _configSync.AddConfigEntry(MinibossDropChance);
            _configSync.AddConfigEntry(LootOptionsCount);
            
            // Load YAML configuration
            LoadYamlConfig();
        }
        
        public void LoadYamlConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    // Load from existing config file
                    string yamlContent = File.ReadAllText(ConfigFilePath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    
                    var yamlConfig = deserializer.Deserialize<YamlConfig>(yamlContent);
                    
                    // Convert YAML config to our runtime format
                    ConvertYamlConfig(yamlConfig);
                    
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("YAML configuration loaded successfully");
                }
                else
                {
                    // Create default config
                    CreateDefaultYamlConfig();
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("Default YAML configuration created");
                }
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error loading YAML configuration: {ex.Message}");
                // Create default config as fallback
                CreateDefaultYamlConfig();
            }
        }
        
        private void ConvertYamlConfig(YamlConfig yamlConfig)
        {
            ChestLoot.Clear();
            ChestEnemyMap.Clear();
            EnemyToChestMap.Clear();
            
            // Apply settings from YAML if available
            if (yamlConfig.Settings != null && yamlConfig.Settings.DropChance != null)
            {
                // Update BepInEx config values from YAML
                CreatureDropChance.Value = yamlConfig.Settings.DropChance.RegularCreatures;
                BossDropChance.Value = yamlConfig.Settings.DropChance.Bosses;
                MinibossDropChance.Value = yamlConfig.Settings.DropChance.Minibosses;
                ValhalLootPlugin.ValhalLootLogger.LogInfo("Applied drop chance settings from YAML config");
            }
            
            // Convert chest loot entries
            foreach (var chestLoot in yamlConfig.ChestLoot)
            {
                string chestPrefab = chestLoot.Key;
                List<LootEntry> entries = new List<LootEntry>();
                
                foreach (string entryString in chestLoot.Value)
                {
                    LootEntry? entry = ParseLootEntry(entryString);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                
                ChestLoot[chestPrefab] = entries;
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Loaded {entries.Count} loot entries for chest {chestPrefab}");
            }
            
            // Process chest-to-enemy mapping
            foreach (var mapping in yamlConfig.ChestEnemyMap)
            {
                string chestPrefab = mapping.Key;
                List<string> enemies = new List<string>(mapping.Value);
                
                ChestEnemyMap[chestPrefab] = enemies;
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Loaded {enemies.Count} enemy mappings for chest {chestPrefab}");
                
                // Build reverse lookup (enemy to chest)
                foreach (string enemy in enemies)
                {
                    EnemyToChestMap[enemy] = chestPrefab;
                    ValhalLootPlugin.ValhalLootLogger.LogDebug($"Mapped enemy {enemy} to chest {chestPrefab}");
                }
            }
        }
        
        private void CreateDefaultYamlConfig()
        {
            var yamlConfig = new YamlConfig
            {
                Settings = new Settings
                {
                    DropChance = new DropChance
                    {
                        RegularCreatures = CreatureDropChance.Value,
                        Bosses = BossDropChance.Value,
                        Minibosses = MinibossDropChance.Value
                    },
                    // Cooldown removed as per user request
                },
                ChestLoot = new Dictionary<string, List<string>>
                {
                    {
                        "meadowsloot_ru", new List<string>
                        {
                            "CapeDeerHide:2-4:1.0",
                            "HelmetLeather:2-4:1.0",
                            "ArmorLeatherChest:2-4:1.0",
                            "ArmorLeatherLegs:2-4:1.0",
                            "AxeStone:2-4:1.0",
                            "AxeFlint:2-4:1.0",
                            "Club:2-4:1.0",
                            "ShieldWood:2-4:1.0",
                            "ShieldWoodTower:2-4:1.0",
                            "Bow:2-4:1.0",
                            "KnifeFlint:2-4:1.0",
                            "SpearFlint:2-4:1.0",
                            "Hoe:2-4:1.0",
                            "Hammer:2-4:1.0",
                            "PickaxeAntler:2-4:0.5"
                        }
                    },
                    {
                        "blackforestloot_ru", new List<string>
                        {
                            "ArmorTrollLeatherChest:2-4:1.0",
                            "ArmorTrollLeatherLegs:2-4:1.0",
                            "HelmetTrollLeather:2-4:1.0",
                            "CapeTrollHide:2-4:1.0",
                            "ArmorBronzeChest:2-4:1.0",
                            "ArmorBronzeLegs:2-4:1.0",
                            "HelmetBronze:2-4:1.0",
                            "MaceBronze:2-4:1.0",
                            "SwordBronze:2-4:1.0",
                            "AxeBronze:2-4:1.0",
                            "KnifeCopper:2-4:1.0",
                            "BowFineWood:2-4:1.0",
                            "SpearBronze:2-4:1.0",
                            "AtgeirBronze:2-4:1.0",
                            "SledgeStagbreaker:2-4:1.0",
                            "ShieldBoneTower:2-4:1.0",
                            "ShieldBronzeBuckler:2-4:1.0",
                            "Cultivator:2-4:1.0",
                            "PickaxeBronze:2-4:1.0"
                        }
                    },
                    {
                        "swamploot_ru", new List<string>
                        {
                            "KnifeChitin:2-4:1.0",
                            "SpearChitin:2-4:1.0",
                            "ArmorRootChest:2-4:1.0",
                            "ArmorRootLegs:2-4:1.0",
                            "HelmetRoot:2-4:1.0",
                            "ArmorIronChest:2-4:1.0",
                            "ArmorIronLegs:2-4:1.0",
                            "HelmetIron:2-4:1.0",
                            "AxeIron:2-4:1.0",
                            "Battleaxe:2-4:1.0",
                            "BowHuntsman:2-4:1.0",
                            "MaceIron:2-4:1.0",
                            "SledgeIron:2-4:1.0",
                            "ShieldBanded:2-4:1.0",
                            "ShieldIronBuckler:2-4:1.0",
                            "ShieldIronTower:2-4:1.0",
                            "ShieldSerpentScale:2-4:1.0",
                            "SpearElderbark:2-4:1.0",
                            "SwordIron:2-4:1.0",
                            "AtgeirIron:2-4:1.0",
                            "PickaxeIron:2-4:1.0"
                        }
                    },
                    {
                        "mountainsloot_ru", new List<string>
                        {
                            "ArmorWolfChest:2-4:1.0",
                            "ArmorWolfLegs:2-4:1.0",
                            "HelmetDrake:2-4:1.0",
                            "CapeWolf:2-4:1.0",
                            "ArmorFenringChest:2-4:1.0",
                            "ArmorFenringLegs:2-4:1.0",
                            "HelmetFenring:2-4:1.0",
                            "ShieldSilver:2-4:1.0",
                            "BowDraugrFang:2-4:1.0",
                            "SpearWolfFang:2-4:1.0",
                            "SwordSilver:2-4:1.0",
                            "KnifeSilver:2-4:1.0",
                            "BattleaxeCrystal:2-4:1.0",
                            "MaceSilver:2-4:1.0",
                            "FistFenrirClaw:2-4:1.0"
                        }
                    },
                    {
                        "plainsloot_ru", new List<string>
                        {
                            "ArmorPaddedCuirass:2-4:1.0",
                            "ArmorPaddedGreaves:2-4:1.0",
                            "HelmetPadded:2-4:1.0",
                            "CapeLinen:2-4:1.0",
                            "AxeBlackMetal:2-4:1.0",
                            "AtgeirBlackMetal:2-4:1.0",
                            "KnifeBlackMetal:2-4:1.0",
                            "MaceNeedle:2-4:1.0",
                            "ShieldBlackMetal:2-4:1.0",
                            "ShieldBlackMetalTower:2-4:1.0",
                            "SwordBlackMetal:2-4:1.0"
                        }
                    },
                    {
                        "mistlandsloot_ru", new List<string>
                        {
                            "ArmorCarapaceChest:2-4:1.0",
                            "ArmorCarapaceLegs:2-4:1.0",
                            "HelmetCarapace:2-4:1.0",
                            "HelmetMage:2-4:1.0",
                            "ArmorMageChest:2-4:1.0",
                            "ArmorMageLegs:2-4:1.0",
                            "CapeFeather:2-4:1.0",
                            "AxeJotunBane:2-4:1.0",
                            "BowSpineSnap:2-4:1.0",
                            "AtgeirHimminAfl:2-4:1.0",
                            "KnifeSkollAndHati:2-4:1.0",
                            "SledgeDemolisher:2-4:1.0",
                            "SwordMistwalker:2-4:1.0",
                            "THSwordKrom:2-4:1.0",
                            "CrossbowArbalest:2-4:1.0",
                            "ShieldCarapace:2-4:1.0",
                            "ShieldCarapaceBuckler:2-4:1.0",
                            "StaffFireball:2-4:1.0",
                            "StaffIceShards:2-4:1.0",
                            "StaffShield:2-4:1.0",
                            "StaffSkeleton:2-4:1.0",
                            "PickaxeBlackMetal:2-4:1.0"
                        }
                    },
                    {
                        "ashlandsloot_ru", new List<string>
                        {
                            "CapeAsksvin:2-4:1.0",
                            "CapeAsh:2-4:1.0",
                            "AxeBerzerkr:2-4:1.0",
                            "MaceEldner:2-4:1.0",
                            "SwordNiedhogg:2-4:1.0",
                            "THSwordSlayer:2-4:1.0",
                            "SpearSplitner:2-4:1.0",
                            "ShieldFlametal:2-4:1.0",
                            "ShieldFlametalTower:2-4:1.0",
                            "BowAshlands:2-4:1.0",
                            "CrossbowRipper:2-4:1.0",
                            "StaffLightning:2-4:1.0",
                            "StaffGreenRoots:2-4:1.0",
                            "StaffClusterbomb:2-4:1.0",
                            "StaffRedTroll:2-4:1.0",
                            "ArmorMageChest_Ashlands:2-4:1.0",
                            "ArmorMageLegs_Ashlands:2-4:1.0",
                            "HelmetMage_Ashlands:2-4:1.0",
                            "ArmorAshlandsMediumChest:2-4:1.0",
                            "ArmorAshlandsMediumlegs:2-4:1.0",
                            "HelmetAshlandsMediumHood:2-4:1.0",
                            "HelmetFlametal:2-4:1.0",
                            "ArmorFlametalChest:2-4:1.0",
                            "ArmorFlametalLegs:2-4:1.0"
                        }
                    }
                },
                ChestEnemyMap = new Dictionary<string, List<string>>
                {
                    {
                        "meadowsloot_ru", new List<string>
                        {
                            "Greyling",
                            "Boar",
                            "Neck",
                            "Deer"
                        }
                    },
                    {
                        "blackforestloot_ru", new List<string>
                        {
                            "Greydwarf",
                            "Greydwarf_Elite",
                            "Greydwarf_Shaman",
                            "Skeleton_Hildir",
                            "Troll",
                            "Eikthyr",
                            "Ghost"
                        }
                    },
                    {
                        "swamploot_ru", new List<string>
                        {
                            "Draugr",
                            "Draugr_Elite",
                            "Wraith",
                            "Serpent",
                            "gd_king",
                            "Abomination"
                        }
                    },
                    {
                        "mountainsloot_ru", new List<string>
                        {
                            "Wolf",
                            "Drake",
                            "Fenring",
                            "StoneGolem",
                            "Bonemass",
                            "Fenring_Cultist_Hildir",
                            "Fenring_Cultist"
                        }
                    },
                    {
                        "plainsloot_ru", new List<string>
                        {
                            "Goblin",
                            "GoblinBrute",
                            "BlobTar",
                            "GoblinShaman",
                            "Deathsquito",
                            "Dragon",
                            "GoblinBrute_Hildir",
                            "Lox"
                        }
                    },
                    {
                        "mistlandsloot_ru", new List<string>
                        {
                            "Gjall",
                            "Seeker",
                            "SeekerBrute",
                            "GoblinKing",
                            "Dverger"
                        }
                    },
                    {
                        "ashlandsloot_ru", new List<string>
                        {
                            "Charred_Archer",
                            "Charred_Mage",
                            "Charred_Melee",
                            "Charred_Twitcher",
                            "Morgen",
                            "BonemawSerpent",
                            "FallenValkyrie",
                            "Volture",
                            "piece_Charred_Balista",
                            "SeekerQueen",
                            "Fader",
                            "Charred_Melee_Dyrnwyn",
                            "Asksvin"
                        }
                    }
                }
            };
            
            // Convert to runtime format
            ConvertYamlConfig(yamlConfig);
            
            // Save to file
            SaveYamlConfig(yamlConfig);
        }
        
        private void SaveYamlConfig(YamlConfig config)
        {
            try
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                
                string yamlContent = serializer.Serialize(config);
                File.WriteAllText(ConfigFilePath, yamlContent);
                ValhalLootPlugin.ValhalLootLogger.LogInfo("YAML configuration saved successfully");
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error saving YAML configuration: {ex.Message}");
            }
        }
        
        // Helper method to parse a loot entry string (format: "ItemName:MinQuality-MaxQuality:Weight")
        private static LootEntry? ParseLootEntry(string entryString)
        {
            try
            {
                string[] parts = entryString.Split(':');
                string itemName = parts[0];
                
                string[] qualityParts = parts[1].Split('-');
                int minQuality = int.Parse(qualityParts[0]);
                int maxQuality = qualityParts.Length > 1 ? int.Parse(qualityParts[1]) : minQuality;
                
                float weight = parts.Length > 2 ? float.Parse(parts[2]) : 1.0f;
                
                return new LootEntry
                {
                    ItemName = itemName,
                    MinQuality = minQuality,
                    MaxQuality = maxQuality,
                    Weight = weight
                };
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error parsing loot entry '{entryString}': {ex.Message}");
                return null;
            }
        }
        
        // YAML configuration structure
        public class YamlConfig
        {
            public YamlConfig? Config { get; private set; }
            public Settings? Settings { get; set; }
            [YamlMember(Alias = "LootChestTables")]
            public Dictionary<string, List<string>> ChestLoot { get; set; } = new Dictionary<string, List<string>>();
            [YamlMember(Alias = "EnemyChestTables")]
            public Dictionary<string, List<string>> ChestEnemyMap { get; set; } = new Dictionary<string, List<string>>();
            // public Dictionary<string, List<string>> BiomeLoot => Config?.ChestLoot ?? new Dictionary<string, List<string>>();
            // public Dictionary<string, List<string>> EnemyBiomeMap => Config?.ChestEnemyMap ?? new Dictionary<string, List<string>>();
        }
        
        public class Settings
        {
            public DropChance DropChance { get; set; } = new DropChance();
            
            // List of enemies considered minibosses
            // These will use the MinibossDropChance instead of CreatureDropChance
            [YamlMember(Alias = "Minibosses")]
            public List<string> Minibosses { get; set; } = new List<string>
            {
                "Skeleton_Hildir",
                "Fenring_Cultist_Hildir",
                "GoblinBrute_Hildir",
                "Charred_Melee_Dyrnwyn"
            };
        }
        
        public class DropChance
        {
            public float RegularCreatures { get; set; } = 0.05f;
            public float Bosses { get; set; } = 0.30f;
            public float Minibosses { get; set; } = 0.50f;
        }
    }
    
    public class LootEntry
    {
        public string ItemName { get; set; } = string.Empty;
        public int MinQuality { get; set; }
        public int MaxQuality { get; set; }
        public float Weight { get; set; } = 1.0f;
    }
}
