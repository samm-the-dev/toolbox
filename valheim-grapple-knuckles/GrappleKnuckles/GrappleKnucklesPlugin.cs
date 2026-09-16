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

        // Vanilla prefabs we read from.
        public const string SourceItemPrefabName = "FistGold"; // Nord Knucklechains
        public const string VanillaHookPrefabName = "GrapplingHook"; // Deep North grappling hook
        public const string VanillaHookProjectilePrefabName = "Projectile_GrapplingHook";
        public const string ClonedItemPrefabName = "FistGold_Grapple";
        public const string ClonedProjectilePrefabName = "Projectile_GrapplingHook_Knuckles";

        // Cloned once PrefabManager.OnVanillaPrefabsAvailable fires, so
        // GrappleAttackPatch can give it its own damage without mutating the
        // GameObject the vanilla GrapplingHook item shares.
        public static GameObject ClonedProjectile { get; private set; }

        private readonly Harmony _harmony = new Harmony(ModGUID);

        private void Awake()
        {
            ItemManager.OnItemsRegistered += CloneKnucklechains;
            ItemManager.OnItemsRegistered += FenrisMageArmor.Clone;
            PrefabManager.OnVanillaPrefabsAvailable += CloneProjectile;

            _harmony.PatchAll();

            Jotunn.Logger.LogInfo($"{ModName} {ModVersion} loaded");
        }

        private void OnDestroy()
        {
            ItemManager.OnItemsRegistered -= CloneKnucklechains;
            ItemManager.OnItemsRegistered -= FenrisMageArmor.Clone;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneProjectile;
            _harmony?.UnpatchSelf();
        }

        private void CloneProjectile()
        {
            if (ClonedProjectile != null)
            {
                return;
            }

            try
            {
                ClonedProjectile = PrefabManager.Instance.CreateClonedPrefab(
                    ClonedProjectilePrefabName, VanillaHookProjectilePrefabName);

                Jotunn.Logger.LogInfo($"Cloned {VanillaHookProjectilePrefabName} -> {ClonedProjectilePrefabName}");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone {VanillaHookProjectilePrefabName}: {ex}");
            }
        }

        private void CloneKnucklechains()
        {
            try
            {
                // Grapple Knuckles is framed as an alternative to enchanting
                // Knucklechains into Frostfire/Thunderblood, not a further
                // upgrade of them: it trades the elemental proc for the
                // grapple secondary attack plus a pierce/speed bonus (see
                // GrappleAttackPatch). So the recipe consumes the plain
                // FistGold, not an enchanted variant.
                //
                // Station: vanilla FistGold is actually a two-step item
                // (Cast made at the Black Forge, finished at the Frost
                // Foundry via a cooking-station-style mechanic, not a normal
                // recipe) - we're not replicating that chain, just picking
                // a thematically fitting CraftingStation for this new
                // combine recipe. "blackforge" is the confirmed internal
                // prefab name for the Black Forge.
                var itemConfig = new ItemConfig
                {
                    Name = "$item_fistgold_grapple",
                    Description = "$item_fistgold_grapple_description",
                    CraftingStation = "blackforge",
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
