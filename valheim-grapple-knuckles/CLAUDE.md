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

## Whole-mod gaps

- **No localization file exists anywhere in this project.** Every item
  name/description (`$item_fistgold_grapple`, `$item_fenrismage_chest`,
  etc.) is an unlocalized token - in-game, these will likely show as the
  literal raw string, not readable text, until a `Translations/English.json`
  (or Jötunn's localization API) is added.
- Nothing in this mod has been run in an actual Valheim session. Every
  "confirmed" fact above was confirmed via someone else's source code, not
  by observing this mod's actual behavior.

## Open idea, not yet designed or researched

User's next idea (as of this writing, not yet built): a magic shield that
channels a small Eitr drain per second while blocking (instead of vanilla's
stamina-cost block), visually a smaller version of the Staff of
Protection's bubble, and working in all directions rather than a frontal
arc (if vanilla shields are frontal-only - not yet confirmed). This would
be a new *kind* of mechanic for this mod: a continuous per-second resource
drain gated on a held input state (blocking), rather than a one-shot
attack or a static item-stat change like everything built so far. Likely
needs a Harmony patch on whatever method handles block start/hold/end
(unconfirmed - not yet researched), not just `SharedData`/`Attack` field
edits. Also unconfirmed: whether vanilla block is already omnidirectional
or arc-limited, and how the Staff of Protection's bubble VFX is actually
implemented (worth reusing if it's a clean prefab reference, same pattern
as the grapple hook's projectile reuse).
