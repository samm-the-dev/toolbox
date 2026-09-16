# Grapple Knuckles

Valheim (1.0.7, post-Deep North) BepInEx + HarmonyX + Jötunn (JVL) mod.

Crafted by combining **Nord Knucklechains** (`FistGold`) + the vanilla
**Grappling Hook** (`GrapplingHook`, added in the Deep North update) at the
**Black Forge** (`blackforge`), into a new item, **Grapple Knuckles**
(`FistGold_Grapple`), whose secondary attack launches the grapple hook
instead of Knucklechains' normal special move.

Grapple Knuckles is meant as an **alternative to enchanting** Knucklechains
into Frostfire (`FistGold_FrostFire`) or Thunderblood
(`FistGold_BloodLightning`), not a further upgrade of them — the recipe
only accepts plain `FistGold`. You trade the elemental proc for the grapple
utility, a pierce damage bonus on the grapple hit itself, a halved reload
time, and a movement speed bonus, all deliberately tuned for fun over strict
balance (see `GrappleAttackPatch.cs`). If you craft from an upgraded
(higher-quality) Knucklechains, that quality level carries over to the
Grapple Knuckles (see `QualityTransferPatch.cs`) — vanilla has no built-in
mechanism for this since it's a different item, so that's a from-scratch
Harmony patch on the crafting flow.

## How the secondary attack override works

Rather than Harmony-patching `Humanoid.StartAttack` and reimplementing the
hook's raycast/pull/rope physics, `GrappleAttackPatch.cs` patches
`ObjectDB.UpdateRegisters` (the same extension point other Valheim mods use
to tweak item stats after load) and copies the real `GrapplingHook` item's
own `m_secondaryAttack` config (stamina cost and reload time) onto the
clone's secondary attack slot, pointed at a **cloned** grapple projectile
(see below). Since `m_attackProjectile` is what actually drives the vanilla
`GrapplingPoint` component (rope `LineRenderer`, pull-toward-anchor logic),
this reuses vanilla's launch mechanic and VFX wholesale — no physics
reimplementation. The secondary attack also reuses the item's own
primary/light attack animation trigger (`Attack.m_attackAnimation`,
confirmed field), so it plays as "punch, then a hook flies out" rather than
a crossbow-draw animation on bare fists.

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

## Damage / feel tuning

All in `GrappleAttackPatch.cs`, all deliberately tuned for fun over strict
balance, per explicit request:

- **+40 pierce damage on the grapple projectile only** (`Projectile.m_damage.m_pierce`
  on the cloned projectile, not the item's melee `m_damages`, so the fists'
  regular punches are unaffected). Compensates for forgoing
  Frostfire/Thunderblood's elemental damage. This number is a starting
  point for playtesting, not a researched balance target — the enchanted
  variants' real damage figures could only be corroborated via web search
  snippets, not primary source.
- **Reload time halved** vs. the vanilla hook (`ReloadTimeMultiplier = 0.5f`).
- **+10% movement speed** while equipped (`SharedData.m_movementModifier`,
  confirmed field — a flat additive fraction summed across all equipped
  items, same mechanism as Wolf/Troll armor's speed penalty but positive
  here).

**Status:** item clone/recipe, attack-wiring (including the cloned
projectile, animation reuse, and Black Forge station), quality transfer,
and all the damage/feel tuning above are implemented. Untested in-game (no
local Valheim install available in this environment) — treat this as a
first pass to verify, not a finished/verified mod.

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
  with a recipe of `FistGold` + `GrapplingHook` at the Black Forge, and
  clones `Projectile_GrapplingHook` via `PrefabManager.CreateClonedPrefab`.
- `GrappleAttackPatch.cs` - Harmony patch on `ObjectDB.UpdateRegisters` that
  wires the clone's secondary attack to the cloned grapple projectile,
  reuses the item's own attack animation, and applies the pierce/reload/
  movement-speed tuning.
- `QualityTransferPatch.cs` - Harmony patch on `InventoryGui.DoCrafting`
  that carries the source `FistGold`'s quality level onto the crafted
  `FistGold_Grapple`.
- `manifest.json` - Thunderstore package manifest.
- `Libraries/` - local-only reference DLLs (gitignored, not committed).
