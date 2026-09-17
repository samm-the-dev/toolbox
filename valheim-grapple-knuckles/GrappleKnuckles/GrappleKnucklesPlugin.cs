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
        public const string SourceItemPrefabName = "FistGold"; // Nord Knucklechains - model/mechanics source only, see CloneKnucklechains
        public const string VanillaHookPrefabName = "GrapplingHook"; // Mistlands grappling hook (NOT Deep North - corrected, see CLAUDE.md)
        public const string VanillaHookProjectilePrefabName = "Projectile_GrapplingHook";
        public const string ClonedItemPrefabName = "FistGold_Grapple";
        public const string ClonedProjectilePrefabName = "Projectile_GrapplingHook_Knuckles";

        private const string FlametalPrefabName = "Flametal";
        private const string CharredBonePrefabName = "CharredBone";

        // Cloned once PrefabManager.OnVanillaPrefabsAvailable fires, so
        // GrappleAttackPatch can give it its own damage without mutating the
        // GameObject the vanilla GrapplingHook item shares.
        public static GameObject ClonedProjectile { get; private set; }

        private readonly Harmony _harmony = new Harmony(ModGUID);

        private void Awake()
        {
            ItemManager.OnItemsRegistered += CloneKnucklechains;
            ItemManager.OnItemsRegistered += FenrisMageArmor.Clone;
            ItemManager.OnItemsRegistered += MountainTierAxe.Clone;
            ItemManager.OnItemsRegistered += AshlandsHybridArmor.Clone;
            ItemManager.OnItemsRegistered += DeepNorthHybridArmor.Clone;
            ItemManager.OnItemsRegistered += BloodMagicSpear.Clone;
            ItemManager.OnItemsRegistered += PrismBlade.Clone;
            PrefabManager.OnVanillaPrefabsAvailable += CloneProjectile;
            ElementalWeapons.Init();
            ShieldOfFrost.Init();
            ExplodingSledge.Init();

            _harmony.PatchAll();

            Jotunn.Logger.LogInfo($"{ModName} {ModVersion} loaded");
        }

        private void OnDestroy()
        {
            ItemManager.OnItemsRegistered -= CloneKnucklechains;
            ItemManager.OnItemsRegistered -= FenrisMageArmor.Clone;
            ItemManager.OnItemsRegistered -= MountainTierAxe.Clone;
            ItemManager.OnItemsRegistered -= AshlandsHybridArmor.Clone;
            ItemManager.OnItemsRegistered -= DeepNorthHybridArmor.Clone;
            ItemManager.OnItemsRegistered -= BloodMagicSpear.Clone;
            ItemManager.OnItemsRegistered -= PrismBlade.Clone;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneProjectile;
            ElementalWeapons.Dispose();
            ShieldOfFrost.Dispose();
            ExplodingSledge.Dispose();
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
                // Re-tiered to Ashlands per explicit direction: the intended
                // progression is "get the plain Grappling Hook easily in
                // Mistlands, then upgrade to this fist weapon in Ashlands."
                // That means the recipe deliberately does NOT require a real
                // FistGold (Deep North) - gating an Ashlands-tier, pre-Deep-
                // North item behind Deep North would contradict the whole
                // point. FistGold is still the CustomItem clone source for
                // model/mechanics only (per explicit "use the Deep North
                // fist weapon model" direction) - Jötunn's clone is a
                // design-time template copy, not a crafting requirement, so
                // this is safe to do without needing the player to ever
                // actually own a FistGold.
                //
                // Station: "blackforge" - confirmed real for Ashlands
                // weapon-tier crafting too (Dyrnwyn, Nidhögg both use it),
                // not exclusive to Deep North/Mistlands despite where this
                // mod first used it.
                var itemConfig = new ItemConfig
                {
                    Name = "$item_fistgold_grapple",
                    Description = "$item_fistgold_grapple_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = VanillaHookPrefabName, Amount = 1 },
                        new RequirementConfig { Item = FlametalPrefabName, Amount = 15, AmountPerLevel = 10 },
                        new RequirementConfig { Item = CharredBonePrefabName, Amount = 3, AmountPerLevel = 2 },
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
