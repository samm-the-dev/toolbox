using HarmonyLib;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Three distinct mechanics, all hooked onto real, confirmed vanilla
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
    //
    // Omnidirectional blocking (a rotation-swap trick on BlockAttack's
    // frontal-arc check) was designed, decompile-verified as sign-correct,
    // and then dropped per explicit direction ("too fiddly") before ever
    // being tried in-game - see CLAUDE.md for the removed design if it's
    // ever worth revisiting.
    //
    // Decompile-confirmed this session against the user's actual
    // assembly_valheim.dll and Jotunn.dll 2.30.0 (see CLAUDE.md "Decompile
    // verification pass" for the full list) - no longer just assumptions:
    //   - Humanoid.UseEitr(float): inherited virtual from Character
    //     (Player overrides it to drain over RPC for networking), confirmed.
    //   - m_leftItem: confirmed protected field on Humanoid holding the
    //     equipped left-hand/shield item.
    //   - Character.Damage(HitData): confirmed public method.
    //   - Humanoid.UpdateBlock(float)/BlockAttack(HitData, Character):
    //     confirmed private/protected-override signatures match this file's
    //     Harmony patches exactly.
    //   - m_perfectBlockInterval (0.25f) and m_timedBlockBonus: confirmed:
    //     vanilla's own parry check is
    //     `m_timedBlockBonus > 1f && m_blockTimer != -1f && m_blockTimer < 0.25f`.
    //     This file's replica (below) omits the `m_blockTimer != -1f` guard -
    //     m_blockTimer is set to -1 whenever NOT currently blocking, so in
    //     the (rare/edge-case) situation where BlockAttack fires while
    //     m_blockTimer is exactly -1, this file would misfire the frost
    //     proc as a "perfect parry" when vanilla itself would not grant the
    //     timed-block bonus. Low severity (worst case: an extra frost proc
    //     on a near-miss block), but worth matching vanilla's guard exactly.
    //
    // Still NOT confirmed / genuinely needs live testing or asset data:
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
        [HarmonyPostfix]
        private static void BlockAttack_Postfix(Humanoid __instance, Character attacker, bool __result)
        {
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
                // m_perfectBlockInterval = 0.25f, and the m_blockTimer != -1f
                // guard vanilla also checks - m_blockTimer sits at -1 whenever
                // not currently blocking, which would otherwise read as "under
                // 0.25s" and misfire a parry proc).
                if (blockTimer != -1f && blockTimer < 0.25f && timedBlockBonus > 1f)
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
