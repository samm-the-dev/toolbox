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
    // Visually cloned from ShieldIronBuckler ("Iron Buckler", confirmed
    // real prefab via Jötunn's item-list.html) per explicit direction, with
    // a silver-ish tint applied via MaterialPropertyBlock.SetColor("_Color", ...) -
    // confirmed to be a real, documented technique (Rexabit/valheim-visuals-modifier,
    // a working recolor mod, applies color the same way, on the same
    // "_Color" shader property Valheim's weapon/shield materials expose).
    // Recolor is applied best-effort per renderer, logged and skipped on
    // any exception rather than breaking the item - the *result* was never
    // visually verified in this environment, only the technique.
    //
    // The "frost enchant glow" VFX asked for alongside this was
    // deliberately NOT attempted: the only real precedent found
    // (naomi-nada/nada-vfx-weapon) is a whole dedicated per-item particle
    // VFX rig system, still in active development/preview upstream - not a
    // simple attach-and-done API. Left as an open idea for the desktop
    // session, where the result can actually be seen while iterating.
    internal static class ShieldOfFrost
    {
        public const string ShieldSourcePrefabName = "ShieldIronBuckler"; // "Iron Buckler", confirmed real
        public const string ShieldClonedPrefabName = "ShieldIronBuckler_Frost";

        public const string FrostBurstSourcePrefabName = "staff_clusterbombstaff_projectile"; // Staff of Fracturing
        public const string FrostBurstClonedPrefabName = "Burst_ShieldOfFrost";

        private const string FreezeGlandPrefabName = "FreezeGland";
        private const string RefinedEitrPrefabName = "Eitr";

        // Smaller than a full staff cast, matching the "small AoE" ask.
        private const float FrostBurstScale = 0.6f;

        // A cool, light silver-grey. Confirmed-real technique
        // (MaterialPropertyBlock.SetColor("_Color", ...)), but this exact
        // shade was never visually verified - tune freely.
        private static readonly Color SilverTint = new Color(0.75f, 0.78f, 0.82f, 1f);

        public static GameObject FrostBurstProjectile { get; private set; }

        public static void Init()
        {
            // Item cloning uses OnVanillaPrefabsAvailable, not
            // ItemManager.OnItemsRegistered - see GrappleKnucklesPlugin.cs.
            PrefabManager.OnVanillaPrefabsAvailable += CloneShield;
            PrefabManager.OnVanillaPrefabsAvailable += CloneFrostBurst;
        }

        public static void Dispose()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CloneShield;
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

                TryApplySilverTint(clonedItem.ItemDrop.gameObject);

                Logger.LogInfo($"Cloned {ShieldSourcePrefabName} -> {ShieldClonedPrefabName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {ShieldSourcePrefabName}: {ex}");
            }
        }

        // Best-effort silver recolor via MaterialPropertyBlock, so we don't
        // mutate the shared material asset (which could leak the tint onto
        // the vanilla Iron Buckler too). Degrades silently to the source
        // item's original color on any exception, same "don't break the
        // item over a cosmetic" philosophy as the rest of this mod.
        private static void TryApplySilverTint(GameObject target)
        {
            try
            {
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetColor("_Color", SilverTint);

                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Shield of Frost: silver tint failed, keeping original color: {ex}");
            }
        }
    }
}
