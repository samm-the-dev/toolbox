using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GrappleKnuckles
{
    // Deep North fast-mage hybrid, same pattern as AshlandsHybridArmor.cs:
    // clones the real Deep North mage armor "Caller"
    // (ArmorDeepNorthMageChest + ArmorDeepNorthMagelegs, confirmed real
    // prefabs) and trades a slice of its own native armor for movement
    // speed, keeping its real Eitr regen intact rather than rebuilding it.
    //
    // Confirmed facts this relies on (see FenrisMageArmor.cs for the
    // underlying set-bonus/StatusEffect mechanism, unchanged here):
    //   - ArmorDeepNorthMageChest / ArmorDeepNorthMagelegs are real
    //     confirmed prefabs (Caller - trades defense for Eitr regen versus
    //     Deep North's other two armor lines, Protector/heavy and
    //     Vanguard/medium, per community sources).
    //   - Same as the Ashlands version: m_equipStatusEffect is deliberately
    //     left untouched so Caller's own real Eitr regen carries over via
    //     the clone automatically - never independently verified against
    //     Caller's own source data, confirm in-game.
    internal static class DeepNorthHybridArmor
    {
        public const string ChestSourcePrefabName = "ArmorDeepNorthMageChest"; // Caller
        public const string LegsSourcePrefabName = "ArmorDeepNorthMagelegs";
        public const string ChestClonedPrefabName = "ArmorDeepNorthMageChest_Hybrid";
        public const string LegsClonedPrefabName = "ArmorDeepNorthMagelegs_Hybrid";

        private const string SetName = "DeepNorthHybridSet";
        private const int SetSize = 2;

        // Caller is already the lightest/most defense-light of Deep North's
        // three armor lines (~22 armor/piece per community sources, not
        // primary-confirmed) - a modest further trade, not a big rework.
        private const float ArmorScale = 0.85f;

        private const float MovementSpeedBonusPerPiece = 0.05f; // +5% each, +10% total

        private const float SetStaminaRegenMultiplier = 1.25f; // +25% when both pieces worn

        public static void Clone()
        {
            try
            {
                var setStatusEffect = CreateAndRegisterStatusEffect(
                    "SE_DeepNorthHybridSet",
                    se => se.m_staminaRegenMultiplier = SetStaminaRegenMultiplier);

                CloneArmorPiece(
                    ChestClonedPrefabName, ChestSourcePrefabName,
                    "$item_deepnorthhybrid_chest", "$item_deepnorthhybrid_chest_description",
                    setStatusEffect);

                CloneArmorPiece(
                    LegsClonedPrefabName, LegsSourcePrefabName,
                    "$item_deepnorthhybrid_legs", "$item_deepnorthhybrid_legs_description",
                    setStatusEffect);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone Deep North hybrid armor: {ex}");
            }
        }

        private static void CloneArmorPiece(
            string clonedName, string sourceName, string nameToken, string descriptionToken,
            StatusEffect setStatusEffect)
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

            shared.m_movementModifier = MovementSpeedBonusPerPiece;

            // m_equipStatusEffect deliberately left untouched - see file header.

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

            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, fixReference: false));

            return effect;
        }
    }
}
