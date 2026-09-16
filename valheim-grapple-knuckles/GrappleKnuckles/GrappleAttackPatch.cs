using HarmonyLib;

namespace GrappleKnuckles
{
    // TODO: This patch is a placeholder until the vanilla Grappling Hook's
    // launch method is confirmed against a decompile of the actual game
    // assembly (Assembly-CSharp.dll) for 1.0.7. Do NOT ship this as-is.
    //
    // What we need to confirm locally (ILSpy/dnSpy on your own Valheim
    // install, or from an existing open-source mod that already patches
    // the hook):
    //   1. The vanilla Grappling Hook's actual prefab name (NOT "FistGold").
    //   2. Whether the hook launch logic lives on a custom ItemDrop.ItemData
    //      subtype / component (e.g. something exposing a "Fire"/"Launch"
    //      method), or is driven entirely through Attack + an
    //      AttackData-style config with a specific "attack type" enum value
    //      that Humanoid.StartAttack dispatches on.
    //   3. The exact signature of the method that fires the grapple raycast
    //      and pulls the player (so we can call it directly instead of
    //      reimplementing physics).
    //
    // Once confirmed, this class should:
    //   - [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    //     (or wherever secondary-attack dispatch actually happens)
    //   - Check if the current weapon prefab name is
    //     GrappleKnucklesPlugin.ClonedItemPrefabName and the attack slot is
    //     "secondary"
    //   - If so, short-circuit vanilla knucklechains special-move logic and
    //     invoke the vanilla hook's launch method/component instead
    //     (ideally by reusing the vanilla Hook item's own component so we
    //     get its raycast + pull-toward-anchor + rope rendering for free).
    internal static class GrappleAttackPatch
    {
        // Filled in once the real target method is confirmed.
    }
}
