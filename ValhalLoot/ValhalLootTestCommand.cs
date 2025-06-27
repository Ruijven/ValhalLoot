using System;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Player = Valheim.Player;

namespace ValhalLoot
{
    /// <summary>
    /// Console command for testing ValhalLoot functionality
    /// </summary>
    public class ValhalLootTestCommand : ConsoleCommand
    {
        public override string Name => "valhalloot_test";
        public override string Help => "Tests ValhalLoot functionality";
        
        public override void Run(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: valhalloot_test [loot|chest|e2e]");
                    Console.WriteLine("  loot - Test loot chest opening logic");
                    Console.WriteLine("  chest - Test chest prefab to biome mapping");
                    Console.WriteLine("  e2e - Run end-to-end test of the loot system");
                    return;
                }
                
                string command = args[0].ToLower();
                
                switch (command)
                {
                    case "loot":
                    case "chest":
                        // Run the test method in ValhalLootManager
                        if (ValhalLootManager.Instance != null)
                        {
                            Console.WriteLine("Running ValhalLoot chest test...");
                            ValhalLootManager.Instance.TestLootChestOpening();
                            Console.WriteLine("Test complete. Check BepInEx logs for results.");
                        }
                        else
                        {
                            Console.WriteLine("Error: ValhalLootManager instance not available");
                        }
                        break;
                        
                    case "e2e":
                    case "endtoend":
                        // Run the end-to-end test
                        if (ValhalLootManager.Instance != null && Player.m_localPlayer != null)
                        {
                            Console.WriteLine("Running ValhalLoot end-to-end test...");
                            RunEndToEndTest();
                            Console.WriteLine("End-to-end test complete. Check BepInEx logs for results.");
                        }
                        else
                        {
                            Console.WriteLine("Error: ValhalLootManager instance or local player not available");
                            Console.WriteLine("Make sure you're in-game with a character loaded");
                        }
                        break;
                    
                    default:
                        Console.WriteLine($"Unknown test command: {command}");
                        Console.WriteLine("Usage: valhalloot_test [loot|chest|e2e]");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running test command: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error in test command: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        // Run an end-to-end test of the loot system
        private void RunEndToEndTest()
        {
            try
            {
                ValhalLootPlugin.ValhalLootLogger.LogInfo("=== VALHALLOOT END-TO-END TEST ===\n");
                
                // Step 1: Test enemy type detection and drop chance calculation
                ValhalLootPlugin.ValhalLootLogger.LogInfo("STEP 1: Testing enemy type detection and drop chance calculation");
                
                // Test regular creature
                Character regularCreature = new GameObject("TestRegularCreature").AddComponent<Character>();
                regularCreature.m_name = "Greyling";
                regularCreature.m_health = 20f;
                
                // Test boss
                Character boss = new GameObject("TestBoss").AddComponent<Character>();
                boss.m_name = "gd_king";
                boss.m_health = 1000f;
                
                // Test miniboss
                Character miniboss = new GameObject("TestMiniboss").AddComponent<Character>();
                miniboss.m_name = "Troll";
                miniboss.m_health = 600f;
                
                // Test drop chance calculation
                Player player = Player.m_localPlayer;
                
                bool shouldDropRegular = ValhalLootManager.Instance.ShouldDropChest(regularCreature, player);
                bool shouldDropBoss = ValhalLootManager.Instance.ShouldDropChest(boss, player);
                bool shouldDropMiniboss = ValhalLootManager.Instance.ShouldDropChest(miniboss, player);
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Regular creature drop result: {shouldDropRegular}");
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Boss drop result: {shouldDropBoss}");
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Miniboss drop result: {shouldDropMiniboss}");
                
                // Step 2: Test enemy-to-chest mapping
                ValhalLootPlugin.ValhalLootLogger.LogInfo("\nSTEP 2: Testing enemy-to-chest mapping");
                
                string regularChest = ValhalLootManager.Instance.GetEnemyChestPrefab(regularCreature);
                string bossChest = ValhalLootManager.Instance.GetEnemyChestPrefab(boss);
                string minibossChest = ValhalLootManager.Instance.GetEnemyChestPrefab(miniboss);
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Regular creature '{regularCreature.m_name}' maps to chest: '{regularChest}'");
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Boss '{boss.m_name}' maps to chest: '{bossChest}'");
                ValhalLootPlugin.ValhalLootLogger.LogInfo($"Miniboss '{miniboss.m_name}' maps to chest: '{minibossChest}'");
                
                // Step 3: Test chest giving to player
                ValhalLootPlugin.ValhalLootLogger.LogInfo("\nSTEP 3: Testing chest giving to player");
                
                // Only test if we have a valid chest mapping
                if (!string.IsNullOrEmpty(regularChest))
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo($"Attempting to give chest '{regularChest}' to player");
                    ValhalLootManager.Instance.GiveChestToPlayer(player, regularChest);
                }
                else
                {
                    ValhalLootPlugin.ValhalLootLogger.LogInfo("No valid chest mapping found for test creature, skipping chest giving test");
                }
                
                // Step 4: Test loot table mapping
                ValhalLootPlugin.ValhalLootLogger.LogInfo("\nSTEP 4: Testing loot table mapping");
                ValhalLootManager.Instance.TestLootChestOpening();
                
                // Clean up test objects
                UnityEngine.Object.Destroy(regularCreature.gameObject);
                UnityEngine.Object.Destroy(boss.gameObject);
                UnityEngine.Object.Destroy(miniboss.gameObject);
                
                ValhalLootPlugin.ValhalLootLogger.LogInfo("\n=== END-TO-END TEST COMPLETE ===");
            }
            catch (Exception ex)
            {
                ValhalLootPlugin.ValhalLootLogger.LogError($"Error in end-to-end test: {ex.Message}");
                ValhalLootPlugin.ValhalLootLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
