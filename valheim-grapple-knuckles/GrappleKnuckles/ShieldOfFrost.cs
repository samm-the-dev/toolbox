using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // The frost half of the original fire/frost dagger idea, moved here per
    // explicit direction: a shield that channels Eitr while blocking,
    // procs frost + rides vanilla's own stagger-then-double-damage window on
    // a successful parry, bursts frost in an AoE when the block breaks, and
    // blocks from all directions rather than a frontal arc. All the actual
    // mechanics live in ShieldOfFrostPatches.cs; this file just clones the
    // item and the frost burst projectile used for the break effect.
    //
    // Confirmed facts this relies on (see ShieldOfFrostPatches.cs for the
    // combat-mechanic-specific ones):
    //   - Same PrefabManager.CreateClonedPrefab / ItemManager CustomItem
    //     patterns already used throughout this mod.
    //   - staff_clusterbombstaff_projectile (Staff of Fracturing's
    //     projectile) has real m_aoe/m_ttl-driven burst behavior, already
    //     relied on elsewhere in this project.
    //
    // Re-tiered to Mistlands per explicit direction, after a full survey of
    // real vanilla elemental weapons/staves - useful against Seekers'
    // ranged fire attacks. Priced with Freeze Gland + Refined Eitr,
    // confirmed real materials for "Staff of Frost" (the Mistlands frost
    // staff); this shield is a melee/block echo of that staff, not a
    // replica of its exact recipe/cost.
    //
    // NOT confirmed: ShieldSourcePrefabName below (a guess at a real
    // Mistlands-tier vanilla shield to clone from, picked for thematic/tier
    // consistency with the rest of this mod's items) was never verified
    // against Jötunn's prefab list this session - if it's wrong, cloning
    // logs a warning and no-ops rather than crashing, same as every other
    // "prefab not found" case in this mod. Confirm the real name before
    // relying on this.
    internal static class ShieldOfFrost
    {
        public const string ShieldSourcePrefabName = "ShieldCarapace"; // UNCONFIRMED - verify against your game
        public const string ShieldClonedPrefabName = "ShieldCarapace_Frost";

        public const string FrostBurstSourcePrefabName = "staff_clusterbombstaff_projectile"; // Staff of Fracturing
        public const string FrostBurstClonedPrefabName = "Burst_ShieldOfFrost";

        private const string FreezeGlandPrefabName = "FreezeGland";
        private const string RefinedEitrPrefabName = "Eitr";

        // Smaller than a full staff cast, matching the "small AoE" ask.
        private const float FrostBurstScale = 0.6f;

        public static GameObject FrostBurstProjectile { get; private set; }

        public static void Init()
        {
            ItemManager.OnItemsRegistered += CloneShield;
            PrefabManager.OnVanillaPrefabsAvailable += CloneFrostBurst;
        }

        public static void Dispose()
        {
            ItemManager.OnItemsRegistered -= CloneShield;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneFrostBurst;
        }

        private static void CloneFrostBurst()
        {
            if (FrostBurstProjectile != null)
            {
                return;
            }

            try
            {
                FrostBurstProjectile = PrefabManager.Instance.CreateClonedPrefab(FrostBurstClonedPrefabName, FrostBurstSourcePrefabName);
                if (FrostBurstProjectile != null)
                {
                    FrostBurstProjectile.transform.localScale *= FrostBurstScale;
                    Logger.LogInfo($"Cloned {FrostBurstSourcePrefabName} -> {FrostBurstClonedPrefabName} (scaled {FrostBurstScale:P0})");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {FrostBurstSourcePrefabName}: {ex}");
            }
        }

        private static void CloneShield()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_shield_of_frost",
                    Description = "$item_shield_of_frost_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = ShieldSourcePrefabName, Amount = 1 },
                        new RequirementConfig { Item = FreezeGlandPrefabName, Amount = 4, AmountPerLevel = 4 },
                        new RequirementConfig { Item = RefinedEitrPrefabName, Amount = 10, AmountPerLevel = 5 },
                    },
                };

                var clonedItem = new CustomItem(ShieldClonedPrefabName, ShieldSourcePrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                Logger.LogInfo($"Cloned {ShieldSourcePrefabName} -> {ShieldClonedPrefabName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {ShieldSourcePrefabName}: {ex}");
            }
        }
    }
}
