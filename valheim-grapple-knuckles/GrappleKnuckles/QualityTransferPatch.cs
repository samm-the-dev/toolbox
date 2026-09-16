using System.Linq;
using HarmonyLib;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Vanilla's own quality-carry mechanism (InventoryGui.m_craftUpgradeItem)
    // only kicks in when re-crafting a recipe whose output item type matches
    // one the player already owns - the in-place "upgrade at the forge" case.
    // Since our recipe consumes a *different* item (FistGold) to produce a
    // *different* output (FistGold_Grapple), that mechanism never triggers,
    // and the crafted item would otherwise always start at quality 1.
    //
    // Confirmed from a decompile of InventoryGui.DoCrafting(Player): the
    // method reads m_craftRecipe (the Recipe being crafted) and, after
    // consuming resources, creates the output via
    // player.GetInventory().AddItem(prefabName, amount, quality, ...).
    // There's no hook to intercept that local `quality` value directly, so
    // instead we:
    //   1. Prefix: if the recipe being crafted is ours, find the FistGold
    //      instance in the player's inventory (highest quality if they have
    //      more than one - vanilla doesn't expose which specific instance
    //      ConsumeResources will remove) and remember its quality.
    //   2. Let the original method run normally (crafts at quality 1).
    //   3. Postfix: find the newly crafted Grapple Knuckles instance and set
    //      its quality to match what we captured.
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting", typeof(Player))]
    internal static class InventoryGui_DoCrafting_QualityTransferPatch
    {
        private static int? _capturedSourceQuality;

        private static void Prefix(InventoryGui __instance, Player player)
        {
            _capturedSourceQuality = null;

            var recipe = Traverse.Create(__instance).Field("m_craftRecipe").GetValue<Recipe>();
            if (recipe?.m_item == null || recipe.m_item.gameObject.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            var sourceItem = player.GetInventory().GetAllItems()
                .Where(item => item.m_dropPrefab != null && item.m_dropPrefab.name == GrappleKnucklesPlugin.SourceItemPrefabName)
                .OrderByDescending(item => item.m_quality)
                .FirstOrDefault();

            if (sourceItem != null)
            {
                _capturedSourceQuality = sourceItem.m_quality;
            }
        }

        private static void Postfix(Player player)
        {
            if (_capturedSourceQuality is not int quality)
            {
                return;
            }

            _capturedSourceQuality = null;

            var craftedItem = player.GetInventory().GetAllItems()
                .Where(item => item.m_dropPrefab != null && item.m_dropPrefab.name == GrappleKnucklesPlugin.ClonedItemPrefabName)
                .OrderByDescending(item => item.m_quality)
                .FirstOrDefault();

            if (craftedItem == null)
            {
                Logger.LogWarning("Crafted Grapple Knuckles item not found in inventory; could not carry over quality.");
                return;
            }

            craftedItem.m_quality = quality;

            Logger.LogInfo($"Carried quality {quality} from {GrappleKnucklesPlugin.SourceItemPrefabName} onto crafted Grapple Knuckles.");
        }
    }
}
