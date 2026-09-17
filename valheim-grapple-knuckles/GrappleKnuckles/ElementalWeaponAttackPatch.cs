using HarmonyLib;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Same approach as GrappleAttackPatch: patch ObjectDB.UpdateRegisters
    // (confirmed extension point) after items and cloned bolt projectiles
    // are both available, and point each weapon's secondary Attack at its
    // scaled-down cloned bolt, with an Eitr cost (confirmed field
    // Attack.m_attackEitr) instead of a stamina-only special move. Reuses
    // the primary attack's animation trigger, same reasoning as Grapple
    // Knuckles: keep the weapon's own swing animation rather than the
    // source staff's cast animation.
    [HarmonyPatch(typeof(ObjectDB), "UpdateRegisters")]
    internal static class ObjectDB_UpdateRegisters_ElementalWeaponsPatch
    {
        private const float SecondaryAttackEitrCost = 10f;
        private const float SecondaryAttackStaminaCost = 5f;
        private const float SecondaryAttackReloadTime = 2f;

        // Faster than Dundr's own cast, per explicit "faster and weaker"
        // direction (the "weaker" half is the LightningBoltDamageMultiplier
        // in ElementalWeapons.cs). Note there's no charge/draw mechanic to
        // strip out here in the first place - this patch never sets
        // m_drawStaminaDrain/m_reloadEitrDrain (the fields a "loading" cast
        // would use), so the sword's bolt was always instant-fire.
        private const float LightningSwordReloadTime = 1f;

        private static bool _applied;

        private static void Postfix(ObjectDB __instance)
        {
            if (_applied || __instance == null)
            {
                return;
            }

            if (ElementalWeapons.FireBoltProjectile == null ||
                ElementalWeapons.LightningBoltProjectile == null)
            {
                // Bolt projectiles not cloned yet; try again next call.
                return;
            }

            var fireDagger = __instance.GetItemPrefab(ElementalWeapons.FireDaggerPrefabName);
            var lightningSword = __instance.GetItemPrefab(ElementalWeapons.LightningSwordPrefabName);

            if (fireDagger == null || lightningSword == null)
            {
                // Not all registered yet this pass; try again next call.
                return;
            }

            WireSecondaryAttack(fireDagger, ElementalWeapons.FireBoltProjectile, "Fire Dagger", SecondaryAttackReloadTime);
            WireSecondaryAttack(lightningSword, ElementalWeapons.LightningBoltProjectile, "Lightning Sword", LightningSwordReloadTime);

            _applied = true;
        }

        private static void WireSecondaryAttack(UnityEngine.GameObject weaponPrefab, UnityEngine.GameObject boltProjectile, string label, float reloadTime)
        {
            var itemData = weaponPrefab.GetComponent<ItemDrop>()?.m_itemData;
            if (itemData?.m_shared == null)
            {
                Logger.LogWarning($"{label}: no SharedData yet, skipping secondary attack wiring.");
                return;
            }

            var attack = itemData.m_shared.m_secondaryAttack ?? new Attack();

            attack.m_attackProjectile = boltProjectile;
            attack.m_attackEitr = SecondaryAttackEitrCost;
            attack.m_attackStamina = SecondaryAttackStaminaCost;
            attack.m_reloadTime = reloadTime;

            if (itemData.m_shared.m_attack != null)
            {
                attack.m_attackAnimation = itemData.m_shared.m_attack.m_attackAnimation;
            }

            itemData.m_shared.m_secondaryAttack = attack;

            Logger.LogInfo($"{label}: wired secondary attack to a cloned bolt projectile ({SecondaryAttackEitrCost} Eitr cost, {reloadTime}s reload).");
        }
    }
}
