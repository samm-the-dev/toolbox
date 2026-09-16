using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

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

        // Vanilla prefabs we read from.
        public const string SourceItemPrefabName = "FistGold"; // Nord Knucklechains
        public const string VanillaHookPrefabName = "GrapplingHook"; // Deep North grappling hook
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
                // Grapple Knuckles is framed as an alternative to enchanting
                // Knucklechains into Frostfire/Thunderblood, not a further
                // upgrade of them: it trades the elemental proc for the
                // grapple secondary attack plus a flat pierce bonus (see
                // GrappleAttackPatch). So the recipe consumes the plain
                // FistGold, not an enchanted variant.
                var itemConfig = new ItemConfig
                {
                    Name = "$item_fistgold_grapple",
                    Description = "$item_fistgold_grapple_description",
                    CraftingStation = "piece_forge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = SourceItemPrefabName, Amount = 1 },
                        new RequirementConfig { Item = VanillaHookPrefabName, Amount = 1 },
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
