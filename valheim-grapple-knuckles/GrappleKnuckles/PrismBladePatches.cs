using HarmonyLib;
using Logger = Jotunn.Logger;

namespace GrappleKnuckles
{
    // Prism Blade mechanics: cycles the weapon's active elemental damage
    // type (Fire -> Frost -> Lightning -> Poison -> Fire...) on secondary
    // attack use, and makes every hit deal ONLY the currently-active
    // element (not all four at once).
    //
    // Confirmed facts this relies on:
    //   - ItemDrop.ItemData.GetDamage(int quality, float worldLevelBonus)
    //     returns HitData.DamageTypes BY VALUE (it's a struct) - a Postfix
    //     overriding __result never touches the shared m_shared.m_damages,
    //     so this is safe per-instance even though every Prism Blade
    //     instance shares the same SharedData. Confirmed real precedent:
    //     EpicLoot (RandyKnapp/ValheimMods, ModifyDamage.cs /
    //     ConvertPhysicalDamageToLightning.cs) patches this exact method
    //     the same way.
    //   - GetDamage is ALSO called from tooltip/UI code (confirmed in this
    //     project's research), so this patch must only READ __instance's
    //     state and override __result - never trigger a one-time side
    //     effect here. The element cycling itself lives on the
    //     StartAttack patch below, not here.
    //   - ItemDrop.ItemData.m_variant is a real, already-existing int
    //     field that persists across saves (both ZDO and Inventory
    //     ZPackage serialization, confirmed via decompile in this
    //     project's research) - used here as the 0-3 active-element index
    //     instead of adding new custom data.
    //   - Humanoid.StartAttack(Character target, bool secondaryAttack) is
    //     confirmed real (davrum/assembly_valheim, Humanoid.cs) and is
    //     polled every FixedUpdate while the attack button is held, but
    //     only returns true once per actual successful swing start -
    //     confirmed by a dedicated research pass this session, which also
    //     confirmed the real precedent sighsorry1029/SecondaryAttacks
    //     patches this exact method with a Postfix gated on
    //     `secondaryAttack && __result` for one-shot-per-swing behavior.
    //     The same research pass confirmed there is no field on `Attack`
    //     itself (or its `Start`/`Attack.cs` overloads) distinguishing
    //     primary from secondary - the `secondaryAttack` bool parameter is
    //     the only reliable signal.
    //
    // NOT confirmed / carried-over assumptions from elsewhere in this mod:
    //   - m_rightItem as the private Humanoid field holding a two-handed
    //     weapon like THSwordGold (by analogy with the m_leftItem
    //     assumption ShieldOfFrostPatches.cs already uses for shields -
    //     not freshly confirmed this session, but two-handed weapons are
    //     known to occupy the right-hand slot in vanilla, same as
    //     one-handed weapons).
    //   - Humanoid.UseEitr(float) as the real method name/signature (same
    //     unconfirmed-but-precedented assumption already relied on in
    //     ShieldOfFrostPatches.cs and ElementalWeaponAttackPatch.cs).
    //   - MessageHud.instance.ShowMessage(...) as the way to announce the
    //     newly-active element to the wielder - extremely common Valheim
    //     modding pattern but not decompile-confirmed in this project;
    //     wrapped defensively (null-checked) so a wrong assumption here
    //     just silently skips the message rather than breaking the swap.
    internal static class PrismBladePatches
    {
        internal enum Element
        {
            Fire = 0,
            Frost = 1,
            Lightning = 2,
            Poison = 3,
        }

        // Flat damage dealt by whichever element is currently active -
        // replaces, not adds to, the source THSwordGold's own damage
        // profile while this patch is active.
        private const float ActiveElementDamage = 70f;

        // Small Eitr cost per swap, consistent with this mod's "special
        // moves cost Eitr" convention used everywhere else.
        private const float SwapEitrCost = 8f;

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDamage), typeof(int), typeof(float))]
        [HarmonyPostfix]
        private static void GetDamage_ActiveElementOnly(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result)
        {
            if (!IsPrismBlade(__instance))
            {
                return;
            }

            var element = (Element)__instance.m_variant;

            __result.m_fire = 0f;
            __result.m_frost = 0f;
            __result.m_lightning = 0f;
            __result.m_poison = 0f;

            switch (element)
            {
                case Element.Fire:
                    __result.m_fire = ActiveElementDamage;
                    break;
                case Element.Frost:
                    __result.m_frost = ActiveElementDamage;
                    break;
                case Element.Lightning:
                    __result.m_lightning = ActiveElementDamage;
                    break;
                case Element.Poison:
                    __result.m_poison = ActiveElementDamage;
                    break;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPostfix]
        private static void StartAttack_CycleElement(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            if (!secondaryAttack || !__result)
            {
                return;
            }

            var weapon = Traverse.Create(__instance).Field("m_rightItem").GetValue<ItemDrop.ItemData>();
            if (!IsPrismBlade(weapon))
            {
                return;
            }

            weapon.m_variant = ((weapon.m_variant + 1) % 4);
            __instance.UseEitr(SwapEitrCost);

            var element = (Element)weapon.m_variant;
            Logger.LogInfo($"Prism Blade: switched to {element}");
            AnnounceElement(element);
        }

        private static bool IsPrismBlade(ItemDrop.ItemData item)
        {
            return item?.m_dropPrefab != null && item.m_dropPrefab.name == PrismBlade.ClonedPrefabName;
        }

        private static void AnnounceElement(Element element)
        {
            if (MessageHud.instance == null)
            {
                return;
            }

            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"Prism Blade: {element}");
        }
    }
}
