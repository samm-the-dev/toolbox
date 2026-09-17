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
    // Confirmed facts this relies on:
    //   - The vanilla Grappling Hook prefab is "GrapplingHook", not FistGold.
    //   - ItemDrop.ItemData.SharedData.m_secondaryAttack (type Attack) is the
    //     Attack instance that actually fires the grapple: it holds
    //     m_attackProjectile, which is what spawns/drives the real
    //     GrapplingPoint component (rope LineRenderer, pull-toward-anchor,
    //     etc). By pointing our clone's secondary Attack at a copy of that
    //     projectile, vanilla's own attack dispatch does the rest - no
    //     physics reimplementation needed.
    //   - We reuse the item's own primary/light attack animation trigger
    //     (Attack.m_attackAnimation, confirmed field) for the secondary
    //     attack too, instead of Knucklechains' normal special-move
    //     animation or the hook's crossbow-draw animation.
    //   - The vanilla hook projectile (Projectile_GrapplingHook) is a plain
    //     Projectile component like an arrow/bolt, with its own damage in
    //     Projectile.m_damage (confirmed field) - it already damages
    //     Characters on impact, so no extra Character-hit patch is needed.
    //   - We do NOT mutate Projectile_GrapplingHook directly: that GameObject
    //     is the same one the vanilla GrapplingHook item references, so
    //     changing its damage would leak into vanilla hook throws for every
    //     player. Instead GrappleKnucklesPlugin clones it via Jötunn's
    //     PrefabManager.CreateClonedPrefab (the confirmed, idiomatic way to
    //     clone a vanilla prefab without touching the original) on
    //     PrefabManager.OnVanillaPrefabsAvailable, and we point our clone's
    //     Attack at that independent copy instead.
    //   - SharedData.m_movementModifier (confirmed field) is a flat additive
    //     fraction summed across all equipped items (e.g. -0.2 for a 20%
    //     penalty, matching Wolf/Troll armor); a positive value here is a
    //     speed bonus.
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters))]
    internal static class ObjectDB_UpdateRegisters_GrapplePatch
    {
        private const float ProjectilePierceDamageBonus = 40f;

        // Faster than the plain vanilla hook on purpose - this mod is meant
        // to be more fun than balanced, and framed as "a little faster to
        // grapple" than the Mistlands hook it's an Ashlands upgrade of.
        private const float ReloadTimeMultiplier = 0.4f;

        // +10% movement speed while equipped, deliberately unbalanced/for
        // fun per explicit request.
        private const float MovementSpeedBonus = 0.1f;

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

            var clonedProjectile = GrappleKnucklesPlugin.ClonedProjectile;
            if (clonedProjectile == null)
            {
                // PrefabManager.OnVanillaPrefabsAvailable hasn't fired yet
                // (or the clone failed); try again next call.
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

            var projectileComponent = clonedProjectile.GetComponent<Projectile>();
            if (projectileComponent != null)
            {
                projectileComponent.m_damage.m_pierce += ProjectilePierceDamageBonus;
            }

            var clonedAttack = clonedItemData.m_shared.m_secondaryAttack ?? new Attack();

            clonedAttack.m_attackProjectile = clonedProjectile;
            clonedAttack.m_attackStamina = hookAttack.m_attackStamina;
            clonedAttack.m_reloadTime = hookAttack.m_reloadTime * ReloadTimeMultiplier;
            clonedAttack.m_blockReloadTime = hookAttack.m_blockReloadTime;

            if (clonedItemData.m_shared.m_attack != null)
            {
                clonedAttack.m_attackAnimation = clonedItemData.m_shared.m_attack.m_attackAnimation;
            }

            clonedItemData.m_shared.m_secondaryAttack = clonedAttack;

            clonedItemData.m_shared.m_movementModifier = MovementSpeedBonus;

            _applied = true;

            Logger.LogInfo(
                $"Wired {GrappleKnucklesPlugin.ClonedItemPrefabName}'s secondary attack to a cloned " +
                $"{GrappleKnucklesPlugin.VanillaHookPrefabName} projectile ({ReloadTimeMultiplier:P0} reload " +
                $"time, +{ProjectilePierceDamageBonus} projectile pierce damage), +{MovementSpeedBonus:P0} movement speed.");
        }
    }
}
