# Grapple Knuckles

Valheim (1.0.7, post-Deep North) BepInEx + HarmonyX + Jötunn (JVL) mod.

Crafted by combining **Nord Knucklechains** (`FistGold`) + the vanilla
**Grappling Hook** (`GrapplingHook`, added in the Deep North update) at the
forge, into a new item, **Grapple Knuckles** (`FistGold_Grapple`), whose
secondary attack launches the grapple hook instead of Knucklechains'
normal special move.

Grapple Knuckles is meant as an **alternative to enchanting** Knucklechains
into Frostfire (`FistGold_FrostFire`) or Thunderblood
(`FistGold_BloodLightning`), not a further upgrade of them — the recipe
only accepts plain `FistGold`. You trade the elemental proc for the grapple
utility, plus a flat pierce damage bonus to keep damage output in the same
ballpark as the enchanted variants (see `GrappleAttackPatch.cs`). If you
craft from an upgraded (higher-quality) Knucklechains, that quality level
carries over to the Grapple Knuckles (see `QualityTransferPatch.cs`) —
vanilla has no built-in mechanism for this since it's a different item, so
that's a from-scratch Harmony patch on the crafting flow.

## How the secondary attack override works

Rather than Harmony-patching `Humanoid.StartAttack` and reimplementing the
hook's raycast/pull/rope physics, `GrappleAttackPatch.cs` patches
`ObjectDB.UpdateRegisters` (the same extension point other Valheim mods use
to tweak item stats after load) and copies the real `GrapplingHook` item's
own `m_secondaryAttack` config (its `Attack.m_attackProjectile`, stamina
cost, and reload time) onto the clone's secondary attack slot. Since
`m_attackProjectile` is what actually drives the vanilla `GrapplingPoint`
component (rope `LineRenderer`, pull-toward-anchor logic), this reuses
100% of vanilla's launch mechanic and VFX for free — no physics
reimplementation, and no extra animation/VFX wiring needed beyond this.
Knucklechains' own punch animation is deliberately left in place for the
secondary attack (not overwritten with the hook's crossbow-draw
animation), so the item plays as "punch, then a hook flies out."

**This is implemented against facts confirmed from other open-source
Valheim mods' compiled source** (see PR/commit description for sources),
not a direct decompile of the game assembly, since I don't have access to
your local Valheim install. The riskiest untested assumption: Valheim's
attack-animation-event callback that fires the configured `Attack` is
generic across weapon types, so Knucklechains' punch clip should still
trigger the copied grapple `Attack` even though it wasn't authored for a
crossbow. **Verify this in-game first** — if the secondary attack does
nothing on impact, that assumption is the first thing to check (try
temporarily copying `m_attackAnimation` from the hook's `Attack` too, to
confirm whether it's an animation-event gating issue).

## Quality transfer on craft

`QualityTransferPatch.cs` patches `InventoryGui.DoCrafting(Player)` (private,
confirmed via a decompile). Vanilla's quality-carry mechanism
(`m_craftUpgradeItem`) only applies when re-crafting a recipe whose output
matches an item the player already owns — the in-place "upgrade at the
forge" case — and never fires for a recipe like ours where the output is a
different item from the input. So this patch:

1. **Prefix** — if the recipe being crafted is ours, find the player's
   `FistGold` (highest quality, if they somehow have more than one — vanilla
   doesn't expose which specific instance gets consumed) and remember its
   quality.
2. Let the original method run (crafts at quality 1, as normal).
3. **Postfix** — find the newly crafted `FistGold_Grapple` and set its
   quality to match what was captured.

## Damage balance

`GrappleAttackPatch.cs` adds a flat `+40` base pierce damage
(`SharedData.m_damages.m_pierce`, not `m_damagesPerLevel`, so it stays flat
across quality levels rather than scaling) to compensate for the loss of
Frostfire/Thunderblood's elemental damage. This number is a starting point
for playtesting, not a researched balance target — the enchanted variants'
real damage figures could only be corroborated via web search snippets, not
a primary source, so treat both sides of this comparison as rough.

**Status:** item clone/recipe, attack-wiring, quality transfer, and the
pierce bonus are all implemented. Untested in-game (no local Valheim
install available in this environment) — treat this as a first pass to
verify, not a finished/verified mod.

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

- `GrappleKnucklesPlugin.cs` - BepInEx plugin entrypoint; clones `FistGold` into
  `FistGold_Grapple` via Jötunn's `ItemManager`/`CustomItem`/`ItemConfig`,
  with a recipe of `FistGold` + `GrapplingHook` at the forge.
- `GrappleAttackPatch.cs` - Harmony patch on `ObjectDB.UpdateRegisters` that
  wires the clone's secondary attack to the vanilla `GrapplingHook`'s
  attack/projectile config, and adds the flat pierce damage bonus.
- `QualityTransferPatch.cs` - Harmony patch on `InventoryGui.DoCrafting`
  that carries the source `FistGold`'s quality level onto the crafted
  `FistGold_Grapple`.
- `manifest.json` - Thunderstore package manifest.
- `Libraries/` - local-only reference DLLs (gitignored, not committed).
