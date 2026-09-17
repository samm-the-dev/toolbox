using System.Linq;
using HarmonyLib;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Design: rather than patching Humanoid.StartAttack and reimplementing
    // the hook's raycast/pull/rope physics, we patch ObjectDB.UpdateRegisters
    // (the same extension point BetterGrapplingHook uses to tweak the real
    // Grappling Hook's stats) and clone the vanilla GrapplingHook's own
    // secondary-attack config wholesale (Attack.Clone()) onto our cloned
    // item's secondary attack, then override only what needs to differ
    // (the projectile reference, animation, reload time).
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
    [HarmonyPatch(typeof(ObjectDB), "UpdateRegisters")]
    internal static class ObjectDB_UpdateRegisters_GrapplePatch
    {

        // FistGold's own real base damage (114 blunt, ground-truth extracted
        // from this game's resources.assets via AssetRipper, 2026-09) is
        // Deep North-tier - too strong for an Ashlands item. AssetRipper also
        // pulled every real fist weapon's damage across all tiers: they
        // consistently land ~0.6-0.7x a same-tier one-handed sword/mace's raw
        // damage (e.g. FistFenrirClaw/SwordSilver = 0.57, FistBjornUndeadClaw/
        // SwordBlackmetal = 0.84, FistGold/MaceGold = 0.67 - real Deep North
        // pure-blunt-vs-pure-blunt data, the cleanest comparison available).
        // The real Ashlands pure-blunt one-hander is MaceEldner at 135 blunt;
        // 95/135 = 0.70 sits at the upper end of that real ratio band, in
        // line with FistBjornUndeadClaw's 0.84 (Plains) trending the ratio up
        // at higher tiers. This scales the inherited clone's base blunt
        // damage down to that target rather than a hardcoded replacement, so
        // it stays correct if FistGold's own stats ever change.
        private const float BaseBluntDamageScale = 95f / 114f;

        // Faster than the plain vanilla hook on purpose - this mod is meant
        // to be more fun than balanced, and framed as "a little faster to
        // grapple" than the Mistlands hook it's an Ashlands upgrade of.
        private const float ReloadTimeMultiplier = 0.4f;

        // +5% movement speed while equipped, deliberately unbalanced/for
        // fun per explicit request (cut down from an initial +10% per
        // explicit direction, 2026-09-17).
        private const float MovementSpeedBonus = 0.05f;

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

            var clonedGrapplingPoint = GrappleKnucklesPlugin.ClonedGrapplingPoint;
            if (clonedGrapplingPoint == null)
            {
                // Same as clonedProjectile above - try again next call.
                return;
            }

            var hookItemData = hookPrefab.GetComponent<ItemDrop>()?.m_itemData;
            var clonedItemDrop = clonedPrefab.GetComponent<ItemDrop>();
            var clonedItemData = clonedItemDrop?.m_itemData;

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
                // The flying projectile spawns a SEPARATE prefab on hit to do
                // the actual grapple-attach (real GrapplingHook.m_spawnOnHit,
                // confirmed via prefab data) - point ours at our own cloned
                // GrapplingPoint instead of leaving it on the original shared
                // one (see GrappleKnucklesPlugin.cs).
                projectileComponent.m_spawnOnHit = clonedGrapplingPoint;
            }

            var grapplingPointComponent = clonedGrapplingPoint.GetComponent<GrapplingPoint>();
            if (grapplingPointComponent != null && clonedItemDrop != null)
            {
                // GrapplingPoint.Update() (confirmed via decompile) breaks the
                // grapple early every frame m_equipCheck's item isn't
                // currently equipped (by exact SharedData.m_name match) - the
                // cloned GrapplingPoint inherited a reference to the REAL
                // vanilla GrapplingHook's own ItemDrop here, which could never
                // match while wielding Grapple Knuckles, self-cancelling the
                // grapple the instant it started. Point it at our own item's
                // ItemDrop instead.
                grapplingPointComponent.m_equipCheck = clonedItemDrop;
            }

            // Clone the REAL vanilla hook's Attack wholesale (Attack.Clone(),
            // a real vanilla method: MemberwiseClone() over every field) instead
            // of hand-picking individual fields onto Knucklechains' own kick
            // Attack. Confirmed via the real GrapplingHook.prefab data,
            // 2026-09-17, why the earlier field-by-field approach fell short:
            // the kick Attack's m_projectileVel is 10 (a leftover/irrelevant
            // default - kicks never configure m_attackProjectile, it's
            // {fileID: 0} on the real FistGold prefab) versus the hook's real
            // 40, plus a dozen other projectile-launch fields (accuracy,
            // launch angle, hitTerrain, etc.) that were never copied at all -
            // together this produced the reported "weakly launching in a
            // small arc, not actually grappling." Cloning the hook's full
            // Attack config sidesteps ever missing another one of these.
            var clonedAttack = hookAttack.Clone();

            // Per explicit direction, 2026-09-17: the secondary attack should
            // play Knucklechains' own BASIC/primary punch animation, not its
            // kick (an earlier version tried the kick - wrong per direction)
            // and not the hook's "crossbow_fire" (carried over by Clone()
            // above, and wrong for bare fists).
            //
            // Confirmed root cause of "Attack.Start returns true but nothing
            // visibly happens", 2026-09-17: the real FistGold primary attack
            // is a 2-level combo chain (m_attackChainLevels: 2 in the real
            // prefab data). Attack.Start() (confirmed via decompile) only
            // calls SetTrigger(m_attackAnimation) bare when m_attackChainLevels
            // <= 1 - for a chained attack it calls
            // SetTrigger(m_attackAnimation + currentChainLevel) instead (e.g.
            // "unarmed_attack0"/"unarmed_attack1"), and THAT's the trigger
            // name actually wired to a real Animator state. Copying only
            // m_attackAnimation while m_attackChainLevels stayed at the
            // hook's cloned value (0) meant Start() fired a bare
            // "unarmed_attack" SetTrigger that matches no real Animator
            // parameter - a silent no-op in Unity, hence __result=true (Start
            // itself doesn't know the trigger didn't land) but no animation,
            // no projectile, nothing. Fixed by copying m_attackChainLevels
            // and m_attackRandomAnimations too, so Start()'s trigger-name
            // selection logic matches the primary attack's real behavior
            // exactly, not just its base animation string.
            if (clonedItemData.m_shared.m_attack != null)
            {
                var primaryAttack = clonedItemData.m_shared.m_attack;
                clonedAttack.m_attackAnimation = primaryAttack.m_attackAnimation;
                clonedAttack.m_attackChainLevels = primaryAttack.m_attackChainLevels;
                clonedAttack.m_attackRandomAnimations = primaryAttack.m_attackRandomAnimations;
            }

            clonedAttack.m_attackProjectile = clonedProjectile;
            clonedAttack.m_reloadTime = hookAttack.m_reloadTime * ReloadTimeMultiplier;

            clonedItemData.m_shared.m_secondaryAttack = clonedAttack;

            clonedItemData.m_shared.m_movementModifier = MovementSpeedBonus;

            var baseBluntBefore = clonedItemData.m_shared.m_damages.m_blunt;
            clonedItemData.m_shared.m_damages.m_blunt *= BaseBluntDamageScale;
            clonedItemData.m_shared.m_damagesPerLevel.m_blunt *= BaseBluntDamageScale;

            _applied = true;

            Logger.LogInfo(
                $"Wired {GrappleKnucklesPlugin.ClonedItemPrefabName}'s secondary attack to a cloned " +
                $"{GrappleKnucklesPlugin.VanillaHookPrefabName} projectile ({ReloadTimeMultiplier:P0} reload " +
                $"time), +{MovementSpeedBonus:P0} movement speed. " +
                $"Base blunt damage scaled {baseBluntBefore} -> {clonedItemData.m_shared.m_damages.m_blunt} " +
                "to fit Ashlands tier (see BaseBluntDamageScale). " +
                $"[GrappleDebug] final secondaryAttack: anim='{clonedAttack.m_attackAnimation}', " +
                $"type={clonedAttack.m_attackType}, projectileVel={clonedAttack.m_projectileVel}, " +
                $"projectile={(clonedAttack.m_attackProjectile != null ? clonedAttack.m_attackProjectile.name : "null")}, " +
                $"equipCheck={(grapplingPointComponent != null && grapplingPointComponent.m_equipCheck != null ? grapplingPointComponent.m_equipCheck.name : "null")}, " +
                $"spawnOnHit={(projectileComponent != null && projectileComponent.m_spawnOnHit != null ? projectileComponent.m_spawnOnHit.name : "null")}.");
        }
    }

    // Vanilla GrapplingPoint.Activate() (confirmed via decompile) always
    // anchors the rope's visual start point at the LEFT hand
    // (visEquipment.m_leftHand), regardless of which hand actually threw it -
    // fine for the real GrapplingHook (never dual-wielded), wrong for a fist
    // weapon worn on the right hand. Postfix re-anchors it to the right hand
    // instead, but only for our own cloned GrapplingPoint instances (matched
    // by the "(Clone)" name Unity gives Instantiate()'d copies of our
    // GrapplingPoint_Knuckles prefab) - never touches real vanilla hook
    // throws from other players/the vanilla item.
    [HarmonyPatch(typeof(GrapplingPoint), "Activate")]
    internal static class GrapplingPoint_RightHandAnchor_Patch
    {
        private static void Postfix(GrapplingPoint __instance, Character character)
        {
            if (__instance == null || !__instance.name.StartsWith(GrappleKnucklesPlugin.ClonedGrapplingPointPrefabName))
            {
                return;
            }

            if (character is Humanoid humanoid)
            {
                var visEquipment = humanoid.GetVisEquipment();
                if (visEquipment != null && visEquipment.m_rightHand != null)
                {
                    __instance.m_attachPoint = visEquipment.m_rightHand;
                }
            }
        }
    }

    // The grapple projectile's actual damage does NOT come from the
    // projectile prefab's own m_damage (0 on both the real hook and our
    // clone) - confirmed via decompile, Attack.FireProjectileBurst()
    // computes hitData.m_damage from m_weapon.GetDamage(), the WIELDER's
    // own weapon damage. The real vanilla GrapplingHook's own base damage
    // is just 10 pierce (a near-harmless utility tool); Grapple Knuckles'
    // is the real ~95 blunt Ashlands-tier fist damage, so the throw
    // naturally hit far harder than a vanilla hook throw would - not a
    // bug, just an emergent result of a strong fist weapon using the same
    // mechanic a near-harmless tool uses. Per explicit direction, scaled
    // down to 1/10th of the wielded weapon's damage for the projectile
    // specifically, leaving the melee punch's own damage untouched.
    //
    // Patched on Projectile.Setup (manually - not [HarmonyPatch], for the
    // same reason as GrappleCooldownPatch: several other Attack/
    // Humanoid-adjacent methods this session turned out to silently not
    // get patched via PatchAll()'s attribute discovery, while a manual
    // Patch() call on the identical method works reliably - safer to use
    // the proven-working path for a method we haven't specifically
    // verified either way). HitData is a class (confirmed via decompile),
    // so mutating hitData.m_damage's fields here, before Setup stores the
    // reference on the projectile, correctly affects the actual hit.
    internal static class Projectile_ScaleGrappleDamage_Patch
    {
        private const float ProjectileDamageScale = 0.1f;

        internal static void RegisterManualPatch(HarmonyLib.Harmony harmony)
        {
            var original = HarmonyLib.AccessTools.Method(typeof(Projectile), "Setup");
            var prefix = new HarmonyLib.HarmonyMethod(HarmonyLib.AccessTools.Method(typeof(Projectile_ScaleGrappleDamage_Patch), nameof(Prefix)));
            harmony.Patch(original, prefix: prefix);
        }

        private static void Prefix(HitData hitData, ItemDrop.ItemData item)
        {
            if (hitData == null || item?.m_dropPrefab == null
                || item.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            hitData.m_damage.m_blunt *= ProjectileDamageScale;
            hitData.m_damage.m_slash *= ProjectileDamageScale;
            hitData.m_damage.m_pierce *= ProjectileDamageScale;
            hitData.m_damage.m_chop *= ProjectileDamageScale;
            hitData.m_damage.m_pickaxe *= ProjectileDamageScale;
            hitData.m_damage.m_fire *= ProjectileDamageScale;
            hitData.m_damage.m_frost *= ProjectileDamageScale;
            hitData.m_damage.m_lightning *= ProjectileDamageScale;
            hitData.m_damage.m_poison *= ProjectileDamageScale;
            hitData.m_damage.m_spirit *= ProjectileDamageScale;
        }
    }

    // Launch sound, per explicit direction ("would be nice to use the sound
    // fx for the vanilla hook"). Confirmed via decompile earlier this
    // session that the real hook's Attack-level EffectLists (m_triggerEffect
    // etc.) are all empty on both attacks, so there was nothing to inherit
    // via Attack.Clone() - the working theory was that vanilla's launch
    // sound is baked into the "crossbow_fire" animation clip itself (via a
    // Unity Animation Event), which we never play since Grapple Knuckles
    // reuses the punch animation instead. Rather than trying to extract or
    // replicate that clip-level event, LogGrappleSoundCandidates() (see
    // GrappleKnucklesPlugin.cs) found the real, standalone sound-effect
    // PREFABS Valheim uses for the hook directly: sfx_grapplinghook_fire,
    // _hit, _pull, _reload, _detach, _repel - separate, spawn-and-forget
    // GameObjects (confirmed via decompiled ZSFX.m_playOnAwake = true, the
    // same pattern GrapplingPoint.m_pullSound already uses:
    // Object.Instantiate(m_pullSound, transform) with no explicit Play()
    // call). Instantiating sfx_grapplinghook_fire directly sidesteps the
    // animation-event problem entirely - it's the same real asset vanilla
    // uses, just triggered from code instead of a clip we don't play.
    internal static class GrappleFireSoundPatch
    {
        private const string FireSoundPrefabName = "sfx_grapplinghook_fire";

        private static GameObject _fireSoundPrefab;
        private static bool _lookupAttempted;

        internal static void RegisterManualPatch(Harmony harmony)
        {
            // Manually patched, not [HarmonyPatch] + PatchAll() - same known
            // gap as GrappleCooldownPatch/Projectile_ScaleGrappleDamage_Patch.
            var original = AccessTools.Method(typeof(Attack), "Start");
            var postfix = new HarmonyMethod(AccessTools.Method(typeof(GrappleFireSoundPatch), nameof(AttackStart_PlayFireSound)));
            harmony.Patch(original, postfix: postfix);
        }

        private static void AttackStart_PlayFireSound(Humanoid character, Attack __instance, bool __result)
        {
            if (!__result || __instance?.m_attackProjectile == null
                || __instance.m_attackProjectile != GrappleKnucklesPlugin.ClonedProjectile)
            {
                return;
            }

            if (!_lookupAttempted)
            {
                _lookupAttempted = true;
                _fireSoundPrefab = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == FireSoundPrefabName);
                if (_fireSoundPrefab == null)
                {
                    Logger.LogWarning($"[Grapple] Could not find real sound prefab '{FireSoundPrefabName}' - no launch sound will play.");
                }
            }

            if (_fireSoundPrefab != null && character != null)
            {
                Object.Instantiate(_fireSoundPrefab, character.transform.position, Quaternion.identity);
            }
        }
    }
}
