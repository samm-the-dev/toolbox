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

## STATUS (2026-09-17): CONFIRMED WORKING END-TO-END

User: "Working perfectly now." Grapple Knuckles is fully functional and
polished - core grapple, cooldown (including the sprint/jump-exploit and
requeue fixes), chain texture (locked to `Shield_Flametal_mat`), and sound
all confirmed in-game. The cleanup checklist below has been completed -
`GrappleDebugPatch.cs` deleted, `HARMONY_DEBUG`/diagnostic-wrapping removed
from `Awake()`, `_harmony` restored to a field initializer. Everything else
in this file is the historical debugging log that got the mod here - still
useful for context on *why* things are built the way they are, but no
longer describes outstanding work for Grapple Knuckles itself. Remaining
open items are about re-enabling the OTHER items (see "Current test-pass
scope" below), not this one.

## SESSION HANDOFF (2026-09-17, end of this session)

Read this section first if picking this up fresh. Long live-testing session,
deep in Grapple Knuckles' secondary attack specifically. Summary of what's
confirmed, what's fixed, and what's still open, newest/most important first.

**CONFIRMED WORKING IN-GAME**: core grapple (animation, projectile launch,
actual pull), pierce-bonus removal, right-hand rope anchor, projectile
damage scaled to 1/10th of weapon damage (`Projectile_ScaleGrappleDamage_Patch`),
cooldown bar (shows correctly after the `Humanoid.StartAttack` patch move),
chain texture recolor (shows correctly after the `GetTexturePropertyNames()`
fix) - user's pick so far: `Shield_Flametal_mat` ("flametal shield looks best
out of those options").

**Third polish round (2026-09-17, latest)**, user feedback: cooldown bar
works but doesn't quite match the vanilla hook - specifically the
`"reload_crossbow"` pose looks wrong on a fist weapon, and reload sound
works (rides along with that animator state) but the grapple LAUNCH has no
sound. Changes made, NOT YET RE-TESTED:
- `GrappleCooldownPatch.QueueCooldown` now uses `m_animation = "equipping"`
  instead of `"reload_crossbow"` (with no `m_doneAnimation`, matching how
  real `QueueEquipAction`/`QueueUnequipAction` build their own
  `MinorActionData`). It's a bool, not a one-shot trigger, so it holds for
  the full cooldown duration same as before. **Known trade-off, not yet
  confirmed either way**: the reload SOUND that was working was very likely
  riding along with the `"reload_crossbow"` animator state as a baked-in
  Animation Event on that specific clip (same mechanism as the missing
  launch sound, see below) - swapping the animation bool probably swaps
  that sound out too, for whatever `"equipping"` itself carries (if
  anything). Flag this to the user on the next test rather than assuming
  it's still fine.
- Added two TEMPORARY diagnostic dumps (`GrappleKnucklesPlugin.cs`,
  `LogChainTextureCandidates()`/`LogGrappleSoundCandidates()`, both called
  once from `CloneKnucklechains()`) since the AssetRipper export from
  earlier in the session couldn't be relocated on disk this round (searched
  Desktop/Downloads/Documents/temp, not found - may have been in a
  since-cleaned temp dir). Rather than guess more candidate names blindly:
    - `LogChainTextureCandidates()` dumps every real, currently-loaded
      `Material` whose name contains a weapon/armor/shield-ish keyword -
      per explicit direction ("think we need to just look at weapon/armor/
      shield textures, might try a new list"), to build the next
      `ChainTexture` dropdown revision from real verified names.
    - `LogGrappleSoundCandidates()` dumps every currently-loaded
      `AudioSource` whose GameObject name contains a bow/crossbow/hook/
      release/fire/shoot/throw-ish keyword, hunting for a real, reusable
      launch-sound source. Required adding a `UnityEngine.AudioModule`
      reference to `GrappleKnuckles.csproj` (copied from the user's own
      `valheim_Data/Managed/`) - `AudioSource` isn't in `CoreModule`.
  Both are genuinely exploratory - there's no guarantee `AudioSource`
  components (vs. some other Valheim-specific sound wrapper) are how these
  sounds are actually implemented; read next test's log output before
  building anything on top of what they find.
- **Working theory on the missing launch sound** (not yet confirmed):
  confirmed earlier this session that the real vanilla hook's Attack-level
  EffectLists (`m_triggerEffect` etc.) are ALL empty, and separately
  confirmed (see "grapple secondary attack" section below) that Valheim's
  actual attack-trigger mechanism is a Unity Animation Event baked into the
  specific animation CLIP, not attack-level data. The working reload sound
  is the strongest evidence yet for this: it appeared automatically just
  from reusing the real `"reload_crossbow"` animator bool, with zero sound-
  specific code on our end. By the same logic, the launch "whoosh" is most
  likely baked into the real crossbow-FIRE clip specifically - which we
  never play, since Grapple Knuckles reuses the punch animation instead.
  If true, there's no simple field to copy for this (unlike the reload);
  it would need either accepting the punch's own sound, or manually
  triggering a real sound effect via code once a real, reusable AudioClip/
  prefab reference is found (this is what the new diagnostic dump is for).

**Fourth polish round (2026-09-17, latest)**: user confirmed the `"equipping"`
reload animation "looks great," locked in (no further animation changes
planned). User then corrected the cooldown TIMING model with real vanilla
knowledge ("the vanilla hook reload doesn't start until after the grapple is
complete, and resets to 0 on sprint") - traced via decompile, NOT YET
RE-TESTED:
- **Confirmed, fixed**: real vanilla holds off queueing the reload for the
  entire active-pull duration, not from the moment of throwing. Decompiled
  `GrapplingPoint.cs` directly: `Activate()` sets `Player.m_localPlayer.m_grappling = 1f`
  on hit, `Update()` refreshes it to `0.2f` every frame while the pull is
  active, `Break(bool early)` (fires on BOTH early-break and normal
  completion) forces it to exactly `0f`. Separately, `Player.QueueReloadAction()`
  explicitly guards `!(m_grappling > 0f)` before queueing. So reload is held
  off for the pull's whole duration and becomes eligible the instant `Break()`
  runs. **Fix**: `GrappleCooldownPatch` no longer queues on
  `Humanoid.StartAttack` (throw time) - it now queues on `GrapplingPoint.Break`
  (pull completion), via a manual postfix reading the private `m_character`
  field through Harmony's `___m_character` injection convention, scoped to
  `__instance.name.StartsWith(ClonedGrapplingPointPrefabName)` and
  `Player.m_localPlayer`.
  **Known gap, deliberately not handled this round**: a THROW THAT MISSES
  (projectile despawns without ever hitting anything, so `GrapplingPoint`
  never spawns/activates/breaks) has no cooldown trigger at all right now -
  real vanilla gets this for free because `m_weaponLoaded` resets on
  attack-trigger regardless of hit/miss, independent of `m_grappling`; our
  cooldown isn't wired through that generic system. Would need to detect "our
  cloned projectile despawned without hitting anything" (e.g. patch its
  destroy/TTL path) - flagged for later, not yet requested.
- **CONFIRMED, no code change needed**: "resets to 0 on sprint," and it
  applies to the post-pull reload countdown specifically (confirmed via
  user: "it doesn't start until after the pull"). First two search passes
  missed it by stopping a few lines short: `Player.CheckRun()` (the real
  sprint gate) doesn't just check stamina/`IsDrawingBow()`/`IsBlocking()` -
  reading a few lines further, on the success path (has stamina, actually
  moving) it unconditionally calls `ClearActionQueue()` every single frame
  the player is sprinting, right before returning `true`. Separately,
  `Player.OnJump()` does the exact same thing - jumping ALSO wipes the
  entire action queue, a bonus fact the user hadn't mentioned. Since
  `GrappleCooldownPatch` manipulates the real `Player.m_actionQueue` list
  via reflection (not a private copy), vanilla's own `ClearActionQueue()`
  calls already apply to our queued Reload entry exactly like they would to
  a real reload - **this already works with zero code changes**, purely as
  an emergent property of reusing the real queue instead of building a
  separate cooldown system. Nothing to implement; just confirm in the next
  test that sprinting/jumping during the post-grapple cooldown correctly
  cancels the bar (and note that canceling removes the entry entirely - the
  player is NOT immediately re-locked-out afterward, matching real vanilla,
  since there's no re-queue on cancel anywhere in this code path).
- **Also fixed, same round**: `EquipItem_QueueInitialLoad` (the "load on
  equip" trigger) converted from `[HarmonyPatch]` + `PatchAll()` to a manual
  `harmony.Patch()` call, same as `GrapplingPoint.Break` above. Evidence:
  an entire test pass of repeated equip/unequip cycling never logged even a
  single `QueueCooldown called` line for this trigger (not even a "already
  queued, skipping" dupe-guard hit) - the same silent `PatchAll()`
  non-application already known for `Attack.Start`/`Humanoid.StartAttack`,
  apparently not unique to those two methods. Going forward: default to
  manually patching anything touching `Humanoid`/`Attack`/`GrapplingPoint`
  in this mod rather than assuming `[HarmonyPatch]` + `PatchAll()` works,
  since the gap has now shown up on three unrelated methods with no root
  cause ever found.

**Sixth polish round (2026-09-17, latest)**, NOT YET RE-TESTED:
- **Texture locked in**: `Shield_Flametal_mat` hardcoded as
  `ChainTextureMaterialName`, dropdown config and both candidate-dump
  diagnostics removed (served their purpose).
- **Sprint/jump reload exploit, round 1**: real vanilla auto-requeues a
  cleared reload every frame via `Player.UpdateWeaponLoading()` (reads the
  PRIMARY attack's `m_requiresReload`, always false for our punch, so it
  never did this for us). Added `_grappleLoaded`/`_grappleInFlight` state
  plus a postfix on `UpdateWeaponLoading` that requeues for our item
  specifically, mirroring vanilla.
- **Sprint/jump reload exploit, round 2 (the real fix)**: user found that
  sprinting CONTINUOUSLY still let attacks slip through despite round 1 -
  "if I sprint and keep it at zero, I can trigger the launch without
  waiting for the reload at all." Root cause: our ONLY block was
  `Humanoid.InMinorAction()`, which is purely animator-state-TAG based, not
  tied to the C# queue directly. `Player.CheckRun()`'s `ClearActionQueue()`
  (confirmed via decompile) only clears the list - it does NOT touch the
  animator bool; that only happens inside `UpdateActionQueue()`'s "queue
  now empty" branch. Sprinting continuously creates a race every frame:
  `CheckRun()` clears the queue -> `UpdateActionQueue()` sees it empty and
  sets the animator bool false -> `InMinorAction()` is briefly, genuinely
  false -> `StartAttack()` slips through in that window, even though
  `UpdateWeaponLoading_RequeueIfNeeded` re-adds a fresh entry moments later
  (too late). Real vanilla doesn't have this hole because `Attack.Start()`
  has a SECOND, non-animator gate for real reload weapons:
  `if (m_requiresReload && !IsWeaponLoaded()) return false;` - pure C#
  state, immune to animator/queue timing. Added the equivalent ourselves:
  `StartAttack_BlockIfNotLoaded`, a Prefix on `Humanoid.StartAttack` that
  checks `_grappleLoaded` directly and blocks (skips the original method,
  `__result = false`) before `Attack.Start`/`ClearActionQueue` ever run,
  regardless of what the animator happens to be doing that frame. This is
  the robust, timing-independent gate; `InMinorAction()` (via the queued
  MinorActionData) remains as the visual/HUD-bar layer on top, not the
  actual enforcement anymore.

**Fifth polish round (2026-09-17)**: the
`LogChainTextureCandidates()`/`LogGrappleSoundCandidates()` diagnostics
added last round paid off - both produced real, verified data on the very
next test:
- **ChainTexture dropdown rebuilt from real data**: the diagnostic dumped
  297 real, currently-loaded materials matching weapon/armor/shield-ish
  keywords. Replaced the old ore-bar-heavy shortlist with a curated list of
  real weapon/armor/shield surface materials: `Shield_Flametal_mat`
  (new default - user's confirmed favorite), `FlametalArmor_Mat`,
  `Flametal_Mat`, `nordfistweapon_frostflame_mat`/`_thunderblood_mat` (real
  alternate skins for THIS SAME FistGold model, not just a same-shader
  guess), `BlackMetalChest_mat`, `BlackMetalRoundShields_mat`,
  `blackmetalsword`, `IronTowerShield_mat`, `SilverShield_Mat`,
  `SilverHammer_mat`, `Dyrnwyn_mat`, `Charred_dyrnwyn_mat`,
  `CrystalAxe_mat`, `battleaxe_mistlands_mat`, `Jotnarmor_mat`,
  `DN_armor_heavy_mat`, `WolfCapeChain`.
- **Launch sound implemented**: the sound diagnostic found the real,
  standalone sound-effect prefabs Valheim uses for the hook:
  `sfx_grapplinghook_fire`, `_hit`, `_pull`, `_reload`, `_detach`,
  `_repel` (all found via `AudioSource` GameObject name matching, though
  their `.clip` read back null via plain `AudioSource` reflection - Valheim
  wraps them in a custom `ZSFX` component instead, confirmed via decompile:
  `ZSFX.m_playOnAwake = true` by default, matching how
  `GrapplingPoint.m_pullSound` is already used elsewhere -
  `Object.Instantiate(prefab, transform)` with no explicit `Play()` call).
  New `GrappleFireSoundPatch` (`GrappleAttackPatch.cs`, manually patched on
  `Attack.Start` like the cooldown/damage patches) looks up
  `sfx_grapplinghook_fire` once via `Resources.FindObjectsOfTypeAll<GameObject>()`
  and instantiates it at the character's position when the grapple fires.
  Sidesteps the earlier "sound is baked into the crossbow_fire animation
  clip we don't play" theory entirely - uses the real standalone sound
  prefab directly instead of trying to extract/replicate a clip-embedded
  Animation Event.

**FIXED, NOT YET RE-TESTED (this session's last round)**:
- **Cooldown bar never showed, root cause found via decompile**:
  `Humanoid.StartAttack()`'s own body is
  `if (attack.Start(...)) { ClearActionQueue(); ...; return true; }` -
  `ClearActionQueue()` unconditionally wipes `Player.m_actionQueue`
  immediately after EVERY successful attack start, any weapon. Our cooldown
  patch was a postfix on `Attack.Start`, which runs BEFORE that line (it's
  still inside the `if` condition), so our queued Reload entry was added
  then wiped before the next frame's HUD read ever saw it - explains why
  `GrappleCooldownPatch`'s own diagnostic logging always showed
  `queue count=0` right before every `Queued ...` line, and why
  `GrappleDebugPatch.HudUpdateActionProgress_Debug` never once logged a
  Reload entry for `$item_fistgold_grapple` despite the queue add
  "succeeding" every time. The real vanilla hook's reload survives because
  it's queued from `UpdateWeaponLoading()` on a later frame, outside that
  call stack. **Fix**: moved `GrappleCooldownPatch`'s manual patch from
  `Attack.Start` to `Humanoid.StartAttack` (postfix runs after the WHOLE
  method body, including `ClearActionQueue()`); identifies "this was our
  grapple" via the equipped weapon's prefab name + `secondaryAttack` flag
  instead of the (now out-of-scope) `Attack` instance's projectile
  reference.
- **Chain texture always rendered flat white, root cause found via
  decompile**: `Material.mainTexture` only resolves through a shader's
  `[MainTexture]`-flagged property (an SRP/URP-era Unity feature) or falls
  back to a literal `"_MainTex"` property; if a shader has neither, it
  silently returns null. We were hardcoding `SetTexture("_MainTex", ...)`
  from `sourceMaterial.mainTexture` - if that resolved null for a given
  material's shader, the clone ended up with no diffuse texture at all
  while `_Color` (confirmed to exist on all these materials) got forced to
  the source's tint, usually pure white. This explains why EVERY material
  choice in the dropdown rendered white, not just Flametal specifically.
  **Fix**: `ApplyChainTexture()` now enumerates the source material's real
  texture properties via `Material.GetTexturePropertyNames()` and copies
  each one (texture + offset + scale) onto the clone by name, instead of
  assuming `"_MainTex"` is correct - also logs the source shader name and
  its texture property list so a future failure is diagnosable from one log
  line instead of another blind round-trip.

**Still open**: sound effects (user asked to investigate; inconclusive so
far - see "sound effects investigation" note further down). Sprint-blocking
during the cooldown (reasoned to work for free via `InMinorAction()`, never
explicitly confirmed in-game).

**Symptom history, so the shape of the bug is clear**: (1) originally
nothing happened at all → fixed by correcting `m_attackType` to `Projectile`
and animation handling → (2) kick animation played, projectile spawned but
"weakly launching in a small arc, not actually grappling" → fixed by
`Attack.Clone()`-ing the hook's full config (real `m_projectileVel: 40`
etc.) instead of hand-picking fields, and by ALSO cloning `GrapplingPoint`
(a separate prefab the projectile spawns on hit, which does the actual
pull - the flying projectile alone doesn't grapple anything) → (3) that
regressed to "nothing happens at all again, __result=true but silent" →
fixed by the `m_attackChainLevels`/`m_attackRandomAnimations` copy (real
primary attack is a 2-level combo, so `Attack.Start()` needs the
chain-level-suffixed trigger name, not the bare one) → (4) grapple fully
working; cooldown bar and chain texture both silently no-op'd, fixed above.

**A confirmed-working temporary diagnostic tool is still in the codebase**:
`GrappleDebugPatch.cs` (logs `Humanoid.StartAttack`/`Attack.Start` calls
live) plus a manual isolated `_harmony.Patch()` call and `HARMONY_DEBUG`
env var wiring in `GrappleKnucklesPlugin.Awake()`. **All of this is
TEMPORARY and should be deleted once the grapple is confirmed working** -
see the dedicated section below for exactly what to remove.

**Unresolved side-mystery (does NOT block real functionality, don't chase
it further unless curious)**: `_harmony.PatchAll()` (the normal
attribute-based `[HarmonyPatch]` discovery) mysteriously never applies ANY
postfix to `Humanoid.StartAttack` or `Attack.Start` specifically - true for
both `GrappleDebugPatch.StartAttack_Debug`/`AttackStart_Debug` AND the
pre-existing `PrismBladePatches.StartAttack_CycleElement`, even though
`PatchAll()` reports success (no exception) and correctly applies 3 other
postfixes to `ObjectDB.UpdateRegisters` in the same call. A **manual,
isolated `_harmony.Patch(method, postfix: ...)` call on the exact same
`Attack.Start` method succeeds immediately** and its postfix reliably
fires during real gameplay (confirmed live, `__result=True`). So Harmony
CAN patch these methods - something specific about `PatchAll()`'s
auto-discovery path skips them silently for reasons never root-caused.
Doesn't matter for the mod's real functionality (the actual grapple
mechanic never depended on patching these two methods - only the debug
tooling did), but flagging in case it recurs for a real future patch on
either method: use a manual `_harmony.Patch(...)` call as a working
fallback if `[HarmonyPatch]` attribute discovery silently fails again.

**Toolchain notes for whoever picks this up**: `.NET 8 SDK` and `ilspycmd`
(dotnet tool) are installed on this machine specifically for this project.
`GrappleKnuckles/Libraries/` is populated with real DLLs copied from the
user's own game install and r2modman profile (BepInExPack 5.4.2350, Jötunn
2.30.0) - the project builds clean (`dotnet build -c Release`). A dedicated
r2modman profile, `GrappleKnucklesDev`, exists with just BepInExPack +
Jötunn + this mod + the user's 15 QoL mods, isolated from their main
`Default` profile. `deploy-local.ps1` at the repo root rebuilds and
redeploys to that profile in one step (or just `dotnet build` +
`cp bin/Release/GrappleKnuckles.dll` to
`%APPDATA%\r2modmanPlus-local\Valheim\profiles\GrappleKnucklesDev\BepInEx\plugins\GrappleKnuckles\`
- **check the `valheim` process isn't running first, it locks the DLL**).
BepInEx's console window is enabled for this profile
(`Logging.Console.Enabled = true` in that profile's `BepInEx.cfg`) - more
reliable for live debugging than the disk log, which has repeatedly (if
inconsistently) lagged behind real-time in this session; when in doubt,
ask for a relaunch and check `BepInEx/LogOutput.log` in that profile - our
own `Jotunn.Logger` calls always eventually show up there once the process
exits cleanly, even when the console window itself doesn't cooperate.

### Post-fix polish round (2026-09-17, same session, after "working now!")

Four changes, all in response to live-testing the now-working grapple:

1. **Pierce damage bonus removed** (`GrappleAttackPatch.cs`) - per explicit
   direction, the `+40 m_pierce` on the grapple projectile is gone. The
   `ProjectilePierceDamageBonus` constant and its log-message reference were
   removed too, not just the application.
2. **Rope anchors to the right hand, not vanilla's hardcoded left hand**
   (new `GrapplingPoint_RightHandAnchor_Patch` in `GrappleAttackPatch.cs`).
   `GrapplingPoint.Activate()` (confirmed via decompile) always sets
   `m_attachPoint = visEquipment.m_leftHand` - fine for the real hook
   (never dual-wielded), wrong for a fist weapon. Postfix re-points
   `m_attachPoint` to `visEquipment.m_rightHand` instead, scoped to only
   our own cloned `GrapplingPoint` instances (matched by name prefix, since
   `Instantiate()` appends `"(Clone)"`) - never touches real vanilla hook
   throws from other players. Checked the real `ChitinHarpoon`/
   `SE_Harpooned` (a different rope-visual weapon) for a cleaner pattern
   first - it has no hand-anchor concept at all (pulls a target via a
   status effect, structurally different), so this was the only real
   option.
3. **Real cooldown, both post-fire and on-equip**
   (`GrappleCooldownPatch.cs`, new file). Extensively researched why
   vanilla has nothing usable here (see "why no vanilla reload mechanism
   works" below) - ended up reusing `Player.m_actionQueue`/
   `Player.MinorActionData` directly via reflection, the same general
   system real crossbow reloads/equipping/unequipping use.
   `AttackStart_QueueCooldown` (manually patched onto `Attack.Start`, same
   reason as the debug probe - see below) queues a fake
   `ActionType.Reload` action after a successful grapple fire, identified
   by the fired `Attack`'s `m_attackProjectile` matching our cloned
   projectile (not by weapon name, since `Humanoid.StartAttack` clones the
   `Attack` fresh every time - confirmed via decompile, so the instance is
   never reference-equal to `SharedData.m_secondaryAttack`).
   `EquipItem_QueueInitialLoad` (a normal `[HarmonyPatch]` on
   `Humanoid.EquipItem` - not manually patched, no evidence this one has
   the `Attack.Start`/`Humanoid.StartAttack` PatchAll() issue) does the
   same on equip, matching the real hook's "must load before first use."
   Per explicit direction, `m_animation`/`m_doneAnimation` reuse the REAL
   `"reload_crossbow"`/`"reload_crossbow_done"` bool names (not a made-up
   name) specifically so this rides the same animator-tag-driven
   `Humanoid.InMinorAction()` state real reload uses - confirmed this is
   what blocks starting a new attack, and (needs live confirmation) is
   very likely also what blocks sprinting during a real crossbow reload,
   since it's the same underlying mechanism. Accepted trade-off: the
   character briefly shows the real crossbow-reload pose during the
   cooldown, since fists have no dedicated "reload" animation of their own
   to reuse instead. `Player.GetActionProgress()` (what the vanilla HUD
   progress bar already reads) is generic to whatever's at the front of
   the queue regardless of `ActionType`, so the progress bar should just
   work without any UI code of our own.

   **UPDATE, same session**: cooldown duration is now a live BepInEx config
   entry (`GrappleKnucklesPlugin.CooldownDuration`, section "Grapple"),
   default `2f` matching the real vanilla hook's own `m_reloadTime`
   (restoring the "normal" reload time per explicit direction, replacing
   the earlier arbitrary `3f`). `GrappleCooldownPatch.QueueCooldown` reads
   `.Value` fresh each call, not cached, so editing it live via
   Configuration Manager (already in this test profile) should take effect
   immediately, no relaunch needed.

   **Still unresolved, live-tested and confirmed NOT showing the progress
   bar**: queued successfully (confirmed via log - `QueueCooldown` always
   found the queue empty right before adding, consistent with each
   previous entry expiring normally before the next), but the user never
   saw a visible cooldown bar. Added a manually-patched diagnostic
   (`GrappleDebugPatch.HudUpdateActionProgress_Debug`, patched onto the
   private `Hud.UpdateActionProgress`) that logs exactly what the HUD
   itself reads (`text`/`progress`/`data.m_duration`/`data.m_time`) - not
   yet re-tested with this diagnostic in place. `Hud.UpdateActionProgress`
   (confirmed via decompile) only shows the bar when
   `!string.IsNullOrEmpty(text) && data.m_duration > 0.5f` - our values
   should clear that bar easily, so if the diagnostic shows sane values and
   the bar still doesn't render, the issue is somewhere in
   `m_actionBarRoot`/`m_actionProgress`'s own UI activation, not our data.

   **Why no vanilla reload mechanism works for a melee-type secondary
   attack** (worth keeping so this doesn't get re-investigated from
   scratch): `Attack.m_reloadTime` is only ever *read* inside
   `Player.QueueReloadAction()`, which only runs when the weapon's
   **primary** attack has `m_requiresReload = true` - for
   `ReloadTimeMultiplier`-style tuning on the secondary attack, this field
   is completely inert. Setting `m_requiresReload = true` directly on the
   secondary attack doesn't work either:
   `Player.UpdateWeaponLoading()` (confirmed via decompile) only ever
   checks the **primary** attack's flag to decide whether to call
   `SetWeaponLoaded()` - since Knucklechains' primary is a plain punch
   (`m_requiresReload = false`), this forces `m_weaponLoaded = null` every
   single fixed-update frame regardless of the secondary attack's own
   flag. `Attack.Start()`'s own gate
   (`if (m_requiresReload && !IsWeaponLoaded())`) would then permanently
   block the attack from ever starting again - not add a cooldown, just
   break it outright. `m_blockReloadTime`/`Player.m_blockReload` doesn't
   help either - it only gates whether a *new reload action* can be
   queued, and reload actions only exist for primary-attack-reload
   weapons; for a punch-primary weapon it's simply inert. Hence the
   `MinorActionData` reuse above instead.
4. **Flametal chain recolor** (`TryApplyFlametalChainTexture` in
   `GrappleKnucklesPlugin.cs`), same `MaterialPropertyBlock` technique
   already used for Shield of Frost's silver tint. Confirmed via the real
   game files: FistGold's chain mesh uses a single material,
   `nordfistweapon_mat` (Valheim's own custom weapon shader), and the real
   Flametal item's own material, `flametal`, uses Unity's Standard shader
   instead - different shaders, so this overrides only the
   `_MainTex`/`_Color` properties (present on both) via
   `Resources.FindObjectsOfTypeAll<Material>()` to find the real, already-
   loaded `flametal` material at runtime, rather than swapping the whole
   material (which would also swap the shader and lose whatever weather/
   snow-cover integration the custom shader has that Standard doesn't).

### Second polish round (2026-09-17, live-tested the first round)

- **Chain material was the wrong Flametal, caught live in testing**: the
  material literally named `"flametal"` turned out to be the pre-Ashlands
  legacy "Ancient Metal" look (`_Color: {r: 1, g: 0.54, b: 0.37, a: 1}` -
  orange, confirmed via the real .mat file) - the same old/legacy-vs-current
  split as the `Flametal`/`FlametalNew` item prefabs, just on the material
  side this time. The real current Flametal materials
  (`FlametalArmor_Mat`, `Shield_Flametal_mat`, the world ore's own
  `Flametal_Mat`) are all pure white `_Color` (1,1,1,1) - the real gray/
  metallic look comes entirely from their `_MainTex`, not a tint. Now
  defaults to `FlametalArmor_Mat` (confirmed same custom shader as
  `nordfistweapon_mat`).
- **Chain texture is now a live-editable dropdown config**
  (`GrappleKnucklesPlugin.ChainTexture`, section "Grapple (dev tool)",
  `AcceptableValueList<string>` of a curated real-material shortlist -
  Configuration Manager renders this as an actual dropdown). Re-applies via
  `ChainTexture.SettingChanged` whenever changed, no relaunch needed. This
  is explicitly a TEMPORARY dev/tuning tool per direction, not meant to
  ship as a real setting - remember to remove or hide it once a final
  texture choice is made. Curated shortlist (excludes anything
  Bloodgold-toned, since the base FistGold item already looks like that):
  `FlametalArmor_Mat`, `Shield_Flametal_mat`, `Flametal_Mat`,
  `WolfCapeChain` (a literal chain material, worth trying first),
  `bronze`, `iron`, `copper`, `tin`, `blackmetal`, `barMat`, `silverbar`,
  `tinbar`, `meteorite`.
- **Grapple projectile damage explained, not a bug**: user observed ~40
  damage on the grapple hit, expected it to match the vanilla hook.
  Confirmed via decompile: `Attack.FireProjectileBurst()` computes the
  fired projectile's `HitData` from `m_weapon.GetDamage()` - the WIELDER's
  own weapon damage, not the projectile prefab's own `m_damage` field
  (which is `0` on both the real hook and our clone). The real vanilla
  `GrapplingHook` item's own base damage is `m_pierce: 10` (everything else
  `0`) - a near-harmless utility tool. Grapple Knuckles' base damage is our
  tuned ~95 blunt (the Ashlands-tier fist damage from the earlier data-
  extraction pass), so the grapple throw naturally deals real fist-weapon
  damage (~40 observed is plausibly that base after armor mitigation), not
  the ~10 a vanilla hook throw would. **Fixed, per explicit direction**:
  `GrappleAttackPatch.Projectile_ScaleGrappleDamage_Patch` (manually
  patched onto `Projectile.Setup`, same PatchAll()-reliability reasoning as
  `GrappleCooldownPatch` - not yet independently verified whether
  `Projectile.Setup` specifically needs this workaround, used it
  defensively) scales all ten `HitData.DamageTypes` fields by `0.1x`
  whenever the wielded weapon is Grapple Knuckles, leaving the melee
  punch's own damage untouched (only the fired projectile's `HitData` is
  touched, before `Setup` stores the reference on the projectile). NOT YET
  RE-TESTED IN-GAME.
- **Sound effects - not yet investigated this round**, still open.
- **Cooldown bar still not confirmed showing** - live-tested, found the
  queue mechanics themselves working correctly (well-formed `Equip`/
  `Unequip`/`Reload` entries flowing through with sane durations), but
  every `Reload`-type entry actually captured by the HUD diagnostic that
  session was for `$item_graplinghook` (the REAL vanilla hook, which the
  user was also testing/comparing against), never
  `$item_fistgold_grapple` - inconclusive on whether ours ever got shown,
  since the diagnostic at the time only logged on queue-COUNT change,
  which silently skips a same-count transition (e.g. vanilla hook's own
  reload entry directly replaced by ours, both count=1). Fixed the
  diagnostic to key on `(count, text, type)` instead - not yet re-tested
  with this fix.

### Cleanup checklist once the grapple is confirmed working

All temporary, added purely to chase the secondary-attack bug this session:

- Delete `GrappleDebugPatch.cs` entirely.
- In `GrappleKnucklesPlugin.cs`'s `Awake()`: remove the `HARMONY_DEBUG` env
  var line, the try/catch around `_harmony.PatchAll()` (restore it to a
  plain unwrapped call), the `GetPatchInfo`/owner-logging block, and the
  manual isolated `_harmony.Patch(...)` call that targets
  `GrappleDebugPatch.ManualProbePostfix`. Restore `_harmony` to a
  `private readonly` field initializer
  (`private readonly Harmony _harmony = new Harmony(ModGUID);`) instead of
  being assigned inside `Awake()`, since nothing needs it constructed early
  anymore once `HARMONY_DEBUG` isn't being set. **Keep the
  `GrappleCooldownPatch.RegisterManualPatch(_harmony)` call** - that one is
  real functionality (the cooldown system), not debug tooling, and still
  needs the manual-patch workaround since it also targets `Attack.Start`.
- In `GrappleAttackPatch.cs`'s final `Logger.LogInfo(...)` call: the
  `[GrappleDebug] final secondaryAttack: ...` suffix is fine to keep or trim
  - it's genuinely useful ongoing diagnostic info, not just bug-chasing
  cruft, but shorten it if it feels noisy once things are stable.
- `GrappleKnucklesDev` profile's `BepInEx.cfg` has
  `Logging.Console.Enabled = true` - fine to leave on for continued testing,
  or set back to `false` (the r2modman default) once live debugging is done.

## Current test-pass scope (2026-09-17)

**Only Grapple Knuckles is registered** - per explicit direction, testing
one item at a time rather than the whole mod at once. Everything else
(Mountain Spiritfire Axe, Fenris/Ashlands/Deep North hybrid mage armor,
Fire Dagger, Lightning Sword, Shield of Frost, Exploding Sledge, Prism
Blade) is commented out of `GrappleKnucklesPlugin.cs`'s `Awake`/`OnDestroy`
- code untouched, one line each to re-enable as each item gets verified.
`ElementalWeapons.Init()`/`ShieldOfFrost.Init()`/`ExplodingSledge.Init()`
are also commented out, since that's where those items' own `Init()`
wires their `OnItemsRegistered`/`OnVanillaPrefabsAvailable` subscriptions -
disabling `Init()` fully disables each group.

**`GrappleDebugPatch.cs` is a TEMPORARY diagnostic file**, added 2026-09-17
while chasing "no animation, no projectile on secondary input at all" after
several rounds of fixes that should have addressed a weaker symptom. Logs
`Humanoid.StartAttack` and `Attack.Start` to the live console specifically
when the current weapon is Grapple Knuckles, to see exactly where the
attack chain stops. **Delete this file once the real cause is found** -
not meant to ship in the finished mod.

## Decompile verification pass (2026-09-17)

A later session had real local access: the user's actual Valheim install
(`S:\SteamLibrary\steamapps\common\Valheim`, game version matching
`assembly_valheim.dll` shipped there) and their r2modman profile's
BepInExPack 5.4.2350 + Jotunn 2.30.0. `.NET 8 SDK` and `ilspycmd` were
installed on the desktop machine to actually build the project and
decompile the real assemblies, instead of relying on other mods'
second-hand decompile dumps or web search.

**Two real compile errors were found and fixed** (this project had never
actually been built before this pass):

1. `[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters))]` in
   three files (`GrappleAttackPatch.cs`, `ElementalWeaponAttackPatch.cs`,
   `ExplodingSledge.cs`) failed to compile with `CS0117`. This was **not**
   version drift - `ObjectDB.UpdateRegisters()` still exists, exactly as
   named. The bug is that `nameof()` can't resolve a `private` member of
   another class (C# excludes inaccessible members from `nameof` lookup
   entirely, producing "does not contain a definition" rather than an
   accessibility error), so `nameof` was simply the wrong tool for a
   private-method Harmony target. Fixed by using the string literal
   `"UpdateRegisters"` instead, which is the normal way to patch a private
   method and was already the pattern used for `"UpdateBlock"`/
   `"BlockAttack"` elsewhere in the mod.
2. `new CustomStatusEffect(effect)` in `FenrisMageArmor.cs`,
   `AshlandsHybridArmor.cs`, and `DeepNorthHybridArmor.cs` failed with
   `CS7036` - real Jötunn API drift between the manifest-pinned 2.20.1 and
   the actually-installed 2.30.0: the constructor now requires a
   `fixReference` bool. Fixed by passing `fixReference: false`, since these
   status effects are freshly created via `ScriptableObject.CreateInstance`
   with no `Mock<T>` references to resolve (the constructor's own XML doc:
   "If true references for Mock objects get resolved at runtime"). Also
   bumped `manifest.json`'s pinned dependency versions to
   `denikson-BepInExPack_Valheim-5.4.2350` / `ValheimModding-Jotunn-2.30.0`
   to match what was actually built and tested against.

**`assembly_lib.dll` does not exist in this Valheim version** - the
`Libraries\` DLL list in `GrappleKnuckles.csproj` referenced it, but the
user's actual `valheim_Data/Managed/` folder has no such file (only
`assembly_valheim.dll`, `assembly_utils.dll`, and several unrelated
`assembly_*` modules). Removed the dead reference; nothing in the mod's
code actually used types from it.

**With those three things fixed, `GrappleKnuckles.csproj` builds clean**
(`dotnet build -c Release`) against the real game/BepInEx/Jotunn
assemblies for the first time.

**Field/method assumptions decompile-confirmed correct** (see individual
file sections below for the ones that matter to each): `m_leftItem`,
`m_rightItem`, `Humanoid.UseEitr(float)`, `Character.Damage(HitData)`,
`Humanoid.UpdateBlock(float)`, `Humanoid.BlockAttack(HitData, Character)`,
`Humanoid.StartAttack(Character, bool)`, `Attack.m_attackAnimation`/
`m_attackStamina`/`m_attackEitr`/`m_reloadTime`/`m_blockReloadTime`/
`m_attackProjectile`, `ItemDrop.ItemData.SharedData.m_movementModifier`/
`m_armor`/`m_armorPerLevel`/`m_setName`/`m_setSize`/`m_setStatusEffect`/
`m_secondaryAttack`, `ItemDrop.ItemData.m_variant`,
`ItemDrop.ItemData.GetDamage(int, float)`, `HitData.DamageTypes`'s ten
damage fields, `SE_Stats.m_eitrRegenMultiplier`/`m_staminaRegenMultiplier`/
`m_dodgeStaminaUseModifier`/`m_percentigeDamageModifiers` (that's really
the field's real spelling - "percentige", not "percentage"),
`m_perfectBlockInterval` (`0.25f`), `m_timedBlockBonus`,
`PrefabManager.CreateClonedPrefab`/`OnVanillaPrefabsAvailable`, and the
`GrapplingPoint` class's existence.

**Still not verifiable from the C# assembly alone** (as of the decompile
pass) - these need either extracted asset/prefab data or an actual play
session: Fracturing's real damage type, Embla/Caller's Eitr regen source
(`m_equipStatusEffect` vs `m_setStatusEffect`), the silver-tint/bolt-scale/
rotation-trick visual results, and all multiplayer networking behavior.
Real weapon/armor tuning numbers were resolved by the extraction pass below.

## HIGH PRIORITY, FIXED: item registration used the wrong Jotunn event

First live in-game test (2026-09-17): Grapple Knuckles loaded fine (log
showed `Cloned FistGold -> FistGold_Grapple`, no errors), but never
appeared in the crafting list at an actual Black Forge, in a real loaded
world, across two separate full test sessions (confirmed via Unity's own
`Player.log` showing real world loads/saves - not a "never actually
entered a world" false alarm).

**Root cause**: every item file wired its cloning method to
`ItemManager.OnItemsRegistered`, but Jotunn's official `TestMod` reference
(`Valheim-Modding/Jotunn` repo, `TestMod/TestMod.cs`) never uses that event
for item creation - every cloned-item example there (the closest analog:
`evilSword`, cloned from `SwordBlackmetal` with `CraftingStation =
CraftingStations.Workbench`) subscribes to
`PrefabManager.OnVanillaPrefabsAvailable` instead. Decompiling
`Jotunn.Managers.ItemManager` confirmed why this matters:
`OnItemsRegistered` is invoked via a **Postfix** on `ObjectDB.Awake()`,
which runs *after* Jotunn's own item-injection Prefix
(`RegisterCustomData`, patched on the same method) already copied
whatever was in Jotunn's internal tracking dictionary into that
`ObjectDB` instance's `m_items`. An item added inside an `OnItemsRegistered`
handler is added to Jotunn's tracking dictionary too late for *that*
`Awake()` call's injection pass - it only becomes visible on some later
`Awake()`/`CopyOtherDB()` call, if one happens to fire again.
`PrefabManager.OnVanillaPrefabsAvailable` fires earlier and independently
of ObjectDB's lifecycle, so items registered there are reliably present
before any injection pass runs.

**Fixed everywhere in this mod**, 2026-09-17: `GrappleKnucklesPlugin.cs`
(`CloneKnucklechains`, plus the commented-out `MountainTierAxe.Clone`/
`FenrisMageArmor.Clone`/`AshlandsHybridArmor.Clone`/
`DeepNorthHybridArmor.Clone`/`PrismBlade.Clone` lines held back for the
current one-item-at-a-time test pass), `ElementalWeapons.cs`
(`CloneWeapons`), `ExplodingSledge.cs` (`CloneSledge`), and
`ShieldOfFrost.cs` (`CloneShield`) all now subscribe their item-cloning
method to `PrefabManager.OnVanillaPrefabsAvailable` instead of
`ItemManager.OnItemsRegistered`. This was the same architectural mistake
repeated across every single item file, not a one-off Grapple Knuckles bug
- worth double-checking any *new* item added to this mod follows the
`OnVanillaPrefabsAvailable` pattern too.

**Not yet re-tested in-game** - this fix is a strong, doubly-confirmed
theory (matches both the official reference pattern AND the decompiled
Harmony patch ordering), but confirm Grapple Knuckles actually shows up in
the crafting list next test before trusting it fully.

## Real data extraction pass (AssetRipper, 2026-09-17)

A background research pass extracted ground-truth values directly from
`resources.assets` via AssetRipper (not wiki text, not community numbers) -
see `GrappleKnuckles.csproj`'s Libraries for how to point AssetRipper at
your own install if this needs re-running for a different game version.

**Real fist weapon damage across every tier that has one** (base, quality
1): `FistBjornClaw` (early) 25 slash; `FistFenrirClaw` (Mountain/Silver) 60
slash; `FistBjornUndeadClaw` (Plains/BlackMetal) 20 slash + 60 pierce = 80
total; `FistGold` (Deep North) 114 blunt. No Iron-, Mistlands-, or
Ashlands-tier fist weapon exists in vanilla - confirms this mod's own
earlier survey.

**Real same-tier comparison weapons** (base, quality 1, total damage across
all types): Bronze `SwordBronze` 35 / `AxeBronze` 80; Iron `SwordIron` 55 /
`AxeIron` 110; Silver `SwordSilver` 105 / `MaceSilver` (Frostner) 95;
BlackMetal `SwordBlackmetal` 95 / `AxeBlackMetal` 160; Mistlands
`SwordMistwalker` 115 / `BattleaxeCrystal` 170; Ashlands `SwordDyrnwyn` 155
/ `AxeJotunBane` 190 / `MaceEldner` 135 (pure blunt); Deep North `SwordGold`
170 / `THSwordGold` (2H) 210 / `MaceGold` 170 (pure blunt) / `AxeGold` 266.

**Real fist-vs-comparison-weapon damage ratio**: consistently **~0.6-0.7x**
a same-tier one-handed sword/mace's raw damage (`FistFenrirClaw`/
`SwordSilver` = 0.57; `FistBjornUndeadClaw`/`SwordBlackmetal` = 0.84;
`FistGold`/`MaceGold`, a clean pure-blunt-vs-pure-blunt comparison, = 0.67
exactly), dropping to ~0.4-0.5x against two-handed weapons. This is a real,
consistent vanilla design pattern, not noise.

**Applied to Grapple Knuckles' tuning** (`GrappleAttackPatch.cs`): the
clone previously inherited `FistGold`'s real 114 blunt base damage
untouched - genuine Deep North-tier damage on an Ashlands item, nowhere
close to internally consistent. Now scaled to ~95 blunt (95/135 = 0.70
against the real Ashlands pure-blunt one-hander `MaceEldner`, at the upper
end of the real ratio band, matching the trend of higher-tier fists running
closer to 0.7-0.8x). Scaled proportionally, not hardcoded, so it stays
correct if `FistGold`'s own real stats ever change. The existing +40 pierce
bonus on the grapple-projectile secondary attack is untouched - that's the
utility/grapple hit, not the melee punch, and was already tuned
independently "for fun."

**Real armor values** (quality 1 / quality 4 max, per piece and set total) -
correcting this mod's own wiki-sourced guesses, which the code already
insulated against by scaling *relative* to whatever the clone inherits at
runtime rather than hardcoding an absolute (so no runtime values were ever
actually wrong - only the design commentary's stated justification was):
`ArmorFenringChest`/`ArmorFenringLegs` (Fenris) 10/16 each, 20/32 set total
- the guessed "community-sourced" numbers used to justify `ArmorScale =
1.6f` were never checked against this. `ArmorMageChest_Ashlands`/
`ArmorMageLegs_Ashlands` (Embla) 19/25 each, 38/50 set total - the mod's
"~75" guess overshot the real max-quality total by about 50%.
`ArmorDeepNorthMageChest`/`ArmorDeepNorthMagelegs` (Caller) 22/28 each,
44/56 set total - the mod's "~22/piece" guess almost exactly matched the
real *base*-quality value but undershot the real max-quality value. Worth
re-examining whether `ArmorScale`/`WeightScale` in `FenrisMageArmor.cs`,
`AshlandsHybridArmor.cs`, and `DeepNorthHybridArmor.cs` still hit the
intended power level now that the real baselines are known - not yet done,
needs a real comparison armor set at each tier (e.g. Carapace for
Mistlands) that this pass didn't pull.

**"Northern Vengeance" resolved**: the display name is real (confirmed via
web search cross-referencing a wiki page) - the internal prefab is
`StaffFrostOrbs` (+ `StaffFrostOrbsUncooked`, `Recipe_StaffFrostOrbs`, min
station level 3), a Deep North frost blood-magic staff. Real VFX/child
assets found: `vfx_FrostOrbs` (the orb visual GameObject),
`staff_FrostOrbs_projectile`, `staff_FrostOrbs_aoe`, plus a standalone
`Frost_Orbs` GameObject/material and `Frost_orbs_staff_mat`. **Design
direction, per explicit user instruction (2026-09-17), not yet
implemented**: reuse this VFX *as-is* (same visual) for both the parry proc
(`ApplyFrostProc`) and the block-break burst (`SpawnFrostBurst`) in
`ShieldOfFrostPatches.cs` - only the *damage* should be lower than
`StaffFrostOrbs`' own, reflecting Shield of Frost's lower (Mistlands vs
Deep North) tier. Whether `vfx_FrostOrbs` is a simple
`PrefabManager.CreateClonedPrefab` job (same pattern as every other cloned
projectile in this mod) or has its own attach-point/particle-system
complexity wasn't checked - open a subtask to inspect its child components
before implementing.

**Not extracted / still open**: attack speed, stamina cost, and reload time
for the comparison weapons (only spot-checked, so the ratios above are raw
per-hit damage, not DPS); why `FistGold`'s `m_damagesPerLevel` scales
"slash" while its base damage is pure blunt (observed as real vanilla data,
not explained - this mod's clone inherits that quirk unchanged); Fracturing
's real damage type (separate open question above, not covered by this
pass).

## HIGH PRIORITY, FIXED: "Flametal" was the legacy pre-Ashlands item ("Ancient Metal")

Live test, 2026-09-17: the Grapple Knuckles recipe showed a requirement
called "Ancient Metal" in-game instead of Flametal. Researched and
confirmed via AssetRipper against the user's own game files:
`Flametal.prefab`'s own name token is `$item_flametal_old` - this is
**legacy content**. When Ashlands shipped, old/pre-update Flametal got
relabeled "Ancient Metal" in-game and is no longer used in any real recipe
(per community research: "no implemented crafting recipes using Ancient
ore"). The real, current, actually-obtainable Flametal is a **separate
prefab**, `FlametalNew` (token `$item_flametal`).

This mod was using the wrong one (`"Flametal"`) everywhere. **Fixed** in
`GrappleKnucklesPlugin.cs`, `ElementalWeapons.cs`, and `ExplodingSledge.cs`
- all three now use `"FlametalNew"`. Same legacy/current split likely
doesn't apply to `FlametalOre` usage anywhere in this mod (not directly
referenced), but worth remembering if a future item ever needs raw
Flametal Ore - use `FlametalOreNew`, not `FlametalOre`.

## HIGH PRIORITY, FIXED: grapple secondary attack - three compounding bugs

Live test, 2026-09-17, iterated across several rounds as symptoms changed.
Final root-caused state, all in `GrappleAttackPatch.cs`/
`GrappleKnucklesPlugin.cs`, confirmed via decompiling `Attack.cs`/
`CharacterAnimEvent.cs`/`GrapplingPoint.cs`/`Projectile.cs` and reading the
real `GrapplingHook.prefab`/`FistGold.prefab`/`Projectile_GrapplingHook.prefab`/
`GrapplingPoint.prefab` YAML directly:

1. **Wrong animation, symptom "nothing happens" / later "doing the kick
   animation instead of the basic attack."** `clonedAttack.m_attackAnimation`
   was first overwritten with the primary punch's trigger (original code),
   then swapped to leave Knucklechains' own kick trigger untouched (first
   fix attempt), before landing on the actually-correct answer **per
   explicit direction: reuse the primary/basic punch animation**, not the
   kick. `Attack.m_attackAnimation` only selects which animator-trigger
   plays; the real "fire the attack" call
   (`CharacterAnimEvent.OnAttackTrigger()` -> `Character.OnAttackTrigger()`)
   is invoked by a Unity Animation Event baked into that specific clip -
   any clip with a working one (the punch's does) fires correctly once (2)
   below is also fixed, regardless of which of the item's own real clips
   it is.
2. **`m_attackType` was never `Projectile`, symptom "nothing happens."**
   `Attack.OnAttackTrigger()`'s dispatch (confirmed via decompile) is a
   plain switch on `m_attackType`: melee types call `DoMeleeAttack()`,
   `Projectile` calls `ProjectileAttackTriggered()` (the method that
   actually spawns `m_attackProjectile`). Knucklechains' real kick is a
   melee type (`m_attackType: 0` in `FistGold.prefab`'s own
   `m_secondaryAttack`), so even a correctly-firing animation event never
   reached the projectile-spawn code without this override.
3. **Every other projectile-launch field was still the kick's, symptom
   "projectile shows up but weakly launching in a small arc."** The kick's
   own real `m_projectileVel` is `10` (an irrelevant leftover - kicks leave
   `m_attackProjectile` null in the real prefab, so this field is never
   actually used) versus the hook's real `40`, plus a dozen more fields
   (accuracy, launch angle, hitTerrain, burst count, etc.) that were never
   copied at all. **Fix, replacing all the earlier one-field-at-a-time
   patches**: `Attack.Clone()` (a real vanilla method - `MemberwiseClone()`
   over every field) clones the hook's ENTIRE real secondary-attack config
   wholesale, then only `m_attackAnimation` (the punch, per (1)),
   `m_attackProjectile` (our cloned projectile), and `m_reloadTime`
   (intentional tuning) are overridden on top. Sidesteps ever missing
   another field the same way again.

**Fourth bug, symptom "projectile launches correctly now but still not
actually grappling"** - a structural one, not a tuning gap. The flying
projectile does **not** do the grapple-attach itself: `Projectile_GrapplingHook`'s
own real `m_spawnOnHit` field (confirmed via the prefab YAML) references a
**separate** prefab, `GrapplingPoint`, spawned fresh on impact - that's
what actually does the pull/anchor logic. This mod was only cloning the
flying projectile, so every Grapple Knuckles throw still spawned the
**original, shared** `GrapplingPoint` prefab on hit. That prefab's
`GrapplingPoint.m_equipCheck` field (an `ItemDrop` reference, confirmed via
decompile of `GrapplingPoint.cs` and cross-referencing the real prefab's
serialized GUID) is hardcoded to the real vanilla `GrapplingHook` item, and
`GrapplingPoint.Update()` calls `Break(early: true)` every single frame
`IsItemTypeEquiped(m_equipCheck.m_itemData)` fails - which compares by
exact `SharedData.m_name` token, so it could never match while wielding
`FistGold_Grapple`. This self-cancelled the grapple essentially the instant
it started. **Fix**: `GrappleKnucklesPlugin.cs` now also clones
`GrapplingPoint` (`ClonedGrapplingPoint`) alongside the projectile;
`GrappleAttackPatch.cs` points the cloned projectile's
`Projectile.m_spawnOnHit` at that clone instead of the original, and sets
the clone's `GrapplingPoint.m_equipCheck` to Grapple Knuckles' own
`ItemDrop` instead of the real hook's.

**Not yet re-tested in-game** - all four fixes are decompile-and-real-
prefab-data-backed, but confirm the full chain (animation plays, projectile
launches at real hook speed/arc, hits terrain, and actually pulls the
player without self-cancelling) on the next test pass.

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

## REMOVED: BloodMagicSpear.cs

Built, then dropped per explicit direction ("I'm not sure about the blood
magic weapon... drop the blood magic weapon"). It cloned `SpearGold` (Nord
Spear) and retuned its existing secondary attack to cost Eitr + a
percentage of current health, using the real vanilla "Blood Magic"
resource mechanic (`Attack.m_attackHealth`/`m_attackHealthPercentage`,
confirmed via direct decompile - `davrum/assembly_valheim`, `Attack.cs`
lines 82-89/460-503 and `Character.cs` lines 2531-2541). The file and its
`GrappleKnucklesPlugin.cs` wiring were deleted; the Blood Magic resource
research itself (distinct from Elemental Magic, Eitr + % current health,
engine-clamped so it can't kill the wielder) remains accurate and could be
revisited for a future item if desired - nothing about the mechanic itself
was found to be wrong, it was a design/scope call.

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

- **Open question, deliberately left as-is per explicit direction**:
  Lightning Sword clones `SwordGold`, but vanilla already has a real
  sword+lightning combo via the Iolite gem (`GemstoneBlue`) enchant system
  on `SwordNiedhogg`, raising the question of whether this item should
  instead clone a different base (e.g. `MaceGold`) to feel more distinct
  from that existing vanilla combo. Discussed but explicitly left open
  ("leave the weapon base open for the lightning weapon") - the current
  `SwordGold` base stays as-is; this is a live design question for a
  future session, not an oversight.
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

- **Localization added, 2026-09-17** (`Localization.cs`): every item
  name/description token used across the whole mod (including items
  currently held back from the test pass) is now registered via
  `LocalizationManager.Instance.GetLocalization().AddTranslation("English",
  ...)`, called from `GrappleKnucklesPlugin.Awake()` - the non-obsolete
  Jötunn API (`AddLocalization(string, Dictionary)` and `new
  CustomLocalization()` are both marked `[Obsolete]` in the 2.30.0 API,
  discovered while wiring this up). Names/descriptions are working titles,
  not final flavor text - update freely.
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

This was the highest-risk file in the mod. As of the 2026-09-17 decompile
verification pass (see that section below), the core field/method
assumptions are now confirmed rather than analogical guesses:

- **`Humanoid.UseEitr(float)`** - confirmed: inherited virtual from
  `Character` (`public virtual void UseEitr(float eitr)`), with `Player`
  overriding it to drain over RPC for multiplayer sync. Calling it on a
  `Humanoid`-typed instance dispatches correctly either way.
- **`m_leftItem`** - confirmed: `protected ItemDrop.ItemData m_leftItem`
  on `Humanoid`, holding the equipped left-hand/shield item.
- **`Character.Damage(HitData)`** - confirmed: `public void Damage(HitData
  hit)` on `Character`.
- **`Humanoid.UpdateBlock(float)` / `BlockAttack(HitData, Character)`** -
  confirmed: private/protected-override signatures match this file's
  Harmony patches exactly (`UpdateBlock` is private, hence the
  `"UpdateBlock"` string-literal patch target rather than `nameof`).
- **The parry-detection condition has one small gap, now fixed**: vanilla's
  real check is `m_timedBlockBonus > 1f && m_blockTimer != -1f &&
  m_blockTimer < 0.25f` (`m_perfectBlockInterval` confirmed as `0.25f`).
  This file's replica originally omitted the `m_blockTimer != -1f` guard -
  since `m_blockTimer` sits at `-1` whenever not currently blocking, that
  could misfire the frost proc as a "perfect parry" in the edge case where
  `BlockAttack` fires while `m_blockTimer` is exactly `-1`. Fixed to match
  vanilla exactly.
- **The AoE burst is still spawned via raw `UnityEngine.Object.Instantiate`**,
  not through Valheim's own `ZNetScene` spawn path. The burst prefab
  carries networked components (`ZNetView`/`ZSyncTransform`, per earlier
  research on this same projectile type) - a raw `Instantiate` may not
  register/replicate correctly in multiplayer. Single-player should still
  work. This remains the single biggest open risk in this file - decompile
  access confirms the field names involved, not runtime networking
  behavior, which needs an actual play session (ideally a dedicated server
  test, not just single-player) to verify.

**Omnidirectional blocking removed per explicit direction ("too fiddly")**,
2026-09-17, before ever being tried in-game. It was a rotation-swap trick
on `BlockAttack` (temporarily facing the wielder away from an off-angle hit
so it read as frontal, then restoring rotation immediately after - same
"swap state, call original, restore" pattern `QualityTransferPatch.cs` used
for item data). The sign logic was decompile-verified correct against the
real method (`if (Vector3.Dot(hit.m_dir, transform.forward) > 0f) return
false;` - a positive dot rejects the block, so rotating to force a negative
dot was the right direction), so if this is ever revisited the design was
sound; it just added more moving parts than the "too fiddly" bar allowed
for a first pass. Only three mechanics remain in `ShieldOfFrostPatches.cs`:
Eitr drain while blocking, frost proc on parry, and the block-break AoE.

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
- **Design direction, per explicit user direction (2026-09-17), not yet
  implemented**: reuse the real vanilla **"Northern Vengeance" frost-orb
  VFX as-is** (same visual, not a scaled-down/simplified one) for both the
  parry proc (`ApplyFrostProc`) and the block-break burst
  (`SpawnFrostBurst`) in `ShieldOfFrostPatches.cs` - only the **damage**
  should be lower than Northern Vengeance's own, reflecting that Shield of
  Frost is a lower tier (Mistlands) than Northern Vengeance (Deep North).
  Northern Vengeance is a real Deep North blood-magic staff whose cast
  throws a frost orb with its own VFX already in the base game - a
  background research pass this session was asked to locate its actual
  prefab/VFX child-object names via AssetRipper (see whatever it reported;
  if that didn't happen yet, this still needs a decompile/asset-extraction
  pass to find the real prefab name before it can be cloned, same pattern
  already used for the Fracturing splinter and Dundr/Embers bolt
  projectiles elsewhere in this mod - the visual itself is NOT scaled down,
  only the damage value applied to the cloned projectile). This uses the
  same `PrefabManager.CreateClonedPrefab` approach already used for
  every other cloned projectile in this mod, not a from-scratch VFX build.

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

## IDEA (not built): Hover Cape - Feather Cape upgrade

Queued during ideation, not implemented - needs real game file access to
verify the core mechanism before building. Concept: a `CapeFeather`
upgrade that lets the player hover/float midair (Hexen-wings style),
activated by jumping again while already airborne, deactivated the same
way, draining Eitr continuously while active.

Two candidate implementations, and which is right is the open question:

1. **Reuse a real vanilla flight state**, if one exists as a toggleable
   Character/Player flag independent of devcommands-only debug/creative
   fly mode (birds/fish movement and debug fly both suggest some kind of
   internal "flying" character state exists in vanilla) - would fit this
   mod's established "clone vanilla behavior, don't reimplement" pattern
   used everywhere else (grapple hook, block mechanics, armor set
   bonuses). **Needs decompile access to confirm the field/method exists
   and whether it's reachable outside the debug command path.**
2. **Fallback**: directly manipulate the player's rigidbody each
   FixedUpdate (zero gravity, apply a small upward/hover force) instead -
   more manual, but doesn't depend on an unconfirmed internal flight
   state.

Other pieces are lower-risk / already proven elsewhere in this mod:
- Trigger (jump-while-airborne vs. grounded jump) needs the real
  jump-input hook and a reliable "is grounded" check - likely exists
  already for fall-damage purposes, needs confirming.
- Eitr drain while active: same `UseEitr(rate * dt)` per-tick pattern
  already used by Shield of Frost's block-hold drain (Postfix on
  `Humanoid.UpdateBlock` there; would need an equivalent per-frame hook
  here, e.g. a Postfix on Player's update loop).
- Should verify this wouldn't collide with any other real jump-modifying
  item/effect (double-jump-style buffs) before assuming the airborne-jump
  input is free to repurpose.
- Whether Feather Cape's existing fall-damage-reduction sits on a
  `SharedData` field this upgrade can inherit directly, or needs its own
  separate handling, is also unconfirmed.

## PrismBlade.cs / PrismBladePatches.cs

New: an endgame Deep North two-handed sword (clones `THSwordGold`, "Nord
Greatsword") whose active elemental damage type cycles Fire (default) ->
Frost -> Lightning -> Poison on secondary attack use, per explicit
direction ("an elemental effect that you can change on special use").
Reuses the Deep North `Bloodgold`/`Nornathread` material family already
used elsewhere in this project's research (same not-independently-
confirmed-spelling caveat applies here too).

Two Harmony patches, both backed by a dedicated research pass this
session (not carried over from earlier, less rigorous research):

- **`ItemDrop.ItemData.GetDamage(int, float)` Postfix** - confirmed safe
  to override per-instance because `HitData.DamageTypes` is a struct
  (returned by value), so overriding `__result` never touches the shared
  `SharedData.m_damages` that every Prism Blade instance points at.
  Confirmed real precedent: EpicLoot (`RandyKnapp/ValheimMods`,
  `ModifyDamage.cs`/`ConvertPhysicalDamageToLightning.cs`) patches this
  exact method the same way. Confirmed this method is *also* called from
  tooltip/UI code, so it must only read state and override output - never
  trigger the element swap itself. Zeroes all four elemental fields and
  sets only the currently-active one, per explicit design intent (deal
  ONE element at a time, not all four simultaneously).
- **`Humanoid.StartAttack(Character, bool)` Postfix**, gated on
  `secondaryAttack && __result` - confirmed via a dedicated research pass
  that this method is polled every `FixedUpdate` while the attack button
  is held but only *returns true* once per actual successful swing start,
  and that the real mod `sighsorry1029/SecondaryAttacks` patches this
  exact method the same way for one-shot-per-swing behavior. Also
  confirmed (same research pass) that `Attack`/`Attack.Start` itself has
  no field distinguishing primary from secondary - `Humanoid.StartAttack`'s
  `secondaryAttack` parameter is the only reliable signal, and there's a
  real internal field mirroring it (`Humanoid.m_currentAttackIsSecondary`,
  protected, `Humanoid.cs:93`) that wasn't needed here since the Postfix
  already receives the parameter directly.

**Not confirmed / carried-over assumptions, same confidence level as
identical assumptions already accepted elsewhere in this mod:**

- `m_rightItem` as the private `Humanoid` field holding a two-handed
  weapon - by analogy with the `m_leftItem` assumption
  `ShieldOfFrostPatches.cs` already relies on for the shield slot, not
  freshly confirmed this session. Two-handed weapons are understood to
  occupy the right-hand slot in vanilla (same slot as one-handed
  weapons), but this specific field name wasn't decompile-verified in
  this project's own research threads.
- `Humanoid.UseEitr(float)` - same unconfirmed-but-precedented assumption
  already used in `ShieldOfFrostPatches.cs` and
  `ElementalWeaponAttackPatch.cs`.
- `MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, ...)` to
  announce the newly-active element - a very common Valheim modding
  pattern, but not decompile-confirmed in this project. Defensively
  null-checked so a wrong assumption here just silently skips the message
  rather than breaking the element swap itself.
- `ItemDrop.ItemData.m_variant` was already confirmed real/persistent
  earlier in this session's research (both ZDO and Inventory ZPackage
  serialization) - reused here as the 0-3 active-element index. Genuinely
  per-instance (unlike `SharedData` fields), so multiple Prism Blades in
  the world can each have their own active element correctly.

**Untested in-game, same as every other item in this mod**: whether the
`GetDamage` Postfix actually overrides the damage number shown in
tooltips/combat text as expected, whether the element-swap message
displays correctly, and whether `m_variant` round-trips correctly through
a full save/load cycle for this specific item.

## IDEAS (not built): a Mountain-tier mobility pair, and two Ashlands cloaks

A round of ideation (2026-09-18) produced four more queued concepts, none
implemented yet. All grew out of an observation worth stating explicitly:
Mistlands has exactly two real mobility items - the Grappling Hook
(traversal) and Feather Cape (fall safety) - and this mod already
extrapolated both of those forward a tier (Grapple Knuckles = the
Ashlands hook upgrade; the Hover Cape idea above = the Deep North cape
upgrade). The ideas below extrapolate the same two axes *backward* one
tier, to Mountain, to complete a 3-tier arc for each: Mountain -> Mistlands
-> (Ashlands or Deep North).

### Fenris Belt (Mountain, utility belt)

Clones the real Strength belt (Megingjord), recolored black/silver, but
**deliberately drops Megingjord's own +150 carry weight** - the intent is
a distinct item, not a strict upgrade of it. Materials: Fenris Hair +
Silver + (Wolf Pelt or Leather Scraps) - same material family as the
existing Fenris Mage armor, unverified exact prefab spellings.

**Finalized design, per explicit direction**: day/night-conditional
effects rather than flat always-on stats -
- **Day**: +5% movement speed, -10% dodge stamina cost.
- **Night**: +10% movement speed, plus a stealth/noise-reduction effect.

Balance reasoning (see the cape-roster research below for the numbers
this was checked against): the real **Asksvin Cloak** (Ashlands) grants
an unconditional -15% dodge stamina cost as one of three effects on a
single item, and the real **Fenris armor** set grants +9% movement speed
total (reported as 3% per piece - worth reconciling against this file's
earlier "only 2 real Fenris prefabs confirmed" note, since 9/3 implies
three pieces; unresolved, check on desktop). Landing the belt's dodge
number at 10% (below Asksvin's 15%) despite Mountain being 3 tiers
earlier than Ashlands (Plains and Mistlands both sit between them) was
judged a defensible ratio, not an overshoot.

**Open questions:**
- No vanilla item gates an effect on clock time/day-vs-night specifically
  - confirmed via dedicated research (see cape table below). The closest
  real analog is Asksvin Cloak's **Wind Run** (0-25% speed + up to -100%
  run stamina, scaled by facing vs. the live wind vector) - an
  ambient-world-state gate, just not a time-of-day one. So there's
  precedent for the *category* (ambient conditions driving an equip
  bonus, not just player actions), not the specific trigger. The
  underlying day/night read itself is a real, simple, already-existing
  vanilla system (`EnvMan`) - the novelty is only in gating an equip
  effect on it.
- No cape has a standalone stealth/noise effect in vanilla - the only
  stealth-adjacent cape effect is the Troll Hide Cape's, and that's
  gated behind wearing the full 4-piece Troll set (+15 Sneak skill), not
  a per-item effect. **Need to add: what's the practical mechanical
  difference between granting flat Sneak-skill points (like the real
  Troll set does) versus a bespoke noise-radius-reduction effect?**
  Unclear whether Sneak skill is even the real mechanism behind
  detection/noise radius, or a separate system entirely - needs
  decompile access to resolve, don't guess at it.
- Whether the real Fenris armor set's total is genuinely 3 pieces (see
  above) is unresolved and affects how directly comparable that 9%
  figure actually is.

### Needle Cape (Ashlands, recolored Feather Cape)

Grew out of a vaguer "Ashlands mage cloak, maybe fire resist" idea - see
the monster-resistance research below for why fire-resist-for-survival
still makes sense even though the mechanic below isn't fire-themed.
Recolors the real Feather Cape; the benefit is **damage reflected back at
melee attackers**, as a **percentage of the incoming hit, not a flat
number** - explicitly chosen so it stays relevant into later biomes
rather than being trivialized by Ashlands/Deep North damage scaling.

- **Mechanism, low implementation risk**: reuses the exact pattern
  already proven in `ShieldOfFrostPatches.cs` - build a `HitData` and
  call `attacker.Damage(hitData)` directly (that file already does this
  for the frost-on-parry proc). This would read the *incoming* hit's
  damage, take a percentage of it, and fire it back - just triggered on
  any incoming melee hit instead of gated to a perfect parry.
- **No resource cost** - deliberate design call: the cost is the
  opportunity cost of the cape slot itself (not wearing Lox Cape's frost
  resist, or Feather Cape's own fall protection, or Asksvin/Ashen's
  stamina effects instead), not an Eitr/health drain.
- **Real precedent exists, but only qualitatively**: initially
  misidentified via item-list categorization as a plain offensive staff,
  a corrected research pass (prompted by the user's own play experience
  overriding the wrong first pass - see note on trusting firsthand
  play-testing over categorization-only research) confirmed
  **Northern Vengeance** (`StaffFrostOrbs`, Deep North, Blood Magic) is
  actually a **"Vengeance Sphere"** - a genuine caster-centered
  reflect/retaliation shield ("should your enemy hurt you, it shall
  reflect back upon them at once"), costing 100 Eitr + 40% current
  health per cast (one-time cost, medium confidence, not continuous).
  **The actual reflect percentage/radius/duration for that real ability
  was never found** - the wiki pages most likely to have it were
  proxy-blocked in this environment. So Needle Cape's reflect percentage
  is an original balance call, not lifted from a confirmed vanilla
  number, unlike most of this mod's other tuning so far.
- **Unconfirmed**: whether the incoming `HitData` actually distinguishes
  melee from ranged/projectile sources (needed to gate this to melee
  attackers only, per the original design intent) - reasonable to assume
  it does, not verified. Also unresolved: whether to cap the reflected
  amount alongside the percentage, to avoid a degenerate huge reflect off
  a single massive hit (a boss mechanic, say) - deliberately left open,
  not decided.

### Feather Fall Potion (Mountain, consumable - first mead/potion in this mod)

Reuses the real Feather Cape's exact fall-damage-immunity effect, but as
a short ~30-second consumable burst rather than permanent equipment -
explicitly framed as an emergency "pop it right before a specific big
jump/descent" item, not a pre-buff-and-explore item like vanilla's
existing resistance meads.

- **This is a deliberate tier/permanence compression**: the real Feather
  Cape is Mistlands-tier (confirmed, not Mountain), so this potion pulls
  a later permanent effect down to an earlier, temporary, lower-commitment
  version - consistent with the mobility-arc framing above (temporary at
  Mountain, permanent once you reach the real item at Mistlands).
- **No real vanilla mead uses genuinely Mountain-native materials** -
  checked all ~21 known real meads/potions/wines, none use Wolf
  Pelt/Fang, Freeze Gland, Silver, Obsidian, Drake Trophy, etc. Frost
  Resistance Mead (the mead that gets you *through* Mountain) itself only
  uses Black Forest/Swamp-tier materials (10 Honey, 5 Thistle, 2 Bloodbag,
  1 Greydwarf eye, confirmed 2+ sources) - "protect against tier N using
  materials from tier N-1" is the real vanilla pattern, not an assumption.
- **Recipe: Feathers + Wolf Pelt + Honey.** Feathers confirmed
  early/common (crows, hens, gulls; available from Meadows onward, not
  Mountain-exclusive). Wolf Pelt confirmed a common guaranteed drop (not
  a rare grind like Drake Trophy, which has a wildly inconsistent
  real-world reported drop rate around ~15%). Honey is the near-universal
  real mead filler ingredient, appearing in the large majority of real
  recipes including Frost Resistance Mead itself.
- **Real precedent for the general shape**: **Lightfoot Mead** (2 Scale
  Hide, 5 Feathers, 5 Magecap -> -30% jump stamina, +20% jump height,
  10 min) already pairs Feathers with a jump/movement effect in a real
  recipe - confirms "Feathers + jump-related buff" isn't an invented
  combination.
- **No real precedent for the ~30-second duration specifically** - every
  real resistance mead runs 600s (10 min); this mod's much shorter,
  "emergency item" duration is a genuinely new duration tier, not modeled
  on an existing mead.
- Mead Ketill/Fermenter mechanics confirmed: base is made at a Mead
  Ketill (Forge-adjacent, no station-level gating found - recipes unlock
  by simply holding ingredients), then fermented ~2 in-game days at a
  Fermenter (needs a nearby Lv1 Workbench + roof/70% cover) to yield
  (typically 6) finished potions. This would be the first consumable/mead
  in the whole mod - everything else so far is equip-slot gear. Mead
  status effects reuse the same `StatusEffect` system already used for
  every armor/cape equip effect in this mod, just triggered by
  consumption instead of equip - not a new subsystem, just a new trigger
  path.

### Wolf-Pelt Parachute (Mountain, alternative to the potion - not both)

**Explicitly positioned as an "OR" alternative to the Feather Fall
Potion, not an addition** - per explicit direction, probably one or the
other, decision deferred. Concept: clone the real boat sail prefab
(already an animated, proven vanilla asset), reorient it from
vertical/mast-mounted to horizontal/spread-above-the-player, recolor
toward a wolf-pelt look, and use it as a Fenris-family "deliberate,
equipped, triggered descent tool" rather than the potion's "reactive
emergency" framing. Cords rendered as four `LineRenderer` lines from the
player to anchor points above them. Materials: Feathers + Wolf Pelt +
Fenris Hair (again the Fenris material family).

This is the biggest lift in this batch, but meaningfully de-risked by
reusing a real, already-animated asset instead of authoring new geometry
- consistent with this mod's whole "clone something real, retune/reflavor
it" pattern, just applied to a cloth/sail asset instead of a
weapon/armor/projectile prefab for the first time.

**Everything below needs real desktop/AssetRipper access, not more web
research** - wikis document gameplay stats, not internal GameObject/
component structure, so none of this can be resolved remotely:
- The real sail prefab's exact name(s).
- Whether the sail's billowing motion is driven by a Unity `Cloth`
  component reacting to wind + anchor points, or a simpler baked/scripted
  animation - this materially changes how well reorienting it 90 degrees
  and reparenting it to a player (instead of a mast/boom rig) will
  actually behave. Cloth-driven motion might carry over oddly once
  detached from its original rigging; baked animation would reorient more
  predictably but wouldn't be genuinely wind-reactive.
- Whether a real fur/pelt texture exists whose UV mapping could
  reasonably apply to a sail-shaped mesh. **Managing expectations**: the
  established `MaterialPropertyBlock` recolor trick (already used for
  Shield of Frost) will get the right color palette, but a flat tint
  alone won't add actual fur texture/detail - a sail's cloth material and
  UVs aren't built to look like pelt, so "the right color" and "visibly
  furry" are different bars to clear.
- "Several pelts stitched together" (multiple cloned sail panels instead
  of one, patchwork look) is a nice v2 idea, not a v1 target - get one
  panel working and looking right first.

### Shared open question (relevant to 3 different items now)

How the real Feather Cape's fall-damage protection is actually
implemented at the code level - a hard fall-damage-immunity flag, a
max-fall-speed cap, or a damage-reduction multiplier - is still
unconfirmed (already flagged under the Hover Cape idea above), and now
matters for **three** separate queued items: Hover Cape, Feather Fall
Potion, and the Wolf-Pelt Parachute. Worth resolving once on desktop
rather than re-deriving it three times.

### Cape roster research (reference data for the above)

Full real cape/cloak roster, confirmed via the Jötunn item-list plus
cross-referenced search snippets (many wiki/datamining sites were
proxy-blocked for direct fetch this session - noted per-row where
relevant):

| Cape | Tier | Effects | Conditional? |
|---|---|---|---|
| Deer Hide | Bronze | None (pure armor) | - |
| Troll Hide | Bronze | Alone: nothing. Full 4-piece Troll set: +15 Sneak skill | Set-gated |
| Wolf Fur | Mountain | Frost resistance | Always-on |
| Lox | Plains | Frost resistance | Always-on |
| Linen | Pre-Mistlands | None (no frost resist, unlike Lox) | - |
| Feather | Mistlands | 100% fall damage reduction, frost resist; 2x fire damage taken | Always-on |
| Ashen Cape | Ashlands | Frost resist, -10% attack stamina, -20% block stamina | Always-on |
| Asksvin Cloak | Ashlands | Frost resist, -15% dodge stamina, Wind Run (0-25% speed + up to -100% run stamina, wind-facing-scaled) | Wind Run only |
| Moose Hide | Deep North | Frost resist, -20% attack stamina, -20% run stamina | Always-on |
| Cape of the Caller | Deep North (mage) | Frost resist, +Eitr regen, -dodge stamina (%s single-source) | Always-on |
| Cape of Odin | Legacy/supporter DLC, not normal progression | None found besides armor (single-source, low confidence) | - |

(`CapeTest` also exists in the prefab dump but is a dev-only debug item,
excluded.)

### Ashlands monster fire-resistance research (context for Needle Cape's origin)

Checked whether a fire-offense theme would even work well in Ashlands
before landing on Needle Cape's reflect mechanic instead. **Confirmed:
fire generally underperforms against Ashlands' own monster roster** - 7
of 9 checked enemies resist or are immune to fire, including Bonemaw
Serpent, Lava Blob, and the boss Fader (all fully immune, not just
resistant). The Charred faction (all 4 variants) and Fallen Valkyrie
share a **spirit** weakness instead; Bonemaw and Lava Blob share a
**frost** weakness; Morgen's outlier weakness is **lightning**. Volture's
data was genuinely conflicting between sources - left unresolved. One
roster correction: Growth/Tar Pits are Plains, not Ashlands, despite
being weak to fire themselves - excluded from the pattern above as a
biome mismatch. Practical upshot: fire-resistance-for-the-wearer is
well-justified for an Ashlands item (the biome itself constantly burns
you), but a fire-*offense* angle would fight the biome's own monster
roster rather than working with it.

## IDEA (not built): Water Walking Potion / "Hydrophobia" (Swamp, consumable)

First idea for the Swamp tier - this mod has nothing there yet. Concept:
a several-minute (5-10 min under consideration) consumable that lets the
player walk on water's surface without sinking/swimming, deliberately
expensive/hard to obtain given how powerful the effect is. Possible
secondary effect floated: also prevents the wearer from getting the real
vanilla "Wet" status while active (i.e., water-repellent, not just
water-walking) - name still undecided, "Hydrophobia" was floated as a
pun on the secondary effect, or it could just stay "Potion of Water
Walking."

Explicit framing from ideation: especially useful for the Bonemass fight
specifically, since the Swamp's terrain/mud is normally a hindrance
there.

**Confirmed via dedicated research:**
- **No vanilla water-walking mechanic exists anywhere** - not a mead, not
  an item effect, not a normal gameplay toggle. The only way to cross
  water without swimming in vanilla is the `fly` debug/creative console
  command, which is a dev tool, not an in-game item. Corroborated by the
  fact that community mods exist specifically to add this
  (`WaterWalkingPotion`, `EnhancedPotions`) - if it existed in vanilla,
  those mods wouldn't need to. **This is a genuinely novel mechanic for
  this mod to build, not an extension of anything real** - similar
  situation to Needle Cape's reflect percentage, another case where this
  mod is inventing rather than reusing.
- **Wraith Trophy** (`TrophyWraith`) is real, dropped by nighttime Swamp
  Wraiths. Exact drop rate is disputed between sources (33% vs 5%) -
  don't cite a specific number. **Has no real recipe use in vanilla at
  all** - this potion would be the first thing that ever consumes it,
  which fits the "make gross use of an otherwise-decorative material"
  flavor the ideation wanted.
- **Ancient Bark** (real prefab name `ElderBark`, display name "Ancient
  Bark") is real, obtained by chopping Ancient trees in Swamp, confirmed
  used in Root Armor (10/piece) and the Ancient Bark Spear - solidly
  real Swamp-tier material.
- **Blood Bag** reconfirmed real and Swamp-tier (Leech drop, already
  used in Frost Resistance Mead's real recipe) - but it's a common,
  guaranteed drop, so it doesn't add much scarcity to a recipe meant to
  be "hard to get." **Honey may be the better third ingredient instead**
  - still real effort (bee farming) without diluting the gating the way
  a trivially common material would. Not decided - Blood Bag remains
  thematically apt for the "gross" framing (wood pulp + blood + wraith
  robe wrung out) even if it's not the scarcity-adding ingredient.
- **Duration precedent**: every real resistance mead (Frost/Poison/Fire)
  runs exactly 600s (10 min), confirmed 3+ sources. 10 min would match
  that pattern exactly; 5 min would read as an intentional restriction
  given the effect's power - both defensible, not decided.

**Not confirmed / needs decompile access:**
- Whether Swamp's mud/muck slowdown is its own distinct mechanic, or
  actually the same water-depth/wading-height check reacting to the
  biome's ubiquitous shallow bog (in which case a water-walking effect
  would plausibly bypass it "for free," since it'd be the same
  underlying system) - versus being unrelated entirely (the "Wet" status
  effect, or flat armor movement penalties). Two competing explanations
  found, web research can't adjudicate between them. **Design
  implication: build/promise "walks on water" as the guaranteed effect;
  treat "also bypasses Bonemass-fight mud" as an untested bonus, not a
  guaranteed feature**, until verified on desktop.

## IDEA (not built, less certain): early "broken" magic staff (Plains)

Rougher idea, explicitly flagged by the user as less certain than the
others in this batch. Concept: an early, unreliable magic item for
Plains - a makeshift/broken staff that fires a weak fireball, inspired by
a Plains enemy's own fire ability. Since Eitr doesn't exist yet this
early (confirmed established lore already in this mod - no Eitr items
exist before Mistlands), the resource cost would be **very low
durability instead of Eitr** - maybe ~10 shots before it breaks, repaired
at a deliberately inconvenient station to keep it feeling scavenged/
unreliable rather than a proper crafted tool. This would be a genuinely
different resource model than every other elemental item in this mod
(all Eitr-costed) - durability-as-resource instead.

**Confirmed via dedicated research:**
- Correct enemy: the **Fuling Shaman** (Valheim's Plains humanoids are
  called Fulings, not "Goblins" - that's a player nickname). It has a
  real fireball ability (Burning: 20 blunt + 100 fire, 15 m/s, 4s travel,
  3s cooldown, 0-20m range) - confirmed real, but it's one of *three*
  separate abilities the Shaman has (melee staff-jab, the fireball, and a
  party-shield buff) - "shaman staff that shoots a weak fireball" is a
  fair simplification of the real kit, not a literal 1:1 copy of a single
  attack.
- **No player-equippable weapon/fireball item exists in Jötunn's
  item-list** for this ability - confirmed by direct fetch. **Important
  nuance, corrected after pushback**: this only rules out a *player
  item* already wired into the item registry. It does NOT rule out the
  enemy-side model (the Shaman's staff mesh) or the fireball's VFX/
  projectile prefab existing in the raw game files, since enemies still
  need assets to render their own weapons and spells even without a
  player-facing item wrapping them. **This is genuinely checkable on
  desktop with AssetRipper targeting the Fuling Shaman's own prefab
  specifically (not just the player item-list)** - if the mesh/VFX
  assets turn out to be accessible and cloneable, this item is a much
  smaller lift than "fully custom from scratch." Worth checking before
  assuming the worst case.
- **Artisan Table is real, but wrong on both the tier and the type of
  station guessed**: it's actually **Mountain**-tier (unlocked via Dragon
  Tears, a Moder drop), not Mistlands - but more importantly, **it's a
  building-unlock station** (Blast Furnace, Spinning Wheel, Windmill,
  Stone Oven), not a gear repair/crafting station at all. Don't use it
  regardless of tier - it's the wrong tool, not just the wrong tier.
- **Real Plains-tier repair/crafting station**: Padded Armor (the real
  Plains armor set) uses **Forge lvl 1-2**. **Workbench lvl 2** is also
  real for some Plains gear and might fit the "broken/makeshift" flavor
  better than a proper Forge would - either is a defensible choice.
- **The "expensive/hard to repair" mechanic has zero vanilla precedent**:
  confirmed vanilla repair is always free, for every item, at every
  station - no material-cost repair gate exists anywhere in the base
  game to model this on. This piece of the design would be a fully
  custom mechanic (a Harmony patch intercepting the repair action to
  impose a cost), not an adaptation of anything real, and has no
  existing balance reference point to borrow from - would need to be
  tuned purely by feel. This raises this item's overall complexity closer
  to the Wolf-Pelt Parachute's than to the rest of this mod's "clone and
  retune" items.

## Needle Cape (Plains, recolored Feather Cape mesh - not its stats/recipe)

**Resolves the earlier open tier question** - confirmed Plains, not
Ashlands (the user recalled conflating it with a separate, still-
undefined "Ashlands cape" idea - see below). Thematically tied to
Deathsquitos. Benefit: reflects a percentage of incoming melee damage
back at attackers (see the original design entry above for the full
mechanism/precedent discussion - unchanged, only the tier moved).

- **Materials, confirmed real**: **Needle** (~20 of them - a lot,
  deliberately, both to justify "covered in needles" visually and to
  gate the recipe) + **Tar** + **Lox Pelt**. Needle is dropped by
  Deathsquitos at a **guaranteed 100% rate** - Deathsquitos also die in
  one hit to nearly anything, so "~20 Needles" is grindy in the
  "go find 20 of them" sense, not RNG-punishing. Tar and Lox Pelt are
  both confirmed real Plains materials, Lox Pelt a common/guaranteed
  drop. **No existing real recipe combines all three** - this is a novel
  pairing, fine for a custom item, just not leaning on an established
  recipe pattern. Needle's only other real use: Needle Arrow (4 Needle +
  2 Feathers, Workbench lvl 4).
- **Important correction - do not clone Feather Cape's stats/recipe**:
  the real Feather Cape is **Mistlands-tier** (Galdr Table, 10 Feathers +
  5 Scale Hide + 20 Refined Eitr - both Scale Hide and Refined Eitr are
  Mistlands-only materials), not Plains. "Reskinned Feather Cape" only
  works as a **visual/mesh starting point** - the recipe and station
  need to be built fresh at Plains-tier values (Forge or Workbench, both
  confirmed real for Plains gear), not inherited from the real item.
- Station: Forge or Workbench, both real options at Plains tier - not
  yet decided which.

## DECISION: Grapple Knuckles re-tiered from Ashlands to Deep North

Per explicit direction from a live desktop session (not yet reflected in
this repo's code as of this writing - check `GrappleKnucklesPlugin.cs`
against this before assuming it's already done): Grapple Knuckles moves
from Ashlands to **Deep North**, and should now use the **real `FistGold`
directly as the base item** (not just as a model/mechanics clone source
with a substitute recipe, the way the Ashlands version was designed) -
reasoning being a two-tier jump (Mistlands hook -> Deep North knuckles)
reads as a more significant, satisfying upgrade than a one-tier jump
would. **This means Ashlands currently has zero mod items** - update any
assumption elsewhere in this file that Grapple Knuckles still fills
Ashlands' gap.

## Two Ashlands capes: Elemental Magic and Blood Magic variants

Fills the Ashlands gap left by Grapple Knuckles' move to Deep North.
Both real Ashlands capes (Ashen Cape, Asksvin Cloak - see the roster
table earlier in this file) already cover stamina economy and movement;
neither touches Eitr regen or magic skill, which is the gap these two
custom capes are designed to fill instead of duplicating what's real.

**Shared design, both capes:**
- Materials: **Asksvin Hide + Morgen Sinew** (same material family the
  two real Ashlands capes already use) + one gem, differing per variant.
- **Eitr regen, deliberately less than the real Deep North "Cape of the
  Caller"** - not a hard number yet, just a design intent to keep Deep
  North's own mage cape feeling like a genuine upgrade rather than being
  outclassed by an earlier-tier item.
- **+15 to the relevant magic skill** - confirmed via dedicated research
  as the actual real vanilla convention for this exact mechanic (Troll
  Hide Cape's +15 Sneak, Fenris armor's +15 **Fists** - not "Unarmed",
  corrected via research - and a third confirmed precedent, Root Armor's
  +15 Bows; all three cap at 100). No vanilla item targets Elemental
  Magic or Blood Magic specifically with this mechanic, so applying it to
  these two skills is a new combination, not an existing pattern
  extended - but the magnitude (+15) is well-grounded, not invented.
- **Design rationale for why you'd pick this over just rushing to Deep
  North's stronger regen-only cape**: a flat skill bonus isn't just "more
  frequent casts" - both Elemental Magic and Blood Magic's real mechanics
  (confirmed earlier in this project's research) scale with skill level
  beyond just cast frequency (Blood Magic's health-cost percentage,
  Elemental Magic's output), so a skill bump plausibly makes each cast
  better AND cheaper, not just more frequent. This resolves a "pretty
  thin trade" concern raised during design - worth re-verifying this
  scaling claim still holds before leaning on it too hard.

**Elemental Magic variant**: gem = **Iolite** (real Ashlands material,
drops from Charred chests in Charred Fortresses - confirmed real, but
note it's normally a melee-weapon lightning enchant material in vanilla,
e.g. Nidhögg the Thundering, with **no real connection to the Elemental
Magic skill** - using it here is flavor/thematic, not a mechanically
grounded borrow from an existing system).

**Blood Magic variant**: gem = **Bloodstone** (`GemstoneRed`) - NOT
"Blood Gem," which does not exist as a real item (confirmed, checked
directly). Bloodstone is real and already used elsewhere in this mod
(Dundr), but like Iolite it's normally a weapon-damage enchant material
(scales with the *wielder's own missing HP*) with no real tie to the
Blood Magic skill - same flavor-only caveat applies.

**A third "maybe" idea, explicitly lower priority, not fully committed**:
a Jade-gemmed cape granting poison resistance (useful against Deep
North's lingering poison threats, though not a hard one to deal with
otherwise per the user's own framing). **Confirmed no real vanilla
precedent supports this** - Jade adds poison *damage* to weapons, actual
poison resistance only comes from the real Poison Resistance Mead. Not
dropped, just flagged as weaker-grounded than the other two.

Station not yet decided - both real Ashlands capes provide precedent for
either choice (Ashen Cape: Black Forge Lv3; Asksvin Cloak: Galdr Table
Lv2).

## IDEA (not built, materials/mechanics still being verified): Asksvin Belt (Ashlands)

Grew directly out of a real solo Ashlands-boss playthrough (with troll
summons, so "solo" loosely) - the user found the fight's lava terrain and
fire AoEs genuinely hazardous and wants a belt-slot item addressing that
specifically, "strong but boxed into that biome" by design (a
deliberately powerful, narrowly-scoped item rather than a broadly
progression-breaking one).

Concept: materials Asksvin Hide + Flametal + possibly Celestial Feather +
one more undecided rare material. Two effects: (1) lava
immunity/no-lava-damage, modeled on the belief that Asksvin creatures
themselves are immune to lava - **not yet verified, do not assume true**;
(2) prevents the player from catching fire (the Burning status
specifically) without reducing direct fire damage taken - explicit design
intent that Fire Resistance Barley Wine would still be worth carrying for
the raw damage reduction, this item only stops ignition, giving it real
but bounded power. Research in progress on: whether Asksvin are actually
lava-immune, whether lava damage is mechanically distinct from the fire
damage type or just an extreme case of it (determines whether "lava
immunity" needs a wholly custom mechanic or can reuse something),
whether "Burning" is a real separable status effect from a direct fire
damage hit (some evidence already found via the Fuling Shaman research:
its fireball is described as "Burning: 20 blunt + 100 fire dmg," which
at least suggests Burning is named/tracked as distinct from the instant
hit), whether Celestial Feather is real, and what damage type Lava
Blobs' death explosions actually deal (the user wasn't sure themselves -
suspects fire + blunt).

## Gap check: Plains probably doesn't need a mobility item

Revisited per explicit direction - initially flagged as a gap (every
other tier has at least one mobility item: Swamp's Water Walking Potion,
Mountain's Fenris Belt + Feather Fall Potion/Parachute, Mistlands' real
vanilla Grappling Hook + Feather Cape, Deep North's Hover Cape idea and
now Grapple Knuckles itself per the re-tier above), but **reasoned
through and likely not needed**: Fenris Belt (Mountain-tier, not
biome-gated) already carries forward usefully into Plains, and Plains'
terrain isn't especially hard to traverse compared to Mountain or
Ashlands. A "hard to get fire resist cape" alternative was floated but
explicitly set aside - it would undercut the real Fire Resistance Barley
Wine's usefulness (consumable potion pressure) without a clean way to
gate it specifically to "just before the boss fight" (**confirmed no
real vanilla precedent exists for gating an item's availability to
proximity-to-a-specific-boss within a single biome** - the real pattern
is always cross-tier, needing the *previous* boss's material to progress,
not an in-biome "you're close" gate). Left as: Plains likely doesn't need
a dedicated mobility item, not an unresolved gap.

Note: Needle Cape's original design entry (further up this file, under
"IDEAS (not built): a Mountain-tier mobility pair, and two Ashlands
cloaks") still describes the reflect mechanism/Northern Vengeance
research and predates the Plains re-tier - the tier and materials
sections above supersede that entry's Ashlands framing, the mechanism
discussion there is still accurate.
