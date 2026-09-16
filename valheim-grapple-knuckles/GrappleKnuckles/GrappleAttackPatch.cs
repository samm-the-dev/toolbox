using HarmonyLib;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Design: rather than patching Humanoid.StartAttack and reimplementing
    // the hook's raycast/pull/rope physics, we patch ObjectDB.UpdateRegisters
    // (the same extension point BetterGrapplingHook uses to tweak the real
    // Grappling Hook's stats) to copy the vanilla GrapplingHook's own
    // secondary-attack config onto our cloned item's secondary attack.
    //
    // Confirmed facts this relies on (see research notes / PR description):
    //   - The vanilla Grappling Hook prefab is "GrapplingHook", not FistGold.
    //   - ItemDrop.ItemData.SharedData.m_secondaryAttack (type Attack) is the
    //     Attack instance that actually fires the grapple: it holds
    //     m_attackProjectile, which is what spawns/drives the real
    //     GrapplingPoint component (rope LineRenderer, pull-toward-anchor,
    //     etc). By pointing our clone's secondary Attack at the same
    //     projectile and stamina/reload numbers, vanilla's own attack
    //     dispatch does the rest - no physics reimplementation needed.
    //   - We deliberately do NOT copy m_attackAnimation: Knucklechains'
    //     own secondary-attack punch animation is left in place, so the
    //     item still reads as "knuckles punch, hook flies out" rather than
    //     playing a crossbow-draw animation on bare fists. Valheim's attack
    //     animations fire a generic animation-event callback that triggers
    //     whatever Attack is configured on the weapon regardless of which
    //     clip is playing, so this should still fire the projectile - but
    //     this is the single biggest thing to verify in-game and the most
    //     likely spot to need iteration if the hook doesn't launch.
    //   - The vanilla hook projectile (Projectile_GrapplingHook) is a plain
    //     Projectile component like an arrow/bolt, with real HitData
    //     (~10 pierce, confirmed via community sources) - it already
    //     damages Characters on impact, so no extra Character-hit patch is
    //     needed to make grappling something "count" as a hit.
    //
    // This class also gives the clone a flat pierce damage bonus (base
    // SharedData.m_damages.m_pierce only, not m_damagesPerLevel, so the
    // bonus stays flat across quality levels rather than scaling): Grapple
    // Knuckles is meant as an alternative to enchanting Knucklechains into
    // Frostfire/Thunderblood, not a strict upgrade, so it trades the
    // elemental proc for the grapple utility plus this bonus. The exact
    // number is a starting point for playtesting, not a researched balance
    // target - the enchanted variants' real damage figures could only be
    // corroborated via unverified search snippets, not primary source.
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters))]
    internal static class ObjectDB_UpdateRegisters_GrapplePatch
    {
        private const float PierceDamageBonus = 40f;

        // Halved from vanilla on purpose - this mod is meant to be more fun
        // than balanced.
        private const float ReloadTimeMultiplier = 0.5f;

        private static bool _applied;

        private static void Postfix(ObjectDB __instance)
        {
            // ObjectDB.UpdateRegisters runs repeatedly (login, respawn,
            // etc.); the attack data only needs wiring once per process.
            if (_applied || __instance == null)
            {
                return;
            }

            var hookPrefab = __instance.GetItemPrefab(GrappleKnucklesPlugin.VanillaHookPrefabName);
            var clonedPrefab = __instance.GetItemPrefab(GrappleKnucklesPlugin.ClonedItemPrefabName);

            if (hookPrefab == null)
            {
                Logger.LogWarning(
                    $"Could not find vanilla item prefab '{GrappleKnucklesPlugin.VanillaHookPrefabName}' " +
                    "in ObjectDB - confirm this is still the correct prefab name for your game version.");
                return;
            }

            if (clonedPrefab == null)
            {
                // Not registered yet this pass; try again next UpdateRegisters call.
                return;
            }

            var hookItemData = hookPrefab.GetComponent<ItemDrop>()?.m_itemData;
            var clonedItemData = clonedPrefab.GetComponent<ItemDrop>()?.m_itemData;

            var hookAttack = hookItemData?.m_shared?.m_secondaryAttack;
            if (hookAttack == null)
            {
                Logger.LogWarning(
                    "Vanilla GrapplingHook has no m_secondaryAttack configured - " +
                    "the confirmed field name may have changed, or the hook fires from m_attack instead.");
                return;
            }

            if (clonedItemData?.m_shared == null)
            {
                Logger.LogWarning("Cloned Grapple Knuckles item has no SharedData yet; skipping attack wiring.");
                return;
            }

            var clonedAttack = clonedItemData.m_shared.m_secondaryAttack ?? new Attack();

            clonedAttack.m_attackProjectile = hookAttack.m_attackProjectile;
            clonedAttack.m_attackStamina = hookAttack.m_attackStamina;
            clonedAttack.m_reloadTime = hookAttack.m_reloadTime * ReloadTimeMultiplier;
            clonedAttack.m_blockReloadTime = hookAttack.m_blockReloadTime;

            clonedItemData.m_shared.m_secondaryAttack = clonedAttack;

            clonedItemData.m_shared.m_damages.m_pierce += PierceDamageBonus;

            _applied = true;

            Logger.LogInfo(
                $"Wired {GrappleKnucklesPlugin.ClonedItemPrefabName}'s secondary attack to " +
                $"{GrappleKnucklesPlugin.VanillaHookPrefabName}'s grapple projectile " +
                $"({ReloadTimeMultiplier:P0} reload time), +{PierceDamageBonus} base pierce damage.");
        }
    }
}
