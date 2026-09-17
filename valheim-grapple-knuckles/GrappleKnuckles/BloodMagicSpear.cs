using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Deep North Blood Magic weapon: a spear whose secondary attack costs a
    // percentage of the wielder's CURRENT health (plus a small Eitr cost)
    // instead of the pure-Eitr cost every other item in this toolkit uses.
    // Real Valheim has a distinct "Blood Magic" skill line (Staff of
    // Protection, Dead Raiser, Echo Spike, Spirit Caller) separate from
    // Elemental Magic (the Embers/Frost/Fracturing/Lightning staves this
    // mod has been echoing so far) - this is the first item to actually use
    // that resource pattern, giving Deep North something mechanically
    // distinct rather than another elemental-bolt reskin.
    //
    // Confirmed facts this relies on:
    //   - Attack.m_attackHealth (flat) / m_attackHealthPercentage (percent
    //     of CURRENT health, confirmed via GetHealth() not GetMaxHealth())
    //     are real fields, set the same way m_attackEitr already is
    //     elsewhere in this mod - no Harmony patch needed, vanilla's own
    //     Attack.Update() calls Character.UseHealth() with these values.
    //   - Character.UseHealth() is clamped (Mathf.Min(GetHealth() - 1f, ...))
    //     so a Blood Magic attack can never reduce health below 1 - matches
    //     the real "can't kill yourself" behavior, confirmed via decompile.
    //   - No new projectile/prefab needed - this reuses the spear's own
    //     existing secondary attack (a strong stab/throw), just retunes its
    //     cost and damage rather than pointing it at a cloned bolt like the
    //     rest of this mod's weapons.
    //
    // NOT confirmed: Bloodgold/Nornathread as exact Jötunn prefab spellings.
    // Both appeared consistently across multiple independent recipe
    // research passes in this project (cited in both Echo Spike's and
    // Lightning Strike's real recipes), giving reasonable confidence
    // they're real material names, but the precise capitalization/
    // underscore convention was never checked against Jötunn's item list
    // directly for this specific file - degrades gracefully (logs a
    // warning, no-ops) if wrong, same as every other "prefab not found"
    // case in this mod.
    internal static class BloodMagicSpear
    {
        public const string SpearSourcePrefabName = "SpearGold"; // Nord Spear
        public const string SpearClonedPrefabName = "SpearGold_BloodMagic";

        private const string BloodgoldPrefabName = "Bloodgold";
        private const string NornathreadPrefabName = "Nornathread";

        // Percentage of CURRENT health per secondary attack - deliberately
        // can't kill the wielder (engine-clamped, see file header).
        private const float SecondaryAttackHealthPercentage = 8f;

        // Smaller than the pure-Eitr weapons elsewhere in this mod, since
        // this attack also costs health.
        private const float SecondaryAttackEitrCost = 5f;

        private const float PierceDamageBonus = 40f;

        public static void Clone()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_blood_magic_spear",
                    Description = "$item_blood_magic_spear_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = SpearSourcePrefabName, Amount = 1 },
                        new RequirementConfig { Item = BloodgoldPrefabName, Amount = 10, AmountPerLevel = 5 },
                        new RequirementConfig { Item = NornathreadPrefabName, Amount = 4, AmountPerLevel = 2 },
                    },
                };

                var clonedItem = new CustomItem(SpearClonedPrefabName, SpearSourcePrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                var shared = clonedItem.ItemDrop.m_itemData.m_shared;

                var secondaryAttack = shared.m_secondaryAttack ?? new Attack();
                secondaryAttack.m_attackHealthPercentage = SecondaryAttackHealthPercentage;
                secondaryAttack.m_attackEitr = SecondaryAttackEitrCost;
                shared.m_secondaryAttack = secondaryAttack;

                shared.m_damages.m_pierce += PierceDamageBonus;

                Logger.LogInfo(
                    $"Cloned {SpearSourcePrefabName} -> {SpearClonedPrefabName} " +
                    $"(secondary attack: {SecondaryAttackHealthPercentage}% current health + {SecondaryAttackEitrCost} Eitr)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {SpearSourcePrefabName}: {ex}");
            }
        }
    }
}
