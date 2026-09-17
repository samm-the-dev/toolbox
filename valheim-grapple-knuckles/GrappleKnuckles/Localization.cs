using System.Collections.Generic;
using Jotunn.Managers;

namespace GrappleKnuckles
{
    // Covers every item token used across this mod, including items
    // currently held back from the test pass (GrappleKnucklesPlugin.cs) -
    // harmless to register tokens for items that aren't cloned yet, and
    // saves re-doing this per item as each gets re-enabled.
    internal static class Localization
    {
        public static void Init()
        {
            var localization = LocalizationManager.Instance.GetLocalization();
            localization.AddTranslation("English", new Dictionary<string, string>
            {
                ["$item_fistgold_grapple"] = "Grapple Knuckles",
                ["$item_fistgold_grapple_description"] = "Knucklechains reforged for Ashlands - the secondary attack fires a hook faster than the plain Mistlands version.",

                ["$item_mountain_spiritfire_axe"] = "Spiritfire Axe",
                ["$item_mountain_spiritfire_axe_description"] = "A Silver-tier axe wreathed in fire and spirit, no Eitr required.",

                ["$item_fenrismage_chest"] = "Fenris Mage Tunic",
                ["$item_fenrismage_chest_description"] = "Fenris hide reworked for a caster who still needs to swing a blade.",
                ["$item_fenrismage_legs"] = "Fenris Mage Leggings",
                ["$item_fenrismage_legs_description"] = "Fenris hide reworked for a caster who still needs to swing a blade.",

                ["$item_fire_dagger"] = "Fire Dagger",
                ["$item_fire_dagger_description"] = "A Nord dagger with an Eitr-fed ember bolt on its secondary attack.",

                ["$item_shield_of_frost"] = "Shield of Frost",
                ["$item_shield_of_frost_description"] = "Channels Eitr while raised; punishes a well-timed parry, and a broken guard with a burst of frost.",

                ["$item_ashlandshybrid_chest"] = "Ember Mage Robe",
                ["$item_ashlandshybrid_chest_description"] = "Embla's robes, a measure of armor traded for speed.",
                ["$item_ashlandshybrid_legs"] = "Ember Mage Leggings",
                ["$item_ashlandshybrid_legs_description"] = "Embla's leggings, a measure of armor traded for speed.",

                ["$item_lightning_sword"] = "Lightning Sword",
                ["$item_lightning_sword_description"] = "A Nord sword whose secondary attack looses a bolt - faster and weaker than Dundr's own.",

                ["$item_exploding_sledge"] = "Exploding Sledge",
                ["$item_exploding_sledge_description"] = "A Nord sledge whose secondary attack detonates a frost burst.",

                ["$item_deepnorthhybrid_chest"] = "Caller's Hybrid Robe",
                ["$item_deepnorthhybrid_chest_description"] = "Caller's robes, a measure of armor traded for speed.",
                ["$item_deepnorthhybrid_legs"] = "Caller's Hybrid Leggings",
                ["$item_deepnorthhybrid_legs_description"] = "Caller's leggings, a measure of armor traded for speed.",

                ["$item_prism_blade"] = "Prism Blade",
                ["$item_prism_blade_description"] = "A Nord greatsword whose element cycles fire, frost, lightning, and poison on secondary attack.",
            });
        }
    }
}
