using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace GrappleKnuckles
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class GrappleKnucklesPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.samm.grappleknuckles";
        public const string ModName = "Grapple Knuckles";
        public const string ModVersion = "0.1.0";

        // Vanilla prefab we clone from.
        public const string SourceItemPrefabName = "FistGold"; // Nord Knucklechains
        public const string ClonedItemPrefabName = "FistGold_Grapple";

        private readonly Harmony _harmony = new Harmony(ModGUID);

        private void Awake()
        {
            ItemManager.OnItemsRegistered += CloneKnucklechains;

            _harmony.PatchAll();

            Jotunn.Logger.LogInfo($"{ModName} {ModVersion} loaded");
        }

        private void OnDestroy()
        {
            ItemManager.OnItemsRegistered -= CloneKnucklechains;
            _harmony?.UnpatchSelf();
        }

        private void CloneKnucklechains()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_fistgold_grapple",
                    Description = "$item_fistgold_grapple_description",
                    CraftingStation = "piece_workbench",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = "FistGold", Amount = 1 },
                        new RequirementConfig { Item = "Chain", Amount = 5 },
                        new RequirementConfig { Item = "Iron", Amount = 10 },
                    },
                };

                var clonedItem = new CustomItem(ClonedItemPrefabName, SourceItemPrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                Jotunn.Logger.LogInfo($"Cloned {SourceItemPrefabName} -> {ClonedItemPrefabName}");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone {SourceItemPrefabName}: {ex}");
            }
        }
    }
}
