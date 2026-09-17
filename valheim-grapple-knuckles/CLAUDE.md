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

## HIGH PRIORITY: Staff of Fracturing's real damage type is in doubt

Both `ShieldOfFrostPatches.cs` (the block-break burst) and
`ExplodingSledge.cs` (the secondary attack) clone
`staff_clusterbombstaff_projectile`/its splinter sub-munition and modify
`Projectile.m_damage.m_frost` specifically, on the assumption that "Staff
of Fracturing" deals frost damage. The user flagged (from memory, not
verified) that Fracturing is likely **blunt + fire**, not frost at all. If
that's correct, both of those `m_frost` modifications are silently
adjusting a damage field that's already zero on the source projectile -
the burst would still deal whatever blunt/fire damage the projectile
actually has by default, just not "extra frost" as intended, and the
"frost" framing/flavor text on both items would be factually wrong about
what they actually do. **Check Fracturing's real damage composition first**
and, if it's not frost, either swap the damage field this code modifies
(`m_damage.m_blunt`/`m_damage.m_fire` instead of `m_frost`) or pick a
different source projectile that's actually frost-flavored for these two
effects.

## BloodMagicSpear.cs

New: Deep North's first item, using the real vanilla "Blood Magic" resource
mechanic (Eitr + percentage of CURRENT health, engine-clamped so it can't
kill the wielder) instead of the pure-Eitr pattern every other item in this
mod uses. Fully confirmed via direct decompile (`davrum/assembly_valheim`,
`Attack.cs` lines 82-89/460-503 and `Character.cs` lines 2531-2541) -
`Attack.m_attackHealth`/`m_attackHealthPercentage` are real fields, set the
same simple way `m_attackEitr` already is elsewhere; no Harmony patch
needed, vanilla's own `Attack.Update()` calls `Character.UseHealth()`
directly.

- Clones `SpearGold` (Nord Spear) and retunes its existing secondary attack
  (no new projectile/prefab cloned, unlike every other weapon in this mod)
  rather than reusing one of the already-cloned bolts, specifically so
  Deep North doesn't feel like another elemental-bolt reskin.
