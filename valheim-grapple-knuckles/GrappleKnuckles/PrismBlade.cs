using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // "Prism Blade": an endgame Deep North weapon whose elemental damage
    // type cycles between fire (default), frost, lightning, and poison on
    // secondary attack use. See PrismBladePatches.cs for the mechanics;
    // this file just clones the item.
    //
    // Confirmed facts this relies on (see PrismBladePatches.cs for the
    // combat-mechanic-specific ones):
    //   - Same PrefabManager/ItemManager CustomItem patterns already used
    //     throughout this mod.
    internal static class PrismBlade
    {
        public const string SourcePrefabName = "THSwordGold"; // Nord Greatsword
        public const string ClonedPrefabName = "THSwordGold_Prism";

        private const string BloodgoldPrefabName = "Bloodgold";
        private const string NornathreadPrefabName = "Nornathread";

        public static void Clone()
        {
            try
            {
                var itemConfig = new ItemConfig
                {
                    Name = "$item_prism_blade",
                    Description = "$item_prism_blade_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = SourcePrefabName, Amount = 1 },
                        new RequirementConfig { Item = BloodgoldPrefabName, Amount = 15, AmountPerLevel = 8 },
                        new RequirementConfig { Item = NornathreadPrefabName, Amount = 6, AmountPerLevel = 3 },
                    },
                };

                var clonedItem = new CustomItem(ClonedPrefabName, SourcePrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                // Fire by default, per explicit direction - PrismBladePatches
                // reads this each swing and cycles it on secondary attack.
                clonedItem.ItemDrop.m_itemData.m_variant = (int)PrismBladePatches.Element.Fire;

                Logger.LogInfo($"Cloned {SourcePrefabName} -> {ClonedPrefabName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clone {SourcePrefabName}: {ex}");
            }
        }
    }
}
