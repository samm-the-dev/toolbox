using HarmonyLib;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Four distinct mechanics, all hooked onto real, confirmed vanilla
    // methods rather than reimplemented from scratch:
    //
    //   1. Eitr drain while blocking - Postfix on Humanoid.UpdateBlock(dt),
    //      mirroring the real, confirmed precedent Player.UpdateAttackBowDraw()
    //      uses for continuous per-tick Eitr drain on a held input. Vanilla
    //      has no "just holding block" cost at all (block only costs
    //      stamina per absorbed hit), so this is new behavior, not a tweak
    //      of an existing drain.
    //   2. Frost proc on parry - Postfix on Humanoid.BlockAttack, recomputing
    //      the same perfect-block condition vanilla uses internally
    //      (m_blockTimer < 0.25f - the confirmed m_perfectBlockInterval -
    //      and SharedData.m_timedBlockBonus > 1). We deliberately do NOT
    //      implement "double damage on parry" ourselves: vanilla already
    //      does this automatically for ANY successful parry against ANY
    //      shield (Character.cs: a hit landed on a currently-staggering
    //      non-player target gets x2, and a perfect block already staggers
    //      the attacker) - all we add is the frost proc at the moment of
    //      the parry.
    //   3. AoE burst on block break - same Postfix, checking
    //      __instance.IsStaggering() right after the base call (vanilla has
    //      no distinct "block broken" event; a failed block just staggers
    //      the wielder inline, confirmed via decompile).
    //   4. Omnidirectional blocking - Prefix/Postfix pair on BlockAttack.
    //      Vanilla gates block to a frontal hemisphere via
    //      Vector3.Dot(hit.m_dir, transform.forward) > 0 (confirmed exact
    //      check). We temporarily rotate the wielder to face directly away
    //      from the incoming hit for the duration of the call (guaranteeing
    //      the dot product is negative), then restore their real rotation -
    //      same "swap state, let original run, restore" trick already used
    //      in QualityTransferPatch.cs, just applied to rotation instead of
    //      item data. This is a plausible, low-risk technique on paper but
    //      was never visually verified - a single-frame rotation snap could
    //      be noticeable, or interact oddly with camera/animation. Flagged
    //      in CLAUDE.md as worth watching for in-game.
    //
    // NOT confirmed / higher-risk assumptions in this file:
    //   - Humanoid.UseEitr(float) as the real method name/signature (by
    //     analogy with the confirmed UseStamina and the confirmed
    //     UpdateAttackBowDraw call pattern, but not independently verified).
    //   - m_leftItem as the private field holding the equipped shield
    //     (by analogy with the m_rightItem assumption used elsewhere in
    //     this mod, not freshly confirmed this session).
    //   - Character.Damage(HitData) as the real method to apply the frost
    //     proc's damage directly to the attacker (extremely common pattern
    //     in Valheim modding generally, but not decompile-confirmed in this
    //     project's own research threads).
    //   - Spawning the AoE burst via UnityEngine.Object.Instantiate rather
    //     than through Valheim's own ZNetScene spawn path - the burst
    //     prefab carries networked components (ZNetView/ZSyncTransform per
    //     earlier research), so a raw Instantiate may not register/replicate
    //     correctly in multiplayer. This is the single biggest open risk in
    //     this file; single-player should still work since ZNetView tends
    //     to self-register locally, but treat multiplayer as unverified.
    internal static class ShieldOfFrostPatches
    {
        private const float EitrDrainPerSecond = 4f;
        private const float ParryFrostDamage = 15f;

        // Facing-direction restore state, captured in the omnidirectional
        // Prefix and consumed by its paired Postfix on the same call.
        private static Quaternion? _restoreRotation;

        private static bool IsWieldingShieldOfFrost(Humanoid humanoid)
        {
            var leftItem = Traverse.Create(humanoid).Field("m_leftItem").GetValue<ItemDrop.ItemData>();
            return leftItem?.m_dropPrefab != null && leftItem.m_dropPrefab.name == ShieldOfFrost.ShieldClonedPrefabName;
        }

        [HarmonyPatch(typeof(Humanoid), "UpdateBlock")]
        [HarmonyPostfix]
        private static void UpdateBlock_EitrDrain(Humanoid __instance, float dt)
        {
            if (!__instance.IsBlocking() || !IsWieldingShieldOfFrost(__instance))
            {
                return;
            }

            __instance.UseEitr(EitrDrainPerSecond * dt);
        }

        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        [HarmonyPrefix]
        private static void BlockAttack_Omnidirectional_Prefix(Humanoid __instance, HitData hit)
        {
            _restoreRotation = null;

            if (hit == null || !IsWieldingShieldOfFrost(__instance))
            {
                return;
            }

            if (Vector3.Dot(hit.m_dir, __instance.transform.forward) <= 0f)
            {
                // Already a frontal hit; nothing to do.
                return;
            }

            _restoreRotation = __instance.transform.rotation;
            var awayFromHit = -hit.m_dir;
            if (awayFromHit.sqrMagnitude > 0.0001f)
            {
                __instance.transform.rotation = Quaternion.LookRotation(awayFromHit.normalized, Vector3.up);
            }
        }

        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        [HarmonyPostfix]
        private static void BlockAttack_Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result)
        {
            if (_restoreRotation is Quaternion rotation)
            {
                __instance.transform.rotation = rotation;
                _restoreRotation = null;
            }

            if (!IsWieldingShieldOfFrost(__instance))
            {
                return;
            }

            if (__result && attacker != null)
            {
                var blockTimer = Traverse.Create(__instance).Field("m_blockTimer").GetValue<float>();
                var timedBlockBonus = Traverse.Create(__instance).Field("m_leftItem").GetValue<ItemDrop.ItemData>()
                    ?.m_shared?.m_timedBlockBonus ?? 1f;

                // Recomputes vanilla's own perfect-block condition (confirmed
                // m_perfectBlockInterval = 0.25f) to detect a parry.
                if (blockTimer < 0.25f && timedBlockBonus > 1f)
                {
                    ApplyFrostProc(attacker);
                }
            }

            if (__instance.IsStaggering())
            {
                SpawnFrostBurst(__instance.transform.position);
            }
        }

        private static void ApplyFrostProc(Character attacker)
        {
            var hitData = new HitData
            {
                m_damage = { m_frost = ParryFrostDamage },
                m_point = attacker.transform.position,
                m_dir = Vector3.down,
            };

            attacker.Damage(hitData);
        }

        private static void SpawnFrostBurst(Vector3 position)
        {
            var burstPrefab = ShieldOfFrost.FrostBurstProjectile;
            if (burstPrefab == null)
            {
                Logger.LogWarning("Shield of Frost: frost burst projectile not cloned yet; skipping break effect.");
                return;
            }

            Object.Instantiate(burstPrefab, position, Quaternion.identity);
        }
    }
}
