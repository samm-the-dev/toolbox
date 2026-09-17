using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Two mage-flavored melee weapons, alongside Grapple Knuckles and the
    // Fenris Mage armor: a Fire Dagger (cloned directly from Nord Dagger -
    // dropped the earlier Skoll-and-Hati dual-wield/reskin idea, per
    // explicit direction, so no mesh-swap risk here) and a Lightning Sword
    // (cloned from Nord Sword). The frost half of the original fire/frost
    // dagger pair idea moved to a separate magic shield instead (see
    // ShieldOfFrost.cs, once built). Each weapon fires a small Eitr-costed
    // bolt as its secondary attack, reusing a real vanilla staff's
    // projectile instead of reimplementing spell physics/VFX, same approach
    // as Grapple Knuckles' hook launch.
    //
    // Confirmed facts this relies on:
    //   - KnifeGold = "Nord Dagger", SwordGold = "Nord Sword" - real prefabs.
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
    internal static class ElementalWeapons
    {
        public const string DaggerSourcePrefabName = "KnifeGold"; // Nord Dagger
        public const string FireDaggerPrefabName = "KnifeGold_Fire";

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
                    new RequirementConfig { Item = DaggerSourcePrefabName, Amount = 2 },
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

            var clonedItem = new CustomItem(FireDaggerPrefabName, DaggerSourcePrefabName, itemConfig);
            ItemManager.Instance.AddItem(clonedItem);

            clonedItem.ItemDrop.m_itemData.m_shared.m_damages.m_fire += DaggerElementalDamageBonus;

            Logger.LogInfo($"Cloned {DaggerSourcePrefabName} -> {FireDaggerPrefabName}");
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
    }
}
