# Grapple Knuckles

Valheim (1.0.7, post-Deep North) BepInEx + HarmonyX + Jötunn (JVL) mod.

Clones **Nord Knucklechains** (`FistGold`) into a new item, **Grapple
Knuckles** (`FistGold_Grapple`), and replaces its secondary attack with a
launch of the vanilla Grappling Hook (`GrapplingHook`, added in the Deep
North update).

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

**Status:** item clone/recipe and the attack-wiring patch are implemented.
Untested in-game (no local Valheim install available in this environment) —
treat this as a first pass to verify, not a finished/verified mod.

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
   folder is gitignored â€” never commit them):
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
  `FistGold_Grapple` via Jötunn's `ItemManager`/`CustomItem`/`ItemConfig`.
- `GrappleAttackPatch.cs` - Harmony patch on `ObjectDB.UpdateRegisters` that
  wires the clone's secondary attack to the vanilla `GrapplingHook`'s
  attack/projectile config.
- `manifest.json` - Thunderstore package manifest.
- `Libraries/` - local-only reference DLLs (gitignored, not committed).