- **`Bloodgold`/`Nornathread` prefab names are not independently confirmed
  for this file specifically** - they appeared consistently across
  multiple prior research passes (cited in both Echo Spike's and Lightning
  Strike's real recipes), giving reasonable confidence, but the exact
  Jötunn spelling/capitalization was never checked directly. Degrades
  gracefully if wrong, same as everywhere else in this mod.
- Secondary attack numbers (8% current health, 5 Eitr, +40 pierce) are
  arbitrary starting points, same as every other tuning number in this
  mod.
- A more thematically "complete" Blood Magic item might follow the real
  pattern more closely (Echo Spike/Dead Raiser/Spirit Caller are all
  summon-type weapons whose power scales with Blood Magic skill level, not
  simple direct-damage attacks) - this was deliberately scoped down to
  "reuse an existing attack slot, just change its resource cost," since
  building a real summon/minion mechanic would be substantially more
  engineering than anything else in this mod and wasn't asked for
  specifically.

## GrappleKnucklesPlugin.cs / GrappleAttackPatch.cs

**Re-tiered to Ashlands** per explicit direction, after the
`GrapplingHook`-tier correction below made it possible: the intended
narrative is "get the plain Grappling Hook easily in Mistlands, then
upgrade to this fist weapon in Ashlands." The recipe deliberately does
**not** require a real `FistGold` (that would gate an Ashlands/pre-Deep-
North item behind Deep North) - `FistGold` is only the Jötunn `CustomItem`
clone source for model/mechanics, priced instead with `GrapplingHook` +
Flametal + Charred Bone (the same Ashlands material family used elsewhere
in this mod). The old `QualityTransferPatch.cs` (which carried a consumed
`FistGold`'s quality onto the crafted item) was **removed** as dead code
once the recipe stopped consuming a real `FistGold` - there's nothing left
for it to transfer from.

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
- `ObjectDB.UpdateRegisters` is a **private method** patched by
  name/signature - the most likely kind of thing to get silently renamed
  or restructured between game versions. Confirm it still exists with this
  signature in 1.0.7, and that Harmony successfully patches it (check the
  BepInEx log on startup for patch failures).
- The vanilla hook projectile's "~10 pierce damage" figure is
  community-sourced, not decompiled.
- `Attack.m_attackAnimation` real field name is decompile-confirmed, but
  whether reusing the item's own primary attack's trigger value for the
  secondary attack actually produces a good-looking result (vs. a T-pose,
  vs. silently not triggering the projectile spawn) is untested.
- "Black Forge" (`blackforge`) is confirmed as a real `CraftingStation`
  prefab, and confirmed real for Ashlands weapon-tier crafting specifically
  (Dyrnwyn, Nidhögg both use it) - not just a guess anymore.
- Flametal/Charred Bone recipe quantities (15/3, same as the other Ashlands
  items in this mod) are estimates, not sourced from any single real
  recipe - this is a new item, not a replica.
- Pierce damage bonus (+40), reload time multiplier (0.4x), and movement
  speed bonus (+10%) are arbitrary numbers for fun, explicitly not
  balance-tested. Tune freely.

## AshlandsHybridArmor.cs / DeepNorthHybridArmor.cs

New: fast-mage hybrid armor for Ashlands and Deep North, filling the "Fenris
Mage Armor" role at those tiers. Approached from the opposite direction to
Fenris: instead of starting from a light non-mage set and adding partial
Eitr regen, these clone the *real* mage armor at each tier - "Embla"
(`ArmorMageChest_Ashlands`/`ArmorMageLegs_Ashlands`, confirmed real
prefabs) and "Caller" (`ArmorDeepNorthMageChest`/`ArmorDeepNorthMagelegs`,
confirmed real prefabs) - and trade a slice of their own native armor for
movement speed, keeping their Eitr regen intact.

- **The biggest assumption in both files**: `m_equipStatusEffect` is
  deliberately left untouched on the clone (not overwritten, unlike
  `FenrisMageArmor.cs` which builds a brand-new one from scratch), on the
  theory that Jötunn's clone inherits it from the source item automatically,
  and Embla/Caller's own real Eitr regen almost certainly lives there (by
  analogy with the confirmed per-piece Eitr-weave pattern). **This was
  never independently verified** - it's plausible but unconfirmed that
  Embla/Caller's Eitr regen instead comes through their own
  `m_setStatusEffect` (which these files DO overwrite, to detach from
  vanilla's set and install our stamina-regen bonus instead) - if so, these
  hybrid pieces would silently lose all Eitr regen rather than keep it.
  **Check this first in-game**: equip a hybrid piece alone (not the full
  set) and see if Eitr regen is still boosted.
- Real armor totals for Embla (~75) and Caller (~22/piece) are
  community-sourced (WebSearch), not primary-confirmed - the
  `ArmorScale = 0.85f` trade-off is relative to whatever the clone actually
  inherits, same reasoning as `FenrisMageArmor.cs`'s scaling, not a
  hardcoded absolute.
- Recipe requirements are placeholder-minimal (just the source armor
  piece), same as `FenrisMageArmor.cs` - almost certainly too cheap.
- Both share the same set-bonus mechanism, StatusEffect-creation pattern,
  and caveats as `FenrisMageArmor.cs` below - not re-documented per file.

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
- **Re-tiered per explicit direction, after a full survey of real vanilla
  elemental weapons/staves**: Fire Dagger moved from Deep North to
  Mistlands (priced with Surtling Core + Refined Eitr, the confirmed real
  materials for "Staff of Embers"); Lightning Sword moved from Deep North
  to Ashlands (priced with Flametal + Bloodstone (`GemstoneRed`) + Charred
  Bone, modeled on Dyrnwyn/Nidhögg's real recipes as a generalized
  template, not copied exactly since those are specific named items). Both
  still clone from their original Nord-tier base item (`KnifeGold`/
  `SwordGold`) for model/mechanics, since no confirmed Mistlands-native
  dagger or Ashlands-native one-handed sword exists to clone from instead -
  only the recipe materials/station tier changed, not the visual base.
- **Recipe quantities for the new tier materials (Surtling Core, Flametal,
  Bloodstone, Charred Bone amounts) are estimates**, informed by real
  comparable recipes (Staff of Embers, Dyrnwyn, Nidhögg) but not exact
  copies - these are new items, not replicas of any single real recipe.
  Verify they feel right for the intended tier in actual play.
- Secondary attack numbers (10 Eitr cost, 5 stamina, 2s reload for Fire
  Dagger / 1s for Lightning Sword) and elemental damage bonuses (+20 fire/
  lightning) are arbitrary starting points, same as every other tuning
  number in this mod.
- **Lightning Sword's bolt is now deliberately weaker than Dundr's own
  cast** (`LightningBoltDamageMultiplier = 0.5f` applied to the cloned
  projectile's `m_lightning` damage), per explicit "faster and weaker, no
  loading mechanic" direction. The "no loading mechanic" half was already
  true by construction - this patch never sets the draw/charge-related
  Attack fields (`m_drawEitrDrain`/similar), so the sword's bolt fires
  instantly regardless; only the damage/reload tuning needed an actual
  code change.
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

## MountainTierAxe.cs

New: a Silver/Mountain-tier axe with innate fire+spirit damage and no Eitr
spell at all - Eitr doesn't exist at this tier, matching the real vanilla
"Frostner" (`MaceSilver`) pattern (baked-in elemental damage, fully
craftable, no enchant material). A dedicated survey confirmed no vanilla
axe has ever had innate elemental damage, and axes skip the Silver/
Mountain tier entirely (`AxeIron` -> `AxeBlackMetal`, no `AxeSilver`
exists) - so there's no real base item to clone from for this slot.

- Clones `AxeIron` (closest lower tier) and scales its damage up via a
  multiplier (`DamageScale = 1.8f`) toward Frostner's confirmed real power
  level, rather than a hardcoded absolute - same technique already used for
  Fenris Mage armor, so it stays correct regardless of `AxeIron`'s exact
  real baseline. **The scale factor itself is an estimate** - Frostner's
  real stats (35 blunt/40 frost/20 spirit primary, confirmed via WebSearch
  cross-reference) weren't precisely matched against `AxeIron`'s actual
  numbers, just aimed at roughly that power level.
- `CraftingStation = "forge"` and the Silver/Ancient Bark recipe quantities
  are **unconfirmed guesses** (by analogy with the confirmed no-`"piece_"`-
  prefix `"blackforge"` naming) - never checked against a real Frostner
  recipe or the real Forge station name. `MinStationLevel = 3` matches
  Frostner's confirmed real Forge-level-3 requirement.
- Pairs with the existing Fenris Mage armor (already Mountain-tier) - no
  new armor was built for this tier.

## ExplodingSledge.cs

New: an Ashlands sledge. Normal attacks are untouched vanilla `SledgeGold`
cleave (no per-hit explosion, per explicit "just the usual sledge AoE"
direction) - only the secondary attack is modified, firing a single frost
burst sized like one of "Staff of Fracturing"'s splinter sub-munitions
(`staff_clusterbombstaff_splinter_projectile` - the smaller child
projectile the main clusterbomb spawns on impact, deliberately NOT the main
multi-splinter projectile itself, per explicit direction), damage scaled up
slightly (`SplinterDamageMultiplier = 1.3f`) above a single splinter's own
damage.

- Priced with Flametal + Charred Bone (the same Ashlands material family as
  Lightning Sword's re-tier) rather than replicating `SledgeGold`'s own
  real `_FrostFire`/`_BloodLightning` enchant-sibling recipe, since this is
  a distinct new item.
- Secondary attack stamina/reload numbers are arbitrary starting points,
  same as every other tuning number in this mod.
- Shares the same unconfirmed-mesh/no-localization/untested-in-game caveats
  as every other item in this mod.

## Whole-mod gaps

- **No localization file exists anywhere in this project.** Every item
  name/description (`$item_fistgold_grapple`, `$item_fenrismage_chest`,
  `$item_fire_dagger`, `$item_lightning_sword`, `$item_shield_of_frost`,
  `$item_mountain_spiritfire_axe`, `$item_exploding_sledge`, etc.) is an
  unlocalized token - in-game, these will likely show as the literal raw
  string, not readable text, until a `Translations/English.json` (or
  Jötunn's localization API) is added.
- Nothing in this mod has been run in an actual Valheim session. Every
  "confirmed" fact above was confirmed via someone else's source code, not
  by observing this mod's actual behavior.

## ShieldOfFrost.cs / ShieldOfFrostPatches.cs

**Re-tiered from Deep North to Mistlands** per explicit direction (useful
against Seekers' ranged fire attacks), priced with Freeze Gland + Refined
Eitr - the confirmed real materials for "Staff of Frost", the Mistlands
frost staff. This shield is a melee/block echo of that staff, not a
replica of its exact recipe/cost, and the recipe quantities are estimates
same as everywhere else in this mod.

**Now visually cloned from `ShieldIronBuckler`** ("Iron Buckler", confirmed
real prefab), per explicit direction, replacing the earlier unconfirmed
`ShieldCarapace` guess - this is a confirmed-real source now, not a guess.
A silver-ish tint (`SilverTint`) is applied via
`MaterialPropertyBlock.SetColor("_Color", ...)` - a real, documented
technique (verified against `Rexabit/valheim-visuals-modifier`, a working
recolor mod that uses the exact same approach on the same shader
property), but **the actual visual result was never seen or verified in
this environment** - only the technique is confirmed, not that this
specific shade/approach looks right on this specific shield. Check this
first at the desktop; it degrades silently to the source item's original
color on any exception rather than breaking the item.

A "frost enchant glow" VFX was explicitly requested alongside this but
**deliberately not attempted**: research found only one real precedent
(`naomi-nada/nada-vfx-weapon`), a whole dedicated per-item particle VFX rig
system still in active development/preview upstream, not a simple
attach-and-done API. This needs meaningful engineering effort and visual
iteration this remote environment can't do - left as an open idea for the
desktop session.

This is the highest-risk file in the mod - four distinct mechanics
(continuous Eitr drain on a held input, a parry-detection proc, a
block-break proc, and a rotation-swap trick for omnidirectional blocking),
each landing on a real confirmed vanilla method/field, but several
supporting assumptions were never independently re-verified this session:

- **`Humanoid.UseEitr(float)`** (the Eitr-drain-while-blocking patch) is an
  assumption by analogy with the confirmed `UseStamina` pattern and the
  confirmed `UpdateAttackBowDraw` Eitr-drain precedent - the exact method
  name/signature wasn't independently re-verified this session.
- **`m_leftItem`** as the private field holding the equipped shield is an
  assumption by analogy with the `m_rightItem` assumption used elsewhere in
  this mod (also never freshly confirmed). If wrong, every gating check in
  `ShieldOfFrostPatches.cs` silently no-ops (treats the shield as never
  equipped) rather than erroring - so a "nothing happens" bug here likely
  means this field name is wrong.
- **`Character.Damage(HitData)`** (used to apply the frost proc directly to
  the parried attacker) is an extremely common pattern across Valheim
  modding generally, but wasn't decompile-confirmed in this project's own
  research threads specifically.
- **The AoE burst is spawned via raw `UnityEngine.Object.Instantiate`**,
  not through Valheim's own `ZNetScene` spawn path. The burst prefab
  carries networked components (`ZNetView`/`ZSyncTransform`, per earlier
  research on this same projectile type) - a raw `Instantiate` may not
  register/replicate correctly in multiplayer. Single-player should still
  work. This is the single biggest open risk in this file.
- **The omnidirectional-block rotation trick was never visually verified.**
  It temporarily rotates the wielder to face directly away from an
  off-angle hit for the duration of `BlockAttack`, then restores their real
  rotation immediately after (same "swap state, call original, restore"
  pattern as `QualityTransferPatch.cs`, applied to rotation instead of item
  data) - confirmed to satisfy vanilla's exact frontal-arc check
  (`Vector3.Dot(hit.m_dir, transform.forward) > 0`), but a single-frame
  rotation snap could be visually noticeable or interact oddly with camera/
  animation systems in ways static code review can't catch.
- **"Double damage on parry" is deliberately NOT implemented as custom
  code.** Research confirmed this already happens automatically in vanilla
  for any successful parry against any shield (a perfect block staggers
  the attacker, and `Character.cs` doubles any hit landed on a currently-
  staggering non-player target) - this mod only adds the frost proc on top.
  If parrying with this shield doesn't feel like it's doing "double
  damage," that's most likely this existing vanilla mechanic not
  triggering as expected (e.g. the follow-up hit landing after the stagger
  window closes), not a missing feature.
- Eitr drain rate (4/sec), parry frost damage (15), and the shield's own
  recipe/upgrade numbers are arbitrary starting points, same as every other
  tuning number in this mod.
- The Staff of Protection's bubble VFX prefab name (for a "smaller bubble"
  visual) was only partially confirmed - two real child GameObject names
  (`vfx_StaffShield(Clone)`, `fx_shield_start(Clone)`) were found via a
  real mod's source, but the top-level prefab/StatusEffect asset name to
  actually clone was not. This mod currently has NO custom visual for the
  shield's block/parry/break effects - it reuses the frost burst
  projectile's own VFX for the break effect only. A bubble visual is still
  an open idea, not implemented.

## CORRECTION: GrapplingHook is Mistlands tier, not Deep North

Every reference in this file and the README originally described
`GrapplingHook` as Deep North content - **this was wrong**, caught by the
user and independently confirmed via 4+ cross-referenced sources (high
confidence): the Grappling Hook is entirely **Mistlands**-tier. It's
crafted at the Black Forge from Yggdrasil Wood + Refined Eitr + Mandibles,
plus a non-craftable "Hook" component looted from a Dvergr Treasure Chest
in a Mistlands Infested Mine - all four inputs are Mistlands materials,
none are Deep North. The "Deep North" framing in the original research
conflated the hook's usefulness for navigating that biome's vertical
dungeons with where it's actually obtained (it's a tool carried forward
from Mistlands, not a Deep North unlock).

**Follow-up decision, now implemented**: per explicit direction, this
correction was used to actually move Grapple Knuckles from Deep North to
**Ashlands** - filling the real "no Ashlands fist weapon exists" gap noted
below - rather than leaving it at Deep North. See
`GrappleKnucklesPlugin.cs`'s section above for the current recipe/design.

## Note: no Ashlands-tier fist weapon exists in vanilla

A survey confirmed (reasonably well-supported via WebSearch, not from a
primary/decompiled source) that no vanilla Ashlands fist weapon exists -
the full real `Fist*` roster is `FistBjornClaw` (Meadows),
`FistBjornUndeadClaw` (Plains), `FistFenrirClaw` (Mountain, lower
confidence), and the Deep North `FistGold` family. A community discussion
is cited as explicitly noting fist weapons have gone multiple biomes
without a new entry. Grapple Knuckles (see above) now fills this gap,
per explicit direction, once the `GrapplingHook` tier correction made an
Ashlands placement possible without a progression-ordering conflict.
