using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GrappleKnuckles
{
    // Ashlands fast-mage hybrid, mirroring FenrisMageArmor.cs's pattern but
    // approached from the opposite direction: Fenris started as a light,
    // non-mage set and gained partial Eitr regen; this clones the real
    // Ashlands mage armor "Embla" (ArmorMageChest_Ashlands +
    // ArmorMageLegs_Ashlands, confirmed real prefabs) and trades a slice of
    // its own native armor for movement speed instead, keeping Embla's real
    // Eitr regen intact (see below) rather than rebuilding it from scratch.
    //
    // Confirmed facts this relies on (see FenrisMageArmor.cs for the
    // underlying set-bonus/StatusEffect mechanism, unchanged here):
    //   - ArmorMageChest_Ashlands / ArmorMageLegs_Ashlands are real
    //     confirmed prefabs (Embla, the Ashlands upgrade of the Mistlands
    //     "Eitr-weave" mage set).
    //   - We deliberately do NOT touch m_equipStatusEffect on the clone:
    //     Jötunn's clone inherits it from Embla automatically, and (by
    //     analogy with Eitr-weave's confirmed per-piece Eitr regen
    //     contributions) Embla's own Eitr regen almost certainly lives
    //     there too - untouched, so it should carry over for free. This
    //     assumption was never independently verified against Embla's own
    //     source data (Jötunn's item list has no stat fields at all) -
    //     confirm in-game that Eitr regen still shows on these pieces.
    internal static class AshlandsHybridArmor
    {
        public const string ChestSourcePrefabName = "ArmorMageChest_Ashlands"; // Embla
        public const string LegsSourcePrefabName = "ArmorMageLegs_Ashlands";
        public const string ChestClonedPrefabName = "ArmorMageChest_Ashlands_Hybrid";
        public const string LegsClonedPrefabName = "ArmorMageLegs_Ashlands_Hybrid";

        private const string SetName = "AshlandsHybridSet";
        private const int SetSize = 2;

        // Embla is already a real, balanced mage set (~75 armor total per
        // community sources, not primary-confirmed) - this is a modest
        // defense-for-speed trade, not a big rework like Fenris's power-tier
        // scale-up.
        private const float ArmorScale = 0.85f;

        private const float MovementSpeedBonusPerPiece = 0.05f; // +5% each, +10% total

        private const float SetStaminaRegenMultiplier = 1.25f; // +25% when both pieces worn

        public static void Clone()
        {
            try
            {
                var setStatusEffect = CreateAndRegisterStatusEffect(
                    "SE_AshlandsHybridSet",
                    se => se.m_staminaRegenMultiplier = SetStaminaRegenMultiplier);

                CloneArmorPiece(
                    ChestClonedPrefabName, ChestSourcePrefabName,
                    "$item_ashlandshybrid_chest", "$item_ashlandshybrid_chest_description",
                    setStatusEffect);

                CloneArmorPiece(
                    LegsClonedPrefabName, LegsSourcePrefabName,
                    "$item_ashlandshybrid_legs", "$item_ashlandshybrid_legs_description",
                    setStatusEffect);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone Ashlands hybrid armor: {ex}");
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

            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect));

            return effect;
        }
    }
}
