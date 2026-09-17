using System;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Ashlands sledge: normal attacks stay as vanilla SledgeGold's own
    // cleave/AoE - no per-hit explosion, per explicit direction ("just the
    // usual sledge AoE"). The secondary attack triggers a single frost
    // explosion sized like one of Staff of Fracturing's splinter
    // sub-munitions (staff_clusterbombstaff_splinter_projectile - the
    // smaller child projectile the main clusterbomb spawns on impact, NOT
    // the main clusterbomb projectile itself, per explicit "don't make a
    // bunch of projectiles like that does" direction), slightly buffed
    // above a single splinter's own damage.
    //
    // Priced with Ashlands materials (Flametal + Charred Bone, the same
    // real-material family as Lightning Sword's re-tier - see
    // ElementalWeapons.cs) rather than the Deep North essence system
    // SledgeGold's own real enchant siblings use, since this is a distinct
    // new item, not a replica of vanilla's SledgeGold_FrostFire/
    // BloodLightning.
    internal static class ExplodingSledge
    {
        public const string SledgeSourcePrefabName = "SledgeGold"; // Nord Sledge
        public const string SledgeClonedPrefabName = "SledgeGold_Splinter";

        public const string SplinterSourcePrefabName = "staff_clusterbombstaff_splinter_projectile"; // Staff of Fracturing's sub-munition
        public const string SplinterClonedPrefabName = "Burst_ExplodingSledge";

        private const string FlametalPrefabName = "Flametal";
        private const string CharredBonePrefabName = "CharredBone";

        // "A little stronger than just one" splinter, per explicit direction.
        private const float SplinterDamageMultiplier = 1.3f;

        private const float SecondaryAttackStaminaCost = 15f;
        private const float SecondaryAttackReloadTime = 3f;

        public static GameObject SplinterProjectile { get; private set; }

        public static void Init()
        {
            ItemManager.OnItemsRegistered += CloneSledge;
            PrefabManager.OnVanillaPrefabsAvailable += CloneSplinter;
        }

        public static void Dispose()
        {
            ItemManager.OnItemsRegistered -= CloneSledge;
            PrefabManager.OnVanillaPrefabsAvailable -= CloneSplinter;
        }

        private static void CloneSplinter()
        {
            if (SplinterProjectile != null)
            {
                return;
            }

            try
            {
                SplinterProjectile = PrefabManager.Instance.CreateClonedPrefab(SplinterClonedPrefabName, SplinterSourcePrefabName);

                var projectileComponent = SplinterProjectile?.GetComponent<Projectile>();
                if (projectileComponent != null)
                {
                    projectileComponent.m_damage.m_frost *= SplinterDamageMultiplier;
                }

                Logger.LogInfo($"Cloned {SplinterSourcePrefabName} -> {SplinterClonedPrefabName} ({SplinterDamageMultiplier:P0} damage)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {SplinterSourcePrefabName}: {ex}");
            }
        }

        private static void CloneSledge()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_exploding_sledge",
                    Description = "$item_exploding_sledge_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = SledgeSourcePrefabName, Amount = 1 },
                        new RequirementConfig { Item = FlametalPrefabName, Amount = 15, AmountPerLevel = 10 },
                        new RequirementConfig { Item = CharredBonePrefabName, Amount = 5, AmountPerLevel = 3 },
                    },
                };

                var clonedItem = new CustomItem(SledgeClonedPrefabName, SledgeSourcePrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                Logger.LogInfo($"Cloned {SledgeSourcePrefabName} -> {SledgeClonedPrefabName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {SledgeSourcePrefabName}: {ex}");
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters))]
        private static class ObjectDB_UpdateRegisters_ExplodingSledgePatch
        {
            private static bool _applied;

            private static void Postfix(ObjectDB __instance)
            {
                if (_applied || __instance == null || SplinterProjectile == null)
                {
                    return;
                }

                var sledgePrefab = __instance.GetItemPrefab(SledgeClonedPrefabName);
                var itemData = sledgePrefab?.GetComponent<ItemDrop>()?.m_itemData;
                if (itemData?.m_shared == null)
                {
                    return;
                }

                var attack = itemData.m_shared.m_secondaryAttack ?? new Attack();

                attack.m_attackProjectile = SplinterProjectile;
                attack.m_attackStamina = SecondaryAttackStaminaCost;
                attack.m_reloadTime = SecondaryAttackReloadTime;

                if (itemData.m_shared.m_attack != null)
                {
                    attack.m_attackAnimation = itemData.m_shared.m_attack.m_attackAnimation;
                }

                itemData.m_shared.m_secondaryAttack = attack;

                _applied = true;

                Logger.LogInfo($"{SledgeClonedPrefabName}: wired secondary attack to a splinter burst projectile.");
            }
        }
    }
}
