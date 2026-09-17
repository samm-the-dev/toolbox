using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Two mage-flavored melee weapons, alongside Grapple Knuckles and the
    // Fenris Mage armor: a Fire Dagger (cloned from Nord Dagger for its
    // model/mechanics - no confirmed Mistlands-native dagger exists to clone
    // instead) re-tiered to Mistlands, and a Lightning Sword (cloned from
    // Nord Sword, same reasoning) re-tiered to Ashlands. Both fire a small
    // Eitr-costed bolt as their secondary attack, reusing a real vanilla
    // staff's projectile instead of reimplementing spell physics/VFX, same
    // approach as Grapple Knuckles' hook launch.
    //
    // Tier placement, per explicit direction after a full survey of real
    // vanilla elemental weapons/staves:
    //   - Fire Dagger -> Mistlands, priced with Surtling Core + Refined Eitr
    //     (confirmed real materials for "Staff of Embers", the Mistlands
    //     fire staff - this item is a melee echo of that staff, not a
    //     replica of its exact recipe/cost).
    //   - Lightning Sword -> Ashlands, priced with Flametal + Bloodstone +
    //     Charred Bone (confirmed real Ashlands one-handed-weapon materials,
    //     via Dyrnwyn/Nidhögg's real recipes as reference templates - not
    //     copied exactly, since those are specific unique/named items).
    //     Its secondary attack already reused Dundr's (the Ashlands
    //     lightning staff) actual projectile from the start - what changed
    //     here is tuning it faster and weaker than a full Dundr cast, per
    //     explicit direction, rather than the wiring itself.
    //
    // Confirmed facts this relies on:
    //   - KnifeGold = "Nord Dagger", SwordGold = "Nord Sword" - real prefabs.
    //   - SurtlingCore, FreezeGland, Flametal, GemstoneRed ("Bloodstone"),
    //     CharredBone, Eitr ("Refined Eitr") - real material prefab names.
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

        private const string SurtlingCorePrefabName = "SurtlingCore";
        private const string FreezeGlandPrefabName = "FreezeGland";
        // "Flametal" is the pre-Ashlands-update legacy prefab ("Ancient
        // Metal" in-game, $item_flametal_old) - use "FlametalNew"
        // ($item_flametal), the real current item. See GrappleKnucklesPlugin.cs.
        private const string FlametalPrefabName = "FlametalNew";
        private const string BloodstonePrefabName = "GemstoneRed"; // "Bloodstone"
        private const string CharredBonePrefabName = "CharredBone";
        private const string RefinedEitrPrefabName = "Eitr";

        public const string FireBoltSourcePrefabName = "staff_fireball_projectile"; // Staff of Embers
        public const string LightningBoltSourcePrefabName = "staff_lightning_projectile"; // Dundr

        public const string FireBoltClonedPrefabName = "Bolt_Fire_Dagger";
        public const string LightningBoltClonedPrefabName = "Bolt_Lightning_Sword";

        // "Smaller projectiles" per explicit request.
        private const float BoltScale = 0.5f;

        // Dundr's own bolt is a full staff-cast payload; the sword's version
        // is deliberately weaker (and faster - see ElementalWeaponAttackPatch),
        // per explicit "faster and weaker" direction.
        private const float LightningBoltDamageMultiplier = 0.5f;

        private const float DaggerElementalDamageBonus = 20f;
        private const float SwordLightningDamageBonus = 20f;

        public static GameObject FireBoltProjectile { get; private set; }
        public static GameObject LightningBoltProjectile { get; private set; }

        public static void Init()
        {
            // Item cloning uses OnVanillaPrefabsAvailable, not
            // ItemManager.OnItemsRegistered - see GrappleKnucklesPlugin.cs.
            PrefabManager.OnVanillaPrefabsAvailable += CloneWeapons;
            PrefabManager.OnVanillaPrefabsAvailable += CloneBoltProjectiles;
        }

        public static void Dispose()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CloneWeapons;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneBoltProjectiles;
        }

        private static void CloneBoltProjectiles()
        {
            FireBoltProjectile ??= CloneAndScaleProjectile(FireBoltClonedPrefabName, FireBoltSourcePrefabName);

            if (LightningBoltProjectile == null)
            {
                LightningBoltProjectile = CloneAndScaleProjectile(LightningBoltClonedPrefabName, LightningBoltSourcePrefabName);

                var projectileComponent = LightningBoltProjectile?.GetComponent<Projectile>();
                if (projectileComponent != null)
                {
                    projectileComponent.m_damage.m_lightning *= LightningBoltDamageMultiplier;
                }
            }
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
                    new RequirementConfig { Item = SurtlingCorePrefabName, Amount = 4, AmountPerLevel = 4 },
                    new RequirementConfig { Item = RefinedEitrPrefabName, Amount = 10, AmountPerLevel = 5 },
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
                    new RequirementConfig { Item = FlametalPrefabName, Amount = 15, AmountPerLevel = 10 },
                    new RequirementConfig { Item = BloodstonePrefabName, Amount = 1, AmountPerLevel = 1 },
                    new RequirementConfig { Item = CharredBonePrefabName, Amount = 3, AmountPerLevel = 2 },
                },
            };

            var clonedItem = new CustomItem(LightningSwordPrefabName, SwordSourcePrefabName, itemConfig);
            ItemManager.Instance.AddItem(clonedItem);

            clonedItem.ItemDrop.m_itemData.m_shared.m_damages.m_lightning += SwordLightningDamageBonus;

            Logger.LogInfo($"Cloned {SwordSourcePrefabName} -> {LightningSwordPrefabName}");
        }
    }
}
