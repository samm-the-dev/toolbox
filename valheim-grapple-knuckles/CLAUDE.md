# Verification notes for Claude Code Desktop

This mod was built entirely from web/GitHub research (other mods' decompiled
source, Jötunn's own source/prefab lists, community wikis) with no access
to the actual Valheim game files or a local decompile. Everything below is
either an assumption, a fact confirmed only second-hand, or a mechanic that
was never tested in a running game. Work through this against
`assembly_valheim.dll` (ILSpy/dnSpy) and an actual play session before
trusting any of it.

Organized by file. "Confirmed via decompile" below means *someone else's*
decompile dump found via GitHub search, not this game install's own
assembly - re-verify against your own copy, since dumps can be stale,
mismatched game versions, or just wrong.

## GrappleKnucklesPlugin.cs / GrappleAttackPatch.cs

- **Biggest unverified assumption**: Valheim's attack-animation-event
  callback that actually fires the configured `Attack` is generic across
  weapon/animation types, so Knucklechains' own punch animation clip still
  triggers the copied grapple `Attack` (which was authored for a crossbow
  draw animation) rather than silently doing nothing. If the secondary
  attack does nothing in-game, this is the first thing to check - try
  temporarily copying `m_attackAnimation` from the hook's `Attack` instead
  of reusing the punch animation, to isolate whether it's an animation-event
  gating issue.
- `ItemDrop.ItemData.SharedData.m_secondaryAttack`/`m_attack` (type
  `Attack`), `Attack.m_attackProjectile`/`m_attackStamina`/`m_reloadTime`/
  `m_attackAnimation`/`m_blockReloadTime`, `Projectile.m_damage` (a
  `HitData.DamageTypes`), and `SharedData.m_movementModifier` are all
  confirmed via decompiled source dumps (`porohkun/ValheimMjod`,
  `m3talstorm/valhiem_server`) cross-checked against real open-source mods
  that reference the same fields - solid, but still second-hand.
- `ObjectDB.UpdateRegisters` and `InventoryGui.DoCrafting(Player)` are both
  **private methods** patched by name/signature. Private methods are the
  most likely things to get silently renamed or restructured between game
  versions - confirm both still exist with these signatures in 1.0.7, and
  that Harmony successfully patches them (check the BepInEx log on startup
  for patch failures).
- `QualityTransferPatch.cs` reads `InventoryGui`'s private fields
  `m_craftRecipe` and searches inventory via `Player.GetInventory()` /
  `Inventory.GetAllItems()` - the `GetAllItems()` call specifically was
  never directly confirmed this session (moderate-but-not-verified
  confidence it's the real method name).
- The "highest quality if the player has duplicates" heuristic for picking
  which `FistGold` instance's quality to carry over is a guess - vanilla's
  actual `ConsumeResources` consumption order (which specific item instance
  gets removed) was never confirmed.
- The vanilla hook projectile's "~10 pierce damage" figure is
  community-sourced, not decompiled.
- `Attack.m_attackAnimation` real field name is decompile-confirmed, but
  whether reusing the item's own primary attack's trigger value for the
  secondary attack actually produces a good-looking result (vs. a T-pose,
  vs. silently not triggering the projectile spawn) is untested.
- "Black Forge" (`blackforge`) is confirmed as a real `CraftingStation`
  prefab, but vanilla `FistGold` is actually crafted via a separate
  Cast-at-Black-Forge + finish-at-Frost-Foundry two-step process that this
  mod does NOT replicate - just check that `blackforge` is actually usable/
  unlocked the way this recipe expects.
- Pierce damage bonus (+40), reload time multiplier (0.5x), and movement
  speed bonus (+10%) are arbitrary numbers for fun, explicitly not
  balance-tested. Tune freely.

## FenrisMageArmor.cs

- Only two real Fenris prefabs were found in Jötunn's own generated prefab
  list: `ArmorFenringChest` and `ArmorFenringLegs`. Community wikis
  describe a third Hood piece; it either doesn't exist in 1.0.7, exists
  under a name the prefab list search missed, or the prefab list itself is
  incomplete/stale. **Check your own game files for a Fenris hood/helm
  prefab** - if one exists, this mod is currently missing it entirely.
- Fenris armor's real armor/weight values, and the comparison figures used
  for Wolf/Padded/Carapace armor, are **community-sourced only** (wiki
  search snippets, several domains were egress-blocked so even those
  weren't directly fetched) - never confirmed against decompiled prefab
  default values. The `ArmorScale`/`WeightScale` multipliers were chosen
  to scale relative to whatever the clone actually inherits at runtime
  specifically to route around this uncertainty, but the *result* (is it
  actually "Mistlands power level"?) was never checked against real
  numbers.
- `SharedData.m_armor`/`m_armorPerLevel` field names are based on general
  Valheim-modding convention, not decompile-confirmed in this session's
  research threads specifically.
- `CustomItem.ItemDrop` (the Jötunn API property used to reach
  `.m_itemData.m_shared` after cloning) was not freshly re-verified this
  session - based on general Jötunn familiarity. If the build fails here,
  this is the first place to check against the actual Jötunn API for
  whatever version ends up in `Libraries/`.
- `SE_Stats`'s full field list is probably larger than what's been
  confirmed (`m_eitrRegenMultiplier`, `m_staminaRegenMultiplier`,
  `m_dodgeStaminaUseModifier`, `m_percentigeDamageModifiers`) - worth
  reading the whole class once you have a real decompile, in case there's
  a better-fitting field for future tuning.
- Eitr regen per piece (+20%) was chosen relative to the real Mistlands
  Eitr-weave set's community-sourced numbers (+20%/+40%/+40% per piece) -
  those source numbers are themselves unconfirmed, so this is a guess
  built on a guess. Stamina regen set bonus (+25%) is an arbitrary
  starting point.
- The set-bonus detach logic (overriding `m_setName`/`m_setSize`/
  `m_setStatusEffect` so these clones don't combine with real Fenris
  pieces) relies on `Humanoid.UpdateEquipmentStatusEffects()` re-evaluating
  correctly at runtime - the wiring was never actually tested in a live
  game.
- Recipe requirements are placeholder-minimal (just the source Fenris
  piece, no other materials) - almost certainly too cheap; needs real
  balancing.

## ElementalWeapons.cs / ElementalWeaponAttackPatch.cs

- The Fire Dagger is now a direct clone of `KnifeGold` (Nord Dagger) - an
  earlier version cloned `KnifeSkollAndHati` instead (for its dual-blade
  animation) and attempted a mesh-reskin toward Nord Dagger's appearance,
  but that whole dual-wield/reskin approach was dropped per explicit
  direction. No mesh-swap risk in the current version.
- **Essence/Eitr upgrade cost numbers are placeholders.** The real vanilla
  essence cost to enchant Knucklechains into Frostfire/Thunderblood
  couldn't be found from this environment (recipe requirement amounts are
  recipe-asset data, not decompiled C#, and wiki sites that would have them
  are blocked here) - `BaseVanillaEnchantEssenceCost = 2` in
  `ElementalWeapons.cs` is a guess to double from, per explicit "double
  essences" request. Correct this constant against the real recipe first,
  then the derived `UpgradeEssenceCost` follows automatically.
- Base recipe quantities (2x Nord Dagger + 1x Frostfire Essence per dagger;
  1x Nord Sword + 2x Thunderblood Essence for the sword) came directly from
  the user, not research - not independently verified, but also not a
  guess on my part.
- Secondary attack numbers (10 Eitr cost, 5 stamina, 2s reload) and
  elemental damage bonuses (+20 fire/lightning) are arbitrary starting
  points, same as every other tuning number in this mod.
- The two bolt projectiles are cloned at half scale (`BoltScale = 0.5f`)
  via the same `PrefabManager.CreateClonedPrefab` pattern as the grapple
  hook - confirmed technique, but the *visual* result of scaling a staff
  projectile prefab down (does the VFX/particle system scale
  proportionally, or look broken at non-1x scale?) was never checked.
- `Attack.m_attackEitr` is decompile-confirmed as a real field, but whether
  Eitr actually gets consumed/checked correctly for a *melee* weapon's
  secondary attack (as opposed to a staff's primary attack, which is what
  every real Eitr-costed vanilla item actually is) was never confirmed -
  this mod is the first thing giving a `OneHandedWeapon`/`TwoHandedWeapon`-
  type item an Eitr cost, which might behave differently than expected
  (e.g. no Eitr-cost UI indicator, since that UI may be staff-specific).
- A frost dagger (+20 frost damage instead of fire, otherwise identical)
  was built and then removed per explicit direction: the design settled on
  one Fire Dagger, with the frost identity moved to a separate shield
  instead. If you want it back, it's a near-identical copy of
  `CloneDagger()`.

## Whole-mod gaps

- **No localization file exists anywhere in this project.** Every item
  name/description (`$item_fistgold_grapple`, `$item_fenrismage_chest`,
  `$item_fire_dagger`, `$item_lightning_sword`, etc.) is an unlocalized
  token - in-game, these will likely show as the literal raw string, not
  readable text, until a `Translations/English.json` (or Jötunn's
  localization API) is added.
- Nothing in this mod has been run in an actual Valheim session. Every
  "confirmed" fact above was confirmed via someone else's source code, not
  by observing this mod's actual behavior.

## Open idea, in progress at time of writing: Shield of Frost

Not yet built. Design as of this writing: a frost-themed magic shield,
replacing what was originally going to be a Frost Dagger, that:
- Channels a small Eitr drain per second while actively blocking (instead
  of, or alongside, vanilla's stamina-cost block).
- Works omnidirectionally if vanilla shields don't already (unconfirmed).
- Triggers a frost AoE burst when the shield "breaks" (a hit exceeds its
  block power and staggers the block).
- On a successful parry, procs a frost effect and doubles damage - unclear
  yet whether that means the parry's own bonus damage or the wielder's
  next attack.
- Visually, a smaller version of the Staff of Protection's bubble.

This is a new *kind* of mechanic for this mod: a continuous per-second
resource drain gated on a held input state (blocking), plus hooking two
distinct combat events (block-break, parry) that nothing built so far has
touched - not a one-shot attack or a static item-stat change. A research
pass on the real parry/block-break/channel mechanics was launched but its
results aren't reflected in this file yet as of this writing - check for a
newer version of this section, or the actual `ShieldOfFrost.cs` (if it
exists yet) for what actually got confirmed and built.
