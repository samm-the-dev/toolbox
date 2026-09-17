using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Two mage-flavored melee weapons, alongside Grapple Knuckles and the
    // Fenris Mage armor: a single Fire Dagger (cloned from Skoll and Hati
    // for its dual-blade animation, confirmed to be a single TwoHandedWeapon
    // item - vanilla has no true off-hand dual-wield slot - reskinned
    // toward Nord Dagger's appearance), and a Lightning Sword (cloned from
    // Nord Sword). The frost half of the original fire/frost dagger pair
    // idea moved to a separate magic shield instead (see ShieldOfFrost.cs);
    // this is a single dual-wield weapon, not a pair. Each fires a small
    // Eitr-costed bolt as its secondary attack, reusing a real vanilla
    // staff's projectile instead of reimplementing spell physics/VFX, same
    // approach as Grapple Knuckles' hook launch.
    //
    // Confirmed facts this relies on:
    //   - KnifeGold = "Nord Dagger", SwordGold = "Nord Sword",
    //     KnifeSkollAndHati = "Skoll and Hati" (a single TwoHandedWeapon
    //     item with its own dual-blade swing animation - there is no
    //     vanilla off-hand equip slot to build a true two-item dual-wield
    //     system on).
    //   - OrbFrostFire = "Frostfire Essence", OrbThunderBlood =
    //     "Thunderblood Essence", Eitr = "Refined Eitr" - real material
    //     prefab names.
    //   - RequirementConfig.AmountPerLevel (Jötunn) maps directly to
    //     vanilla's own Piece.Requirement.m_amountPerLevel - upgrade cost
    //     scaling is native vanilla behavior, not custom logic.
    //   - Elemental damage (SharedData.m_damages.m_fire etc.) inherently
    //     procs the matching vanilla status effect (burning) once that
    //     damage type is > 0 - no separate on-hit effect wiring needed
    //     (same mechanism the real Frostfire-enchanted weapons use).
    //   - Attack.m_attackEitr (confirmed field, alongside m_attackStamina)
    //     is how a staff-style Eitr cost attaches to an attack.
    //   - Mesh reskinning (swap MeshFilter.sharedMesh / SkinnedMeshRenderer
    //     .sharedMesh on a clone's child renderers) is a real, precedented
    //     technique (the CustomMeshes mod does exactly this) and confirmed
    //     decoupled from animation - but whether Skoll and Hati's blades are
    //     skinned (bone-rigged) or static meshes was NOT confirmed, so this
    //     is implemented defensively (try/catch, logged, falls back to the
    //     clone's original appearance on any mismatch) and flagged in
    //     CLAUDE.md as the one part most likely to need live-game iteration.
    internal static class ElementalWeapons
    {
        public const string DualBladeAnimationSourcePrefabName = "KnifeSkollAndHati";
        public const string DaggerMeshSourcePrefabName = "KnifeGold"; // Nord Dagger
        public const string FireDaggerPrefabName = "KnifeSkollAndHati_Fire";

        public const string SwordSourcePrefabName = "SwordGold"; // Nord Sword
        public const string LightningSwordPrefabName = "SwordGold_Lightning";

        private const string FrostfireEssencePrefabName = "OrbFrostFire";
        private const string ThunderbloodEssencePrefabName = "OrbThunderBlood";
        private const string RefinedEitrPrefabName = "Eitr";

        public const string FireBoltSourcePrefabName = "staff_fireball_projectile"; // Staff of Embers
        public const string LightningBoltSourcePrefabName = "staff_lightning_projectile"; // Dundr

        public const string FireBoltClonedPrefabName = "Bolt_Fire_Dagger";
        public const string LightningBoltClonedPrefabName = "Bolt_Lightning_Sword";

        // Placeholder - the real vanilla essence cost to enchant Knucklechains
        // into Frostfire/Thunderblood couldn't be found (recipe requirement
        // amounts are recipe-asset data, not decompiled C#, and the wikis
        // that would have them are blocked in this research environment).
        // "Double essences" per explicit request, relative to this guess -
        // correct BaseVanillaEnchantEssenceCost against your own game first.
        private const int BaseVanillaEnchantEssenceCost = 2;
        private const int UpgradeEssenceCost = BaseVanillaEnchantEssenceCost * 2;
        private const int UpgradeRefinedEitrCost = 2;

        // "Smaller projectiles" per explicit request.
        private const float BoltScale = 0.5f;

        private const float DaggerElementalDamageBonus = 20f;
        private const float SwordLightningDamageBonus = 20f;

        public static GameObject FireBoltProjectile { get; private set; }
        public static GameObject LightningBoltProjectile { get; private set; }

        public static void Init()
        {
            ItemManager.OnItemsRegistered += CloneWeapons;
            PrefabManager.OnVanillaPrefabsAvailable += CloneBoltProjectiles;
        }

        public static void Dispose()
        {
            ItemManager.OnItemsRegistered -= CloneWeapons;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneBoltProjectiles;
        }

        private static void CloneBoltProjectiles()
        {
            FireBoltProjectile ??= CloneAndScaleProjectile(FireBoltClonedPrefabName, FireBoltSourcePrefabName);
            LightningBoltProjectile ??= CloneAndScaleProjectile(LightningBoltClonedPrefabName, LightningBoltSourcePrefabName);
        }

        private static GameObject CloneAndScaleProjectile(string clonedName, string sourceName)
        {
            try
            {
                var clone = PrefabManager.Instance.CreateClonedPrefab(clonedName, sourceName);
                if (clone != null)
                {
                    clone.transform.localScale *= BoltScale;
                    Logger.LogInfo($"Cloned {sourceName} -> {clonedName} (scaled {BoltScale:P0})");
                }

                return clone;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone projectile {sourceName}: {ex}");
                return null;
            }
        }

        private static void CloneWeapons()
        {
            try
            {
                CloneDagger();
                CloneSword();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone elemental weapons: {ex}");
            }
        }

        private static void CloneDagger()
        {
            var itemConfig = new ItemConfig
            {
                Name = "$item_fire_dagger",
                Description = "$item_fire_dagger_description",
                CraftingStation = "blackforge",
                Requirements = new[]
                {
                    new RequirementConfig { Item = DaggerMeshSourcePrefabName, Amount = 2 },
                    new RequirementConfig
                    {
                        Item = FrostfireEssencePrefabName,
                        Amount = 1,
                        AmountPerLevel = UpgradeEssenceCost,
                    },
                    new RequirementConfig
                    {
                        Item = RefinedEitrPrefabName,
                        Amount = 0,
                        AmountPerLevel = UpgradeRefinedEitrCost,
                    },
                },
            };

            var clonedItem = new CustomItem(FireDaggerPrefabName, DualBladeAnimationSourcePrefabName, itemConfig);
            ItemManager.Instance.AddItem(clonedItem);

            clonedItem.ItemDrop.m_itemData.m_shared.m_damages.m_fire += DaggerElementalDamageBonus;

            TryReskinMesh(clonedItem.ItemDrop.gameObject, FireDaggerPrefabName);

            Logger.LogInfo($"Cloned {DualBladeAnimationSourcePrefabName} -> {FireDaggerPrefabName}");
        }

        private static void CloneSword()
        {
            var itemConfig = new ItemConfig
            {
                Name = "$item_lightning_sword",
                Description = "$item_lightning_sword_description",
                CraftingStation = "blackforge",
                Requirements = new[]
                {
                    new RequirementConfig { Item = SwordSourcePrefabName, Amount = 1 },
                    new RequirementConfig
                    {
                        Item = ThunderbloodEssencePrefabName,
                        Amount = 2,
                        AmountPerLevel = UpgradeEssenceCost,
                    },
                    new RequirementConfig
                    {
                        Item = RefinedEitrPrefabName,
                        Amount = 0,
                        AmountPerLevel = UpgradeRefinedEitrCost,
                    },
                },
            };

            var clonedItem = new CustomItem(LightningSwordPrefabName, SwordSourcePrefabName, itemConfig);
            ItemManager.Instance.AddItem(clonedItem);

            clonedItem.ItemDrop.m_itemData.m_shared.m_damages.m_lightning += SwordLightningDamageBonus;

            Logger.LogInfo($"Cloned {SwordSourcePrefabName} -> {LightningSwordPrefabName}");
        }

        // Best-effort reskin: swap the clone's mesh/material for the Nord
        // Dagger's, matched by renderer order. Whether Skoll and Hati's
        // blades are skinned (bone-rigged) meshes - which could distort if
        // the replacement isn't rigged to a compatible skeleton - was never
        // confirmed; this degrades gracefully to the clone's original
        // appearance on any mismatch or exception rather than breaking the
        // item.
        private static void TryReskinMesh(GameObject target, string context)
        {
            var meshSource = PrefabManager.Instance.GetPrefab(DaggerMeshSourcePrefabName);
            if (meshSource == null)
            {
                Logger.LogWarning($"{context}: mesh source '{DaggerMeshSourcePrefabName}' not found; keeping original appearance.");
                return;
            }

            try
            {
                var targetFilters = target.GetComponentsInChildren<MeshFilter>(true);
                var sourceFilters = meshSource.GetComponentsInChildren<MeshFilter>(true);
                var targetSkinned = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var sourceSkinned = meshSource.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                var swapped = 0;
                foreach (var (targetFilter, sourceFilter) in targetFilters.Zip(sourceFilters, (a, b) => (a, b)))
                {
                    targetFilter.sharedMesh = sourceFilter.sharedMesh;
                    var targetRenderer = targetFilter.GetComponent<MeshRenderer>();
                    var sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                    if (targetRenderer != null && sourceRenderer != null)
                    {
                        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    }

                    swapped++;
                }

                foreach (var (targetSkin, sourceSkin) in targetSkinned.Zip(sourceSkinned, (a, b) => (a, b)))
                {
                    targetSkin.sharedMesh = sourceSkin.sharedMesh;
                    targetSkin.sharedMaterials = sourceSkin.sharedMaterials;
                    swapped++;
                }

                if (targetFilters.Length != sourceFilters.Length || targetSkinned.Length != sourceSkinned.Length)
                {
                    Logger.LogWarning(
                        $"{context}: renderer count mismatch vs '{DaggerMeshSourcePrefabName}' " +
                        $"(MeshFilter {targetFilters.Length} vs {sourceFilters.Length}, " +
                        $"SkinnedMeshRenderer {targetSkinned.Length} vs {sourceSkinned.Length}) - " +
                        "reskin is partial/best-effort, verify appearance in-game.");
                }
                else
                {
                    Logger.LogInfo($"{context}: reskinned {swapped} renderer(s) to match '{DaggerMeshSourcePrefabName}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"{context}: mesh reskin failed, keeping original appearance: {ex}");
            }
        }
    }
}
