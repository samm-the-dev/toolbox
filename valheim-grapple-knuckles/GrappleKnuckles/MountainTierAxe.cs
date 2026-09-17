using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Silver/Mountain-tier "proto" line: an innately fire+spirit axe, no
    // Eitr-cost spell at all - Eitr doesn't exist yet at this tier. Matches
    // the real vanilla "Frostner" pattern (MaceSilver: baked-in elemental
    // damage, fully craftable, no essence/enchant material needed), per
    // explicit direction after a dedicated survey confirmed:
    //   - No vanilla axe has ever had innate elemental damage.
    //   - Axes skip the Silver/Mountain tier entirely in vanilla (Iron ->
    //     Black Metal, no AxeSilver exists) - so there's no real base item
    //     to clone from for this slot.
    // Clones AxeIron instead (closest lower tier) and scales its damage up
    // toward Frostner's confirmed real power level (35 blunt/40 frost/20
    // spirit primary attack) via a relative multiplier on whatever AxeIron
    // actually has - same technique already used for Fenris Mage armor -
    // rather than a hardcoded absolute, so it stays correct regardless of
    // AxeIron's exact real baseline. Fire and spirit damage are then added
    // on top (spirit matches Silver tier's existing vanilla identity -
    // Frostner and the Silver Sword both have it; fire is the new addition
    // this item brings to the tier).
    //
    // Pairs with the existing Fenris Mage armor (already Mountain-tier) -
    // no new armor needed for this tier.
    //
    // NOT confirmed: CraftingStation = "forge" (by analogy with the
    // confirmed no-"piece_"-prefix "blackforge" naming) and the Silver/
    // Ancient Bark recipe quantities were never checked against a real
    // Frostner recipe - verify against your game.
    internal static class MountainTierAxe
    {
        public const string AxeSourcePrefabName = "AxeIron"; // closest lower tier - no AxeSilver exists in vanilla
        public const string AxeClonedPrefabName = "AxeIron_Spiritfire";

        private const string SilverPrefabName = "Silver";
        private const string AncientBarkPrefabName = "AncientBark";

        // Relative to whatever AxeIron actually has - see file header.
        private const float DamageScale = 1.8f;

        private const float FireDamageBonus = 20f;
        private const float SpiritDamageBonus = 20f;

        public static void Clone()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_mountain_spiritfire_axe",
                    Description = "$item_mountain_spiritfire_axe_description",
                    CraftingStation = "forge",
                    MinStationLevel = 3, // matches Frostner's real Forge lvl 3 requirement
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = SilverPrefabName, Amount = 10, AmountPerLevel = 5 },
                        new RequirementConfig { Item = AncientBarkPrefabName, Amount = 5, AmountPerLevel = 3 },
                    },
                };

                var clonedItem = new CustomItem(AxeClonedPrefabName, AxeSourcePrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                var shared = clonedItem.ItemDrop.m_itemData.m_shared;
                shared.m_damages.m_chop *= DamageScale;
                shared.m_damages.m_slash *= DamageScale;
                shared.m_damages.m_fire += FireDamageBonus;
                shared.m_damages.m_spirit += SpiritDamageBonus;

                Logger.LogInfo(
                    $"Cloned {AxeSourcePrefabName} -> {AxeClonedPrefabName} " +
                    $"({DamageScale:P0} base damage, +{FireDamageBonus} fire, +{SpiritDamageBonus} spirit)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {AxeSourcePrefabName}: {ex}");
            }
        }
    }
}
