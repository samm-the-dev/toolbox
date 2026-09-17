# Grapple Knuckles

Valheim (1.0.7, post-Deep North) BepInEx + HarmonyX + Jötunn (JVL) mod. Has
grown from a single item into a small "fast mage" toolkit, tiered to match
real vanilla elemental-weapon progression (confirmed via a dedicated
survey - see `CLAUDE.md` for the full research trail):

**Test-pass status (2026-09-17): only Grapple Knuckles is registered.**
Testing one item at a time rather than the whole mod at once - everything
else below is held back (code untouched, just commented out of
`GrappleKnucklesPlugin.cs`'s `Awake`/`OnDestroy`) until each is verified
working in-game.

| Tier | Item | Notes |
|---|---|---|
| Mountain / Silver | `MountainTierAxe` - fire+spirit axe | No Eitr spell (Eitr doesn't exist yet); pairs with Fenris Mage armor. **Held back, not registered** |
| Mistlands | Fenris Mage armor | Fast-mage hybrid armor set (built from the light Fenris set). **Held back, not registered** |
| Mistlands | Fire Dagger | Eitr-costed fire bolt secondary. **Held back, not registered** |
| Mistlands | Shield of Frost | Eitr-channel block, parry/break procs. **Held back, not registered** |
| Mistlands | Grappling Hook | Unmodified vanilla item - just the normal progression step |
| Ashlands | Ashlands Hybrid Armor | Fast-mage hybrid armor set (built from the real "Embla" mage armor). **Held back, not registered** |
| Ashlands | Lightning Sword | Reuses Dundr's own bolt, tuned faster/weaker. **Held back, not registered** |
| Ashlands | Exploding Sledge | Frost splinter-burst secondary. **Held back, not registered** |
| Ashlands | Grapple Knuckles | The original item - the Ashlands "upgrade" to the Mistlands hook. **Active - the only item in this test pass** |
| Deep North | Deep North Hybrid Armor | Fast-mage hybrid armor set (built from the real "Caller" mage armor). **Held back, not registered** |
| Deep North | Prism Blade | Endgame weapon; cycles active element (fire/frost/lightning/poison) on secondary attack. **Held back, not registered** |

**Grapple Knuckles** (`FistGold_Grapple`) is the intended narrative:
you get the plain vanilla **Grappling Hook** (`GrapplingHook`) easily in
Mistlands as a normal traversal tool, then in Ashlands you can craft an
upgrade - a real fist weapon (modeled on **Nord Knucklechains**/`FistGold`,
Deep North, used for its look/mechanics only - see below) whose secondary
attack launches the hook faster than the plain version, while still
working as a proper melee weapon on its own.

The Grappling Hook was originally (incorrectly) assumed to be Deep North
content - it's actually entirely **Mistlands**-tier (Black Forge,
Yggdrasil Wood, Refined Eitr, Mandibles, confirmed via 4+ cross-referenced
sources). That correction is what made the Ashlands placement below make
sense: Grapple Knuckles' recipe deliberately does **not** require an
actual `FistGold` (that would gate an Ashlands, pre-Deep-North item behind
Deep North) - it only clones `FistGold` as Jötunn's model/mechanics
template, then prices the real recipe with Ashlands materials + the
Grappling Hook instead. See `CLAUDE.md` for the full history of this
correction.

Grapple Knuckles trades Knucklechains' normal special move (or the
Frostfire/Thunderblood elemental enchant path) for the grapple utility, a
pierce damage bonus on the grapple hit itself, a fast reload, and a
movement speed bonus, all deliberately tuned for fun over strict balance
(see `GrappleAttackPatch.cs`). The recipe no longer consumes a real
`FistGold` at all - see the intro above for why - so there's no quality
level to carry over from an ingredient; the crafted item starts at quality
1 like any other new recipe output.

## Prism Blade (`PrismBlade.cs` / `PrismBladePatches.cs`)

