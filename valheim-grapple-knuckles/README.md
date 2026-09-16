# Grapple Knuckles

Valheim (1.0.7, post-Deep North) BepInEx + HarmonyX + Jötunn (JVL) mod.

Clones **Nord Knucklechains** (`FistGold`) into a new item, **Grapple
Knuckles** (`FistGold_Grapple`), and replaces its secondary attack with a
launch of the vanilla Grappling Hook.

**Status: work in progress.** The item clone/recipe side is implemented.
The Harmony patch that redirects the secondary attack to the grapple
mechanic is a stub (`GrappleAttackPatch.cs`) pending confirmation of the
vanilla hook's internal implementation — see that file for what's needed.

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

5. **Inspect the decompiled game code** with
   [ILSpy](https://github.com/icsharpcode/ILSpy) or dnSpy against
   `assembly_valheim.dll` to confirm the Grappling Hook's actual prefab
   name and launch method before finishing `GrappleAttackPatch.cs` (see the
   TODO block in that file). This step has to happen on your machine
   against your own game files.

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
- `GrappleAttackPatch.cs` - Harmony patch for the secondary-attack override
  (stub, see file).
- `manifest.json` - Thunderstore package manifest.
- `Libraries/` - local-only reference DLLs (gitignored, not committed).
