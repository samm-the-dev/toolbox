using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GrappleKnuckles
{
    // Fast-mage hybrid armor: clones vanilla Fenris armor (ArmorFenringChest
    // + ArmorFenringLegs - the only two real Fenris prefabs confirmed to
    // exist; wiki claims of a third Hood piece don't correspond to anything
    // in the confirmed vanilla prefab list) and scales it up toward
    // Mistlands power level. Each piece trades some Eitr regen (versus the
    // real Mistlands "Eitr-weave" mage set) for a bit of movement speed;
    // wearing both grants a stamina regen set bonus, so the build can hold
    // its own in melee too rather than being purely a kiting caster.
    //
    // Confirmed facts this relies on:
    //   - SharedData.m_setName / m_setSize / m_setStatusEffect drive full-set
    //     bonuses entirely in vanilla code (Humanoid.UpdateEquipmentStatusEffects) -
    //     no Harmony patch needed, just matching fields on both pieces.
    //   - StatusEffect (SE_Stats included) is ScriptableObject-derived;
    //     ScriptableObject.CreateInstance<SE_Stats>() + Jötunn's
    //     CustomStatusEffect + ItemManager.Instance.AddStatusEffect(...) is
    //     the confirmed, real-mod-precedented way to register a brand new
    //     one (see aedenthorn/ValheimMods' CustomArmorStats plugin).
    //   - SE_Stats.m_eitrRegenMultiplier and m_staminaRegenMultiplier are
    //     confirmed real fields.
    //   - Real Fenris/Padded/Carapace armor numbers were only community-
    //     sourced, never primary-confirmed (Jötunn's item list has no armor/
    //     weight columns at all), so armor/weight here are scaled relative
    //     to whatever the clone actually inherits from vanilla Fenris (a
    //     multiplier, not a hardcoded absolute), so they stay correct
    //     regardless of what the real baseline turns out to be.
    internal static class FenrisMageArmor
    {
        public const string ChestSourcePrefabName = "ArmorFenringChest";
        public const string LegsSourcePrefabName = "ArmorFenringLegs";
        public const string ChestClonedPrefabName = "ArmorFenringChest_Mage";
        public const string LegsClonedPrefabName = "ArmorFenringLegs_Mage";

        private const string SetName = "FenrisMageSet";
        private const int SetSize = 2;

        // "Scale up toward Mistlands power level" - relative to whatever
        // vanilla Fenris actually has, since the real absolute numbers
        // weren't primary-source confirmed.
        private const float ArmorScale = 1.6f;
        private const float WeightScale = 1.2f;

        // Real Mistlands "Eitr-weave" set grants per-piece Eitr regen
        // (community-sourced: hood +20%, robe +40%, trousers +40%); ours is
        // deliberately less since this is a trade-off for speed, not a
        // straight mage-armor clone.
        private const float ChestEitrRegenMultiplier = 1.2f; // +20%
        private const float LegsEitrRegenMultiplier = 1.2f; // +20%

        private const float MovementSpeedBonusPerPiece = 0.05f; // +5% each, +10% total

        // Supports melee with "some proficiency" per design intent - stamina
        // regen covers both dodging and swinging, not just one or the other.
        private const float SetStaminaRegenMultiplier = 1.25f; // +25% when both pieces worn

        public static void Clone()
        {
            try
            {
                var setStatusEffect = CreateAndRegisterStatusEffect(
                    "SE_FenrisMageSet",
                    se => se.m_staminaRegenMultiplier = SetStaminaRegenMultiplier);

                CloneArmorPiece(
                    ChestClonedPrefabName, ChestSourcePrefabName,
                    "$item_fenrismage_chest", "$item_fenrismage_chest_description",
                    ChestEitrRegenMultiplier, setStatusEffect);

                CloneArmorPiece(
                    LegsClonedPrefabName, LegsSourcePrefabName,
                    "$item_fenrismage_legs", "$item_fenrismage_legs_description",
                    LegsEitrRegenMultiplier, setStatusEffect);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone Fenris mage armor: {ex}");
            }
        }

        private static void CloneArmorPiece(
            string clonedName, string sourceName, string nameToken, string descriptionToken,
            float eitrRegenMultiplier, StatusEffect setStatusEffect)
        {
            var itemConfig = new ItemConfig
            {
                Name = nameToken,
                Description = descriptionToken,
                CraftingStation = "blackforge",
                Requirements = new[]
                {
                    new RequirementConfig { Item = sourceName, Amount = 1 },
                },
            };

            var clonedItem = new CustomItem(clonedName, sourceName, itemConfig);
            ItemManager.Instance.AddItem(clonedItem);

            var shared = clonedItem.ItemDrop.m_itemData.m_shared;

            shared.m_armor *= ArmorScale;
            shared.m_armorPerLevel *= ArmorScale;
            shared.m_weight *= WeightScale;

            shared.m_movementModifier = MovementSpeedBonusPerPiece;

            shared.m_equipStatusEffect = CreateAndRegisterStatusEffect(
                $"SE_{clonedName}_EitrRegen",
                se => se.m_eitrRegenMultiplier = eitrRegenMultiplier);

            // Detach from vanilla Fenris's own set (which we'd otherwise
            // inherit via the clone) into our own independent set, so these
            // pieces don't combine with real Fenris armor to trigger the
            // vanilla Fenris Blessing, and vice versa.
            shared.m_setName = SetName;
            shared.m_setSize = SetSize;
            shared.m_setStatusEffect = setStatusEffect;

            Jotunn.Logger.LogInfo($"Cloned {sourceName} -> {clonedName}");
        }

        private static SE_Stats CreateAndRegisterStatusEffect(string effectName, Action<SE_Stats> configure)
        {
            var effect = ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = effectName;
            configure(effect);

            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect));

            return effect;
        }
    }
}