**Held back from this test pass, 2026-09-17**: `PrismBlade.Clone` is
commented out of `GrappleKnucklesPlugin.cs`'s registration, so this item
won't appear in-game for now - the user wants to test Grapple Knuckles and
the other items first and has separate ideas for Prism Blade to revisit
later. The code below is untouched and still builds; only the registration
call is disabled (see `GrappleKnucklesPlugin.cs`'s `Awake`/`OnDestroy`).
The Harmony patches in `PrismBladePatches.cs` stay active but are harmless
no-ops for every other item, since both gate on `IsPrismBlade()`.

An endgame Deep North two-handed sword (clones `THSwordGold`, "Nord
Greatsword") whose active elemental damage type cycles on secondary attack
use: Fire (default) -> Frost -> Lightning -> Poison -> back to Fire. Every
hit deals damage in whichever element is currently active, and only that
element - not all four stacked together.

Two mechanics, both confirmed via dedicated research this session:

- **Damage override**: a Harmony Postfix on
  `ItemDrop.ItemData.GetDamage(int, float)` reads the wielded item's
  `m_variant` field (a real, already-persistent int Valheim itself saves
  to both ZDOs and inventory data) to determine the active element, then
  zeroes the other three elemental damage fields on the returned
  `HitData.DamageTypes` and applies a flat bonus to only the active one.
  This is safe per-instance - `HitData.DamageTypes` is a struct, so
  overriding it in a Postfix never touches the shared `SharedData` every
  Prism Blade instance points at. Real precedent: the EpicLoot mod patches
  this exact method the same way to implement its own damage-conversion
  enchantments.
- **Element cycling**: a Harmony Postfix on
  `Humanoid.StartAttack(Character, bool)`, gated on
  `secondaryAttack && __result`, increments `m_variant` and costs a small
  amount of Eitr. `StartAttack` is polled every physics tick while the
  attack button is held but only returns `true` once per actual swing, so
  gating on both the `secondaryAttack` parameter and the return value
  fires the cycle exactly once per secondary-attack use, not once per
  frame. Real precedent: the `SecondaryAttacks` mod hooks this same
  method the same way.

Priced with Bloodgold + Nornathread, the same Deep North material family
used elsewhere in this project's research (exact prefab spelling not
independently confirmed, degrades gracefully if wrong).

**Status:** implemented, untested in-game. See `CLAUDE.md` for the full
list of unconfirmed assumptions (mainly `m_rightItem` as the two-handed
weapon slot field, `UseEitr`'s exact signature, and whether
`MessageHud.ShowMessage` is the right call to announce the swap).

## How the secondary attack override works

Rather than Harmony-patching `Humanoid.StartAttack` and reimplementing the
hook's raycast/pull/rope physics, `GrappleAttackPatch.cs` patches
`ObjectDB.UpdateRegisters` (the same extension point other Valheim mods use
to tweak item stats after load) and **wholesale-clones** the real
`GrapplingHook` item's own `m_secondaryAttack` config via `Attack.Clone()`
(a real vanilla method) onto the clone's secondary attack slot, pointed at
a **cloned** grapple projectile (see below) — every projectile-launch
field (velocity, accuracy, spawn geometry, etc.) comes along automatically
this way, rather than needing to be hand-picked one at a time. Only
`m_attackAnimation` (Knucklechains' own basic punch, per explicit
direction — not the hook's crossbow animation, not Knucklechains' kick),
`m_attackProjectile` (our own cloned projectile), and `m_reloadTime`
(intentional "faster" tuning) are overridden on top of the clone.
`m_attackType` also needs to come along as `Projectile` from that clone -
`Attack.OnAttackTrigger()`'s dispatch is a plain switch on this field, and
an earlier version that started from Knucklechains' own kick Attack
instead left it at a melee type, so the projectile-spawn code was never
reached at all regardless of animation. Confirmed via decompile,
2026-09-17 — see `CLAUDE.md` for the full history of this fix (it went
through a few wrong turns before landing here).

**The flying projectile isn't what actually grapples.** Confirmed via the
real `Projectile_GrapplingHook.prefab` data: on hit, it spawns a
**separate** prefab, `GrapplingPoint` (rope `LineRenderer`,
pull-toward-anchor logic, and — critically — a per-frame check that
self-cancels the grapple if you're not holding a matching item). This mod
now clones `GrapplingPoint` too, not just the flying projectile
(`GrappleKnucklesPlugin.ClonedGrapplingPoint`), and points the cloned
projectile's `m_spawnOnHit` at that clone instead of the original shared
one — otherwise every throw would still spawn the vanilla `GrapplingPoint`,
whose equip-check is hardcoded to the real `GrapplingHook` item and would
immediately break the grapple while wielding Grapple Knuckles instead. See
`CLAUDE.md` for the full mechanism.

**Not yet attempted: anchoring the rope's visual start point at the
wrists.** That likely needs a custom attach-point Transform on the fist
model and/or inspecting how `GrapplingPoint`/the projectile spawn actually
picks its origin — real decompile work on your end, not something
guessable from other mods' source.

## Why the projectile is cloned, not reused directly

`Projectile_GrapplingHook` (the vanilla hook's projectile GameObject) is
the *same* prefab reference the real `GrapplingHook` item uses. Mutating
its `Projectile.m_damage` (confirmed field) in place would buff vanilla
hook throws for every player, modded or not. So `GrappleKnucklesPlugin.cs`
clones it once via Jötunn's `PrefabManager.CreateClonedPrefab` (the
confirmed, idiomatic API for cloning a vanilla prefab without touching the
original), on `PrefabManager.OnVanillaPrefabsAvailable`, and
`GrappleAttackPatch.cs` points the clone's secondary attack at that
independent copy before adding the pierce bonus to it.

## Damage / feel tuning

All in `GrappleAttackPatch.cs`. The base melee damage is now data-driven
from real extracted game values (AssetRipper pass, see `CLAUDE.md`); the
rest is deliberately tuned for fun over strict balance, per explicit
request:

- **Base melee damage scaled from `FistGold`'s real 114 blunt down to ~95
  blunt** (`SharedData.m_damages.m_blunt`, `BaseBluntDamageScale = 95f /
  114f`, applied proportionally so it stays correct if `FistGold`'s own
  stats change). 114 blunt is `FistGold`'s real, ground-truth Deep North
  damage - too strong for an Ashlands item. 95 sits at 0.70x the real
  Ashlands pure-blunt one-hander (`MaceEldner`, 135 blunt), consistent with
  real vanilla fist weapons across every tier landing ~0.6-0.7x (sometimes
  up to 0.84x) a same-tier one-hander's damage - a real, researched pattern
  now, not a guess.
- **+40 pierce damage on the grapple projectile only** (`Projectile.m_damage.m_pierce`
  on the cloned projectile, separate from the melee `m_damages` above).
  Compensates for forgoing Frostfire/Thunderblood's elemental damage. This
  number is still a starting point for playtesting, not a researched
  balance target — the enchanted variants' real damage figures could only
  be corroborated via web search snippets, not primary source.
- **Reload time cut to 40%** of the vanilla hook's (`ReloadTimeMultiplier = 0.4f`),
  framed as "a little faster to grapple" than the plain Mistlands hook,
  matching the item's Ashlands-upgrade narrative.
- **+10% movement speed** while equipped (`SharedData.m_movementModifier`,
  confirmed field — a flat additive fraction summed across all equipped
  items, same mechanism as Wolf/Troll armor's speed penalty but positive
  here).

**Status:** item clone/recipe, attack-wiring (including the cloned
projectile, animation reuse, and Black Forge station), and all the
damage/feel tuning above are implemented, and the project now **builds
clean** against the user's real game/BepInEx/Jötunn assemblies (see
`CLAUDE.md`'s decompile verification pass). Still untested in an actual
running game — treat this as a first pass to verify in-game, not a
finished/verified mod.

## Fenris Mage armor (`FenrisMageArmor.cs`)

**Held back from this test pass, 2026-09-17**: `FenrisMageArmor.Clone` is
commented out of `GrappleKnucklesPlugin.cs`'s registration, along with the
Ashlands/Deep North hybrid armors below - the user wants to develop the
mage/hybrid armor ideas further before testing them. Code is untouched and
still builds; only registration is disabled.

A second, independent item set: a "fast mage" hybrid cloned from vanilla
**Fenris armor** (`ArmorFenringChest` + `ArmorFenringLegs` — the only two
real Fenris prefabs; wiki claims of a third Hood piece don't correspond to
anything in the confirmed vanilla prefab list), scaled up toward Mistlands
power level. Fenris turned out to actually be Mountain-tier (same as Wolf
Armor), not Mistlands-tier as originally assumed — a correction worth
knowing if you go looking for it in-game.

Design, per piece:
- **Armor and weight scaled relative to whatever the clone inherits from
  vanilla Fenris** (`ArmorScale = 1.6f`, `WeightScale = 1.2f`), rather than
  hardcoded absolute numbers — real Fenris/Padded/Carapace armor values were
  only ever community-sourced, never primary-confirmed (Jötunn's item list
  has no armor/weight columns at all), so scaling relative to the real
  inherited value stays correct regardless of what that baseline actually
  is.
- **+20% Eitr regen per piece** (`SE_Stats.m_eitrRegenMultiplier`, confirmed
  field), deliberately less than the real Mistlands "Eitr-weave" mage set's
  per-piece bonus (+40% on its own robe/trousers) — a trade-off, not a
  straight mage-armor clone.
- **+5% movement speed per piece** (`SharedData.m_movementModifier`,
  confirmed field, same mechanism used on Grapple Knuckles), +10% total for
  the full set.
- **+25% stamina regen set bonus** when both pieces are worn
  (`SE_Stats.m_staminaRegenMultiplier` on a custom set-bonus effect) — covers
  both dodging and melee swings, so the build can hold its own in melee with
  some proficiency rather than being a pure kiting caster.

How the set bonus is wired: `ItemDrop.ItemData.SharedData` has
`m_setName`/`m_setSize`/`m_setStatusEffect` fields that drive full-set
bonuses entirely in vanilla code (`Humanoid.UpdateEquipmentStatusEffects()`,
confirmed via decompile and cross-checked against a real mod's Harmony
transpiler targeting that exact method) — no Harmony patch needed here,
just setting matching fields on both cloned pieces. The clone inherits
vanilla Fenris's own set fields by default, so both pieces explicitly
override them to a new `FenrisMageSet` (size 2) to detach from the real
Fenris Blessing set bonus rather than accidentally combining with it.

Custom status effects (`SE_Stats`) are created via
`ScriptableObject.CreateInstance<SE_Stats>()` and registered through
Jötunn's `ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(...))`
— confirmed as the real, Jötunn-documented path, with a working precedent
in `aedenthorn/ValheimMods`' `CustomArmorStats` plugin doing the exact same
thing.

**Also researched but not used:** a set bonus of -10% dodge stamina cost /
+5% elemental damage was considered first. Both map to real confirmed
fields (`SE_Stats.m_dodgeStaminaUseModifier`, real vanilla precedent: the
Deep North Vanguard set's -20% dodge stamina; and
`SE_Stats.m_percentigeDamageModifiers`, a per-damage-type struct — set only
the elemental sub-fields to get an elemental-only bonus, unlike Yagluth's
Forsaken Power which is a flat +10% to all damage types, not elemental-only
despite the memory that prompted checking it). Noted here in case the
stamina-regen version doesn't feel right in practice and this is worth
revisiting.

**Status:** implemented, untested in-game, same caveats as the rest of this
mod.

## Ashlands & Deep North Hybrid Armor (`AshlandsHybridArmor.cs` / `DeepNorthHybridArmor.cs`)

**Held back from this test pass** - see the Fenris Mage armor section
above; same reasoning, both `AshlandsHybridArmor.Clone` and
`DeepNorthHybridArmor.Clone` are commented out of registration.

Fast-mage hybrid armor for the other two tiers, filling the role Fenris
Mage Armor fills at Mountain tier - but approached from the opposite
direction. Fenris started as a light, *non-mage* set and gained partial
Eitr regen built from scratch; these clone the **real mage armor that
already exists at each tier** - "Embla" (Ashlands) and "Caller" (Deep
North), both confirmed real prefabs - and trade a slice of their own
native armor for movement speed, while keeping their real Eitr regen
intact rather than rebuilding it. Both add the same +25% stamina regen set
bonus as Fenris Mage Armor, for the same melee-viability reasoning.

**Biggest assumption**: `m_equipStatusEffect` is deliberately left
untouched on these clones (unlike Fenris Mage Armor, which builds a brand
new one), on the theory that Jötunn's clone inherits it automatically and
Embla/Caller's own Eitr regen lives there - this was never independently
verified, and if their Eitr regen actually comes through their *set*
bonus instead (which these files do overwrite, to install the stamina
bonus), these hybrid pieces would silently lose all Eitr regen. **Check
this first**: equip one piece alone and see if Eitr regen is still
boosted.

**Status:** implemented, untested in-game.

## Elemental weapons (`ElementalWeapons.cs` / `ElementalWeaponAttackPatch.cs`)

Two more mage-flavored melee weapons: a **Fire Dagger** (cloned directly
from `KnifeGold`/"Nord Dagger" - an earlier version tried cloning
`KnifeSkollAndHati` for a dual-wield animation plus a mesh reskin, but that
was dropped per explicit direction in favor of just cloning the dagger
whose appearance was wanted in the first place), and a **Lightning Sword**
(cloned from `SwordGold`/"Nord Sword"). The frost half of the original
fire/frost dagger pair idea moved to a separate shield instead (see below).
Each weapon's secondary attack fires a small, Eitr-costed bolt - a
scaled-down clone of a real vanilla staff projectile, same "reuse vanilla's
own spell/VFX instead of reimplementing it" approach as Grapple Knuckles'
hook launch:

- Fire Dagger's bolt clones "Staff of Embers"' projectile
  (`staff_fireball_projectile`).
- Lightning Sword's bolt clones "Dundr" the lightning staff's projectile
  (`staff_lightning_projectile`).

Both are cloned at half scale and wired via the same
`ObjectDB.UpdateRegisters`-postfix pattern used for Grapple Knuckles, with
`Attack.m_attackEitr` (confirmed real field, alongside the already-used
`m_attackStamina`) giving the secondary attack an Eitr cost instead of a
pure stamina one - the first Eitr-gated melee attack in this mod.

The Fire Dagger gets a flat +20 fire damage bonus on `SharedData.m_damages`
rather than a separate on-hit effect: elemental damage inherently procs the
matching vanilla status effect (burning) once it's above zero, the same
mechanism the real Frostfire-enchanted weapons use, so no extra wiring is
needed. The Lightning Sword gets +20 lightning damage the same way, and its
bolt is deliberately weaker than Dundr's own cast (`LightningBoltDamageMultiplier = 0.5f`)
and fires on a faster 1s reload, per explicit "faster and weaker, no
loading mechanic" direction - it never had a charge/draw mechanic to begin
with, since this patch never sets those Attack fields.

**Tiering**, after a full survey of real vanilla elemental weapons/staves
confirmed where things actually belong: **Fire Dagger is Mistlands-tier**
(priced with Surtling Core + Refined Eitr, "Staff of Embers"' real
materials) and **Lightning Sword is Ashlands-tier** (priced with Flametal +
Bloodstone + Charred Bone, modeled on Dyrnwyn/Nidhögg's real recipes).
Both still clone from their original Nord-tier item (`KnifeGold`/
`SwordGold`) for model/mechanics only - no confirmed Mistlands-native
dagger or Ashlands-native one-handed sword exists to clone from instead,
and only the recipe/station changed. Both crafted at the **Black Forge**
still - confirmed real for weapon-tier crafting in both Mistlands and
Ashlands (e.g. Himminafl, Dyrnwyn, Nidhögg all use it too), not exclusive
to Deep North despite where this mod first used it.

Recipe quantities are estimates informed by real comparable recipes, not
exact copies (these are new items, not replicas of any single real one) -
see `CLAUDE.md` for the specific numbers and their sourcing.

**Status:** implemented, untested in-game, same caveats as the rest of this
mod - see `CLAUDE.md` for the full list of what to verify.

## Shield of Frost (`ShieldOfFrost.cs` / `ShieldOfFrostPatches.cs`)

The frost identity from the original fire/frost dagger idea, moved to its
own item: a shield visually cloned from **Iron Buckler**
(`ShieldIronBuckler`, confirmed real) with a silver-ish tint applied via
`MaterialPropertyBlock` (a real, documented recolor technique - result
unverified visually, see `CLAUDE.md`), Mistlands-tier, priced with Freeze
Gland + Refined Eitr (Staff of Frost's real materials) - useful against
the Seekers' ranged fire attacks. A "frost enchant glow" VFX was requested
too but deliberately not attempted - the only real precedent found is a
whole dedicated particle-rig mod, not a simple attach; left for the
desktop session where the result can actually be seen. There's also a real
in-game precedent worth checking first: the vanilla Deep North item
"Northern Vengeance" already ships a frost-orb VFX, which may be clonable
directly instead of building a new particle rig. Three mechanics, each
hooked onto a real confirmed vanilla method rather than reimplemented:

- **Channels Eitr while blocking** instead of vanilla's zero-cost idle
  block, mirroring the confirmed real precedent for continuous per-tick
  resource drain on a held input (`Player.UpdateAttackBowDraw()`'s Eitr
  drain while charging a bow).
- **Frost proc on a successful parry.** Deliberately does *not* implement
  "double damage" as custom code - research confirmed vanilla already does
  this automatically for any parry against any shield (a perfect block
  staggers the attacker, and any hit landing on a currently-staggering
  non-player target already gets doubled in vanilla). This shield just adds
  a frost hit on top, at the same moment.
- **Frost AoE burst when the block breaks** (a hit exceeds the shield's
  block power) - reuses the same "Staff of Fracturing" projectile clone
  technique as the other weapons, scaled down and spawned at the wielder's
  position.

`Humanoid.UseEitr`, `m_leftItem`, `Character.Damage`, and the
`UpdateBlock`/`BlockAttack` patch targets were all decompile-confirmed
against the user's real `assembly_valheim.dll` (see CLAUDE.md). The
remaining open risk: the AoE burst spawns via a raw `Object.Instantiate`
rather than Valheim's own network-aware spawn path, which may not
replicate correctly in multiplayer (single-player should be fine). An
earlier omnidirectional-blocking mechanic (a rotation trick to bypass
vanilla's frontal-arc block check) was designed and confirmed sign-correct,
then dropped per explicit direction as too fiddly before ever being tried
in-game.

**Status:** implemented, untested in-game.

## Mountain-tier Axe (`MountainTierAxe.cs`)

A Silver/Mountain-tier axe with innate fire+spirit damage and **no Eitr
spell at all** - Eitr doesn't exist yet at this tier, matching the real
vanilla "Frostner" (`MaceSilver`) pattern: baked-in elemental damage, fully
craftable, no enchant material needed. A dedicated survey confirmed no
vanilla axe has ever had innate elemental damage, and axes skip the
Silver/Mountain tier entirely in vanilla (no `AxeSilver` exists) - so
there's no real base item to clone from. Clones `AxeIron` instead and
scales its damage up (`DamageScale = 1.8f`, a multiplier on whatever
`AxeIron` actually has, not a hardcoded absolute - same technique as Fenris
Mage armor) toward Frostner's confirmed real power level, then adds fire
and spirit damage on top (spirit matches the tier's existing vanilla
identity - Frostner and the Silver Sword both have it).

Pairs with the existing Fenris Mage armor (already Mountain-tier) - no new
armor needed for this tier.

**Status:** implemented, untested in-game. Station name and recipe
quantities are unconfirmed guesses - see `CLAUDE.md`.

## Exploding Sledge (`ExplodingSledge.cs`)

An Ashlands sledge. Normal attacks are untouched vanilla `SledgeGold`
cleave - "just the usual sledge AoE," per explicit direction, no per-hit
explosion. Only the secondary attack changes: a single frost burst sized
like one of "Staff of Fracturing"'s splinter sub-munitions
(`staff_clusterbombstaff_splinter_projectile` - deliberately the smaller
child projectile, not the main multi-splinter barrage), damage scaled up
slightly above a single splinter's own strength. Priced with Flametal +
Charred Bone, the same Ashlands material family as the Lightning Sword's
re-tier.

**Status:** implemented, untested in-game.

## Dev environment setup

You need your own legally-owned copy of Valheim; none of the game or
BepInEx binaries are (or should be) committed to this repo.

1. **Install Valheim** via Steam.
2. **Install BepInEx** for Valheim. Easiest path: install
   [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/) or the
   Thunderstore Mod Manager, create a profile, and install
   `denikson-BepInExPack_Valheim`. That gives you a working BepInEx layout
   (`BepInEx/plugins`, `BepInEx/core`, etc.) without hand-patching anything.
   Alternatively, download the BepInEx pack directly from Thunderstore and
   extract it into your Valheim install directory (same folder as
   `valheim.exe`), then run the game once so BepInEx generates its config.
3. **Install Jötunn** the same way (`ValheimModding-Jotunn` on Thunderstore)
   into the same profile/install, so `Jotunn.dll` ends up in
   `BepInEx/plugins`.
4. **Reference assemblies for compiling against.** Copy the following DLLs
   from your local Valheim install into `GrappleKnuckles/Libraries/` (this
   folder is gitignored — never commit them):
   - `valheim_Data/Managed/assembly_valheim.dll`
   - `valheim_Data/Managed/assembly_lib.dll`
   - `valheim_Data/Managed/assembly_utils.dll`
   - `valheim_Data/Managed/UnityEngine*.dll` (as needed)
   - `BepInEx/core/BepInEx.dll`
   - `BepInEx/core/0Harmony.dll`
   - `BepInEx/plugins/Jotunn/Jotunn.dll`

   Valheim ships IL2CPP-adjacent but Mono-built assemblies that are not
   pre-publicized; most fields/methods you'll need for this mod are already
   public, but if you hit `private`/`internal` members you need to touch,
   run a publicizer (e.g. `BepInEx.AssemblyPublicizer` /
   `Jotunn.PatcherModules`, or standalone `AssemblyPublicizer`) over
   `assembly_valheim.dll` first and reference the publicized copy instead.

5. **Recommended: confirm against a decompile of your own game files**
   with [ILSpy](https://github.com/icsharpcode/ILSpy) or dnSpy against
   `assembly_valheim.dll`. `GrappleAttackPatch.cs` was written from facts
   confirmed in other open-source mods' compiled source (see that file's
   comments), not a direct decompile, so it's worth cross-checking
   `ItemDrop.ItemData.SharedData.m_secondaryAttack` on the `GrapplingHook`
   prefab and the `Attack`/`GrapplingPoint` classes against your own
   decompile before relying on this in a real playthrough.

## Build

Open `GrappleKnuckles.sln` in Visual Studio / Rider, or run:

```
dotnet build GrappleKnuckles/GrappleKnuckles.csproj -c Release
```

## Install

Copy the built `GrappleKnuckles.dll` into `<Valheim install>/BepInEx/plugins/GrappleKnuckles/`.

## Project layout

- `GrappleKnucklesPlugin.cs` - BepInEx plugin entrypoint; clones `FistGold`
  (model/mechanics only, not a crafting requirement) into `FistGold_Grapple`
  via Jötunn's `ItemManager`/`CustomItem`/`ItemConfig`, with an Ashlands-
  tier recipe of `GrapplingHook` + Flametal + Charred Bone at the Black
  Forge, and clones `Projectile_GrapplingHook` via
  `PrefabManager.CreateClonedPrefab`.
- `GrappleAttackPatch.cs` - Harmony patch on `ObjectDB.UpdateRegisters` that
  wires the clone's secondary attack to the cloned grapple projectile,
  reuses the item's own attack animation, and applies the pierce/reload/
  movement-speed tuning.
- `FenrisMageArmor.cs` - clones `ArmorFenringChest`/`ArmorFenringLegs` into
  a fast-mage hybrid set, creating and registering custom `SE_Stats` status
  effects for per-piece Eitr regen and the set's stamina regen bonus. No
  Harmony patch needed for this one - set bonuses are stock vanilla
  behavior once the right `SharedData` fields are set.
- `AshlandsHybridArmor.cs` / `DeepNorthHybridArmor.cs` - same pattern,
  cloning the real Embla/Caller mage armor instead and trading armor for
  speed rather than building Eitr regen from scratch.
- `ElementalWeapons.cs` - clones the Fire Dagger (from `KnifeGold`) and
  Lightning Sword (from `SwordGold`), their recipes, elemental damage, and
  the cloned/scaled-down bolt projectiles.
- `ElementalWeaponAttackPatch.cs` - Harmony patch on `ObjectDB.UpdateRegisters`
  that wires each weapon's secondary attack to its bolt projectile with an
  Eitr cost.
- `ShieldOfFrost.cs` - clones the shield item and the frost burst
  projectile used for the block-break effect.
- `ShieldOfFrostPatches.cs` - Harmony patches on `Humanoid.UpdateBlock`/
  `BlockAttack` for the Eitr-channel, frost-on-parry, and break-AoE
  mechanics.
- `MountainTierAxe.cs` - clones `AxeIron` into a Silver-tier fire+spirit
  axe with scaled-up damage, no Eitr spell.
- `ExplodingSledge.cs` - clones `SledgeGold` and a splinter burst
  projectile (from Staff of Fracturing's sub-munition) for its secondary
  attack; includes its own `ObjectDB.UpdateRegisters` Harmony patch.
- `PrismBlade.cs` - clones `THSwordGold` into an endgame Deep North
  cycling-element sword; sets its default active element via the real,
  persistent `m_variant` field.
- `PrismBladePatches.cs` - Harmony patches on
  `ItemDrop.ItemData.GetDamage` (per-instance active-element-only damage
  override) and `Humanoid.StartAttack` (cycles the active element once
  per secondary attack).
- `manifest.json` - Thunderstore package manifest.
- `Libraries/` - local-only reference DLLs (gitignored, not committed).
