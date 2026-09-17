using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Grapple Knuckles' secondary attack has no vanilla cooldown to lean on:
    // Attack.m_reloadTime (confirmed via decompile) is only ever read inside
    // Player.QueueReloadAction(), which itself only runs when the WEAPON'S
    // PRIMARY attack has m_requiresReload = true - ours is a plain punch, so
    // m_reloadTime is completely inert for a melee-type secondary attack.
    // Setting m_requiresReload directly on the secondary attack doesn't work
    // either: Player.UpdateWeaponLoading() only ever checks the primary
    // attack's flag, so it forces m_weaponLoaded = null every single frame
    // regardless of what the secondary attack's own m_requiresReload says -
    // meaning Player.IsWeaponLoaded() can never usefully track OUR loaded
    // state; we track it ourselves (_grappleLoaded below) instead.
    //
    // Instead, this reuses vanilla's own general-purpose "minor action" queue
    // (Player.m_actionQueue / Player.MinorActionData - the same system real
    // reload/equip/unequip actions use) by manually constructing and
    // enqueueing our own MinorActionData entry via reflection (no public API
    // to add to this queue directly). ActionType.Reload specifically (used
    // for the HUD text/SetWeaponLoaded semantics, not the animation) means
    // this gets - confirmed via decompile - Humanoid.InMinorAction() (blocks
    // starting a new attack while queued) and the vanilla HUD's progress bar
    // (Player.GetActionProgress() reads whatever's at the front of this same
    // queue, regardless of ActionType) for free. The ANIMATION played is
    // independent of ActionType - see the "equipping" bool used below.
    //
    // FIXED 2026-09-17 (third round): reusing the REAL m_actionQueue means
    // vanilla's own Player.CheckRun()/Player.OnJump() (confirmed via
    // decompile: both unconditionally call ClearActionQueue() on every
    // successful sprint frame / every jump) already clear our queued Reload
    // entry exactly like a real one - that part was already correct for
    // free. What was missing: real vanilla RE-QUEUES automatically, every
    // frame, via Player.UpdateWeaponLoading() (while m_weaponLoaded != the
    // held weapon and nothing's currently queued) - since that method only
    // ever reads the PRIMARY attack's m_requiresReload (see above, always
    // false for us), it never does this for our weapon. Without our own
    // equivalent, clearing via sprint/jump just silently dropped the
    // cooldown forever - a full exploit, not just a visual hiccup. Fixed by
    // adding our own state (_grappleLoaded, _grappleInFlight) and a
    // continuous per-frame recheck patched onto Player.UpdateWeaponLoading
    // itself (see UpdateWeaponLoading_RequeueIfNeeded below), mirroring
    // vanilla's own requeue behavior exactly, just gated on our item
    // instead of m_requiresReload.
    //
    // Triggers, in the order a grapple throw actually visits them:
    //   1. Attack.Start succeeds for our secondary attack (the throw itself)
    //      - marks _grappleInFlight, so the continuous recheck below won't
    //      try to queue a reload while the outcome (hit or miss) is still
    //      pending, matching real vanilla's own m_grappling/m_blockReload
    //      gates on QueueReloadAction() for the same reason.
    //   2a. GrapplingPoint.Break (hit resolved, pull finished, early or
    //       normal) - queues the cooldown.
    //   2b. The thrown projectile is destroyed without ever hitting
    //       anything (miss) - via GrappleMissDetector, a small component
    //       added to our cloned projectile prefab - also queues the
    //       cooldown, matching real vanilla's hit-independent reset.
    //   3. Player.SetWeaponLoaded(ourItem) - real vanilla code (confirmed
    //      via decompile, runs unconditionally in UpdateActionQueue's
    //      Reload-completion branch for ANY Reload entry, including ours) -
    //      marks _grappleLoaded true the instant our queued cooldown
    //      completes naturally.
    //   4. Player.UpdateWeaponLoading (every frame) - if our item is held,
    //      not loaded, not in flight, and nothing's currently queued,
    //      re-queues. Covers both "never queued yet" (fresh equip, though
    //      EquipItem_QueueInitialLoad usually beats it to it) and "queue
    //      was wiped by sprint/jump."
    //   5. Humanoid.EquipItem - requires an initial "load" before first
    //      use, matching the real vanilla hook's on-equip behavior.
    internal static class GrappleCooldownPatch
    {
        // Tracks whether Grapple Knuckles is ready to fire again. Separate
        // from Player.m_weaponLoaded/IsWeaponLoaded() because that field is
        // useless for us (see file header) - vanilla's own
        // UpdateWeaponLoading forces it null every frame regardless of our
        // state. Local-player-scoped only, matching how this mod already
        // tracks GrapplingPoint's own Player.m_grappling field (real vanilla
        // code does the same for that field).
        private static bool _grappleLoaded;

        // True from a successful throw until the outcome (hit or miss) is
        // known - prevents the continuous requeue check from starting a new
        // cooldown while a throw is still mid-flight/mid-pull.
        private static bool _grappleInFlight;

        internal static void RegisterManualPatch(Harmony harmony)
        {
            // Manually patched, not [HarmonyPatch] + PatchAll() - same known
            // gap documented for Attack.Start/Humanoid.StartAttack in
            // CLAUDE.md, and (confirmed this session) also Humanoid.EquipItem:
            // the equip-triggered initial load never once fired across an
            // entire test pass of repeated equip/unequip cycles. Manually
            // patching everything here rather than trusting PatchAll() for
            // anything touching Humanoid/Attack/GrapplingPoint/Player this
            // mod cares about.
            var startAttack = AccessTools.Method(typeof(Humanoid), "StartAttack");
            var equipItem = AccessTools.Method(typeof(Humanoid), "EquipItem");
            var breakGrapple = AccessTools.Method(typeof(GrapplingPoint), "Break");
            var attackStart = AccessTools.Method(typeof(Attack), "Start");
            var setWeaponLoaded = AccessTools.Method(typeof(Player), "SetWeaponLoaded");
            var updateWeaponLoading = AccessTools.Method(typeof(Player), "UpdateWeaponLoading");

            harmony.Patch(startAttack, prefix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(StartAttack_BlockIfNotLoaded))));
            harmony.Patch(equipItem, postfix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(EquipItem_QueueInitialLoad))));
            harmony.Patch(breakGrapple, postfix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(Break_QueueCooldown))));
            harmony.Patch(attackStart, postfix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(AttackStart_MarkInFlight))));
            harmony.Patch(setWeaponLoaded, postfix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(SetWeaponLoaded_MarkLoaded))));
            harmony.Patch(updateWeaponLoading, postfix: new HarmonyMethod(AccessTools.Method(typeof(GrappleCooldownPatch), nameof(UpdateWeaponLoading_RequeueIfNeeded))));

            Logger.LogInfo("[Grapple] Cooldown patches registered against Humanoid.StartAttack/EquipItem, GrapplingPoint.Break, " +
                            "Attack.Start, Player.SetWeaponLoaded, Player.UpdateWeaponLoading.");
        }

        // FIXED 2026-09-17 (fourth round): "sprint continuously and trigger
        // the launch without waiting for the reload at all." Root cause -
        // InMinorAction() (the ONLY thing that was blocking a new attack) is
        // purely animator-STATE-TAG based, not tied to our C# queue state
        // directly. Player.CheckRun()'s ClearActionQueue() (confirmed via
        // decompile) only clears the LIST - it does NOT touch the animator
        // bool itself; that only happens inside UpdateActionQueue()'s "queue
        // now empty" branch. Sprinting continuously creates a race every
        // single frame: CheckRun() clears the queue -> UpdateActionQueue()
        // sees it empty and sets the animator bool false -> InMinorAction()
        // is briefly, genuinely false -> StartAttack() slips through in that
        // window, even though UpdateWeaponLoading_RequeueIfNeeded re-adds a
        // fresh entry moments later (too late, the attack already started).
        // Real vanilla doesn't have this hole because Attack.Start() has a
        // SECOND, non-animator gate for real reload weapons:
        // `if (m_requiresReload && !IsWeaponLoaded()) return false;` - pure
        // C# state, immune to animator/queue timing. We can't use that gate
        // directly (see file header - IsWeaponLoaded() is permanently
        // useless for us), so this Prefix is our own equivalent: check
        // _grappleLoaded directly and block before Humanoid.StartAttack's
        // own body (and therefore Attack.Start/ClearActionQueue) ever runs,
        // regardless of what the animator happens to be doing that frame.
        private static bool StartAttack_BlockIfNotLoaded(Humanoid __instance, bool secondaryAttack, ref bool __result)
        {
            if (!secondaryAttack || __instance is not Player player)
            {
                return true;
            }

            var weapon = player.GetCurrentWeapon();
            if (weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return true;
            }

            if (!_grappleLoaded)
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static void AttackStart_MarkInFlight(Humanoid character, Attack __instance, bool __result)
        {
            if (!__result || __instance?.m_attackProjectile == null
                || __instance.m_attackProjectile != GrappleKnucklesPlugin.ClonedProjectile)
            {
                return;
            }

            _grappleInFlight = true;
            _grappleLoaded = false;
        }

        // Real vanilla mechanism, confirmed via decompile: GrapplingPoint.
        // Activate() sets Player.m_grappling = 1f (on hit), Update() keeps
        // it refreshed to 0.2f every frame during the pull, Break() (fires
        // on both early break and normal completion) zeroes it - and
        // Player.QueueReloadAction() refuses to queue while m_grappling >
        // 0f. So the real reload is held off for the pull's entire
        // duration and becomes eligible the instant Break() runs, which
        // this mirrors directly.
        private static void Break_QueueCooldown(GrapplingPoint __instance, Character ___m_character)
        {
            if (__instance == null || !__instance.name.StartsWith(GrappleKnucklesPlugin.ClonedGrapplingPointPrefabName))
            {
                return;
            }

            _grappleInFlight = false;

            if (___m_character != Player.m_localPlayer || ___m_character is not Player player)
            {
                return;
            }

            var weapon = player.GetCurrentWeapon();
            if (weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            QueueCooldown(player, weapon);
        }

        // Called by GrappleMissDetector (attached to our cloned projectile
        // prefab in GrappleKnucklesPlugin.CloneProjectile) when the thrown
        // projectile is destroyed WITHOUT ever having hit anything - a
        // miss. Real vanilla doesn't need an equivalent because its reload
        // gate is hit-independent by construction (m_weaponLoaded resets on
        // attack-trigger regardless of outcome); ours is hit-triggered via
        // GrapplingPoint.Break, which never runs on a miss since
        // GrapplingPoint itself never spawns.
        internal static void OnProjectileMissed()
        {
            if (!_grappleInFlight)
            {
                // Already resolved via Break_QueueCooldown (a hit) - a miss
                // callback firing afterward would be a stale/duplicate
                // signal, not a real second event.
                return;
            }

            _grappleInFlight = false;

            var player = Player.m_localPlayer;
            var weapon = player?.GetCurrentWeapon();
            if (weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            QueueCooldown(player, weapon);
        }

        private static void SetWeaponLoaded_MarkLoaded(ItemDrop.ItemData weapon)
        {
            if (weapon?.m_dropPrefab != null && weapon.m_dropPrefab.name == GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                _grappleLoaded = true;
            }
        }

        // The continuous requeue check - mirrors real vanilla's own
        // UpdateWeaponLoading() (called every frame with the currently
        // held weapon), which for a real reload-requiring PRIMARY attack
        // auto-queues QueueReloadAction() any time the weapon isn't marked
        // loaded and nothing's already queued. That method only ever reads
        // the PRIMARY attack's m_requiresReload (always false for our plain
        // punch), so it never does this for Grapple Knuckles - this patch
        // is the missing equivalent, scoped to our item instead.
        private static void UpdateWeaponLoading_RequeueIfNeeded(Player __instance, ItemDrop.ItemData weapon)
        {
            if (__instance != Player.m_localPlayer || _grappleLoaded || _grappleInFlight)
            {
                return;
            }

            if (weapon?.m_dropPrefab == null || weapon.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            QueueCooldown(__instance, weapon);
        }

        private static void EquipItem_QueueInitialLoad(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || item?.m_dropPrefab == null
                || item.m_dropPrefab.name != GrappleKnucklesPlugin.ClonedItemPrefabName)
            {
                return;
            }

            if (__instance is Player player)
            {
                _grappleInFlight = false;
                QueueCooldown(player, item);
            }
        }

        private static void QueueCooldown(Player player, ItemDrop.ItemData item)
        {
            if (player == null || item?.m_shared == null)
            {
                return;
            }

            var queue = Traverse.Create(player).Field("m_actionQueue").GetValue<List<Player.MinorActionData>>();
            if (queue == null)
            {
                Logger.LogWarning("[Grapple] m_actionQueue reflection lookup returned null - field name may have changed.");
                return;
            }

            // Don't stack a second cooldown on top of one already running -
            // mirrors the real IsReloadActionQueued() guard vanilla's own
            // QueueReloadAction() uses for the same reason. Also the guard
            // that stops UpdateWeaponLoading_RequeueIfNeeded from adding a
            // duplicate entry every single frame.
            if (queue.Any(a => a.m_type == Player.MinorActionData.ActionType.Reload))
            {
                return;
            }

            _grappleLoaded = false;

            var duration = GrappleKnucklesPlugin.CooldownDuration.Value;

            // "equipping" bool (not "reload_crossbow") per explicit
            // direction, 2026-09-17: the crossbow-reload pose looked wrong
            // on a fist weapon - see CLAUDE.md's polish-round notes for the
            // full reasoning and the sound trade-off this carries.
            queue.Add(new Player.MinorActionData
            {
                m_type = Player.MinorActionData.ActionType.Reload,
                m_item = item,
                m_duration = duration,
                m_progressText = "$hud_reloading " + item.m_shared.m_name,
                m_animation = "equipping",
            });

            Logger.LogInfo($"[Grapple] Queued {duration}s cooldown on {item.m_dropPrefab.name}.");
        }
    }

    // Attached to our cloned projectile prefab (GrappleKnucklesPlugin.
    // CloneProjectile) so a miss (projectile despawns via TTL without ever
    // hitting anything) still queues the cooldown - see
    // GrappleCooldownPatch.OnProjectileMissed. Projectile itself has no
    // OnDestroy Unity callback of its own (confirmed via decompile) to
    // patch directly, so this rides along as a sibling component instead,
    // reading the real (private) Projectile.m_didHit field via Traverse at
    // the moment Unity tears the GameObject down.
    internal class GrappleMissDetector : MonoBehaviour
    {
        private void OnDestroy()
        {
            var projectile = GetComponent<Projectile>();
            if (projectile == null)
            {
                return;
            }

            var didHit = Traverse.Create(projectile).Field("m_didHit").GetValue<bool>();
            if (!didHit)
            {
                GrappleCooldownPatch.OnProjectileMissed();
            }
        }
    }
}
