using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace GrappleKnuckles
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class GrappleKnucklesPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.samm.grappleknuckles";
        public const string ModName = "Grapple Knuckles";
        public const string ModVersion = "0.1.0";

        // Vanilla prefabs we read from.
        public const string SourceItemPrefabName = "FistGold"; // Nord Knucklechains - model/mechanics source only, see CloneKnucklechains
        public const string VanillaHookPrefabName = "GrapplingHook"; // Mistlands grappling hook (NOT Deep North - corrected, see CLAUDE.md)
        public const string VanillaHookProjectilePrefabName = "Projectile_GrapplingHook";
        public const string ClonedItemPrefabName = "FistGold_Grapple";
        public const string ClonedProjectilePrefabName = "Projectile_GrapplingHook_Knuckles";

        // The flying projectile does NOT do the actual grapple-attach itself -
        // confirmed via the real prefab data, 2026-09-17: Projectile_GrapplingHook's
        // m_spawnOnHit spawns a SEPARATE prefab, "GrapplingPoint", on impact,
        // and THAT prefab's GrapplingPoint.m_equipCheck (checked every frame
        // during an active pull) is what was still hardcoded to the real
        // vanilla GrapplingHook item. Cloning only the flying projectile left
        // every Grapple Knuckles throw spawning the ORIGINAL shared
        // GrapplingPoint, which broke the grapple early (Break(early: true))
        // the instant it checked "is the player still holding a GrapplingHook"
        // and found FistGold_Grapple instead. See GrappleAttackPatch.cs.
        public const string VanillaGrapplingPointPrefabName = "GrapplingPoint";
        public const string ClonedGrapplingPointPrefabName = "GrapplingPoint_Knuckles";

        // "Flametal" is the pre-Ashlands-update legacy prefab, renamed
        // in-game to "Ancient Metal" ($item_flametal_old) and no longer
        // obtainable/used in any real recipe - confirmed via AssetRipper
        // against the user's own game files, 2026-09-17. The real, current
        // Flametal item is a separate prefab, "FlametalNew" ($item_flametal).
        private const string FlametalPrefabName = "FlametalNew";
        private const string CharredBonePrefabName = "CharredBone";

        // Cloned once PrefabManager.OnVanillaPrefabsAvailable fires, so
        // GrappleAttackPatch can give it its own damage without mutating the
        // GameObject the vanilla GrapplingHook item shares.
        public static GameObject ClonedProjectile { get; private set; }

        // See VanillaGrapplingPointPrefabName above for why this needs its
        // own clone too, not just the flying projectile.
        public static GameObject ClonedGrapplingPoint { get; private set; }

        // Read fresh (via .Value) everywhere it's used, not cached, so a
        // live edit through Configuration Manager (already in this test
        // profile) takes effect immediately without a relaunch.
        public static ConfigEntry<float> CooldownDuration { get; private set; }

        // LOCKED IN 2026-09-17 per explicit direction ("flametal shield
        // texture looks best still, let's lock that in") - was a live
        // Configuration Manager dropdown for trying different real
        // materials; removed now that a final choice is made. See
        // ApplyChainTexture().
        private const string ChainTextureMaterialName = "Shield_Flametal_mat";

        private static GameObject _clonedItemGameObject;

        private readonly Harmony _harmony = new Harmony(ModGUID);

        private void Awake()
        {
            // Default matches the real vanilla GrapplingHook's own reload
            // time (m_reloadTime: 2, confirmed via the real prefab data) -
            // per explicit direction, restoring the "normal" hook cooldown
            // rather than the arbitrary 3s this started as.
            CooldownDuration = Config.Bind(
                "Grapple",
                "CooldownDuration",
                2f,
                new ConfigDescription(
                    "Seconds of cooldown after firing the grapple (and required before first use after equipping), " +
                    "matching the real vanilla Grappling Hook's own reload time by default.",
                    new AcceptableValueRange<float>(0f, 15f)));

            Localization.Init();

            // Item cloning subscribes to PrefabManager.OnVanillaPrefabsAvailable,
            // NOT ItemManager.OnItemsRegistered - confirmed 2026-09-17 against
            // Jotunn's own official TestMod reference (every cloned-item example
            // there uses OnVanillaPrefabsAvailable; OnItemsRegistered is never
            // used for item creation). OnItemsRegistered fires on ObjectDB.Awake,
            // a different/less reliable timing signal - items registered there
            // showed up fine at the main menu in testing but never made it into
            // an actual loaded world's crafting list. See CLAUDE.md.
            PrefabManager.OnVanillaPrefabsAvailable += CloneKnucklechains;
            // Everything except Grapple Knuckles held back per explicit
            // direction, 2026-09-17: testing one item at a time. Code for
            // all of these is untouched - just not registered/initialized,
            // so only Grapple Knuckles appears in-game for this test pass.
            // Uncomment one at a time as each is verified working.
            // PrefabManager.OnVanillaPrefabsAvailable += MountainTierAxe.Clone;
            // PrefabManager.OnVanillaPrefabsAvailable += FenrisMageArmor.Clone;
            // PrefabManager.OnVanillaPrefabsAvailable += AshlandsHybridArmor.Clone;
            // PrefabManager.OnVanillaPrefabsAvailable += DeepNorthHybridArmor.Clone;
            // PrefabManager.OnVanillaPrefabsAvailable += PrismBlade.Clone;
            PrefabManager.OnVanillaPrefabsAvailable += CloneProjectile;
            // ElementalWeapons.Init();
            // ShieldOfFrost.Init();
            // ExplodingSledge.Init();

            _harmony.PatchAll();

            // Real functionality, not diagnostics - registered manually for
            // the same reason as the probe above (PatchAll() doesn't apply
            // attribute-based patches to Attack.Start reliably). See
            // GrappleCooldownPatch.cs.
            GrappleCooldownPatch.RegisterManualPatch(_harmony);
            Projectile_ScaleGrappleDamage_Patch.RegisterManualPatch(_harmony);
            GrappleFireSoundPatch.RegisterManualPatch(_harmony);

            Jotunn.Logger.LogInfo($"{ModName} {ModVersion} loaded");
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= CloneKnucklechains;
            // PrefabManager.OnVanillaPrefabsAvailable -= MountainTierAxe.Clone; // see Awake()
            // PrefabManager.OnVanillaPrefabsAvailable -= FenrisMageArmor.Clone; // see Awake()
            // PrefabManager.OnVanillaPrefabsAvailable -= AshlandsHybridArmor.Clone; // see Awake()
            // PrefabManager.OnVanillaPrefabsAvailable -= DeepNorthHybridArmor.Clone; // see Awake()
            // PrefabManager.OnVanillaPrefabsAvailable -= PrismBlade.Clone; // see Awake()
            PrefabManager.OnVanillaPrefabsAvailable -= CloneProjectile;
            // ElementalWeapons.Dispose(); // see Awake()
            // ShieldOfFrost.Dispose(); // see Awake()
            // ExplodingSledge.Dispose(); // see Awake()
            _harmony?.UnpatchSelf();
        }

        private void CloneProjectile()
        {
            if (ClonedProjectile == null)
            {
                try
                {
                    ClonedProjectile = PrefabManager.Instance.CreateClonedPrefab(
                        ClonedProjectilePrefabName, VanillaHookProjectilePrefabName);
                    // Detects a miss (despawns without hitting anything) so
                    // GrappleCooldownPatch can still queue the cooldown -
                    // see GrappleCooldownPatch.cs.
                    ClonedProjectile.AddComponent<GrappleMissDetector>();

                    Jotunn.Logger.LogInfo($"Cloned {VanillaHookProjectilePrefabName} -> {ClonedProjectilePrefabName}");
                }
                catch (System.Exception ex)
                {
                    Jotunn.Logger.LogError($"Failed to clone {VanillaHookProjectilePrefabName}: {ex}");
                }
            }

            if (ClonedGrapplingPoint == null)
            {
                try
                {
                    ClonedGrapplingPoint = PrefabManager.Instance.CreateClonedPrefab(
                        ClonedGrapplingPointPrefabName, VanillaGrapplingPointPrefabName);

                    Jotunn.Logger.LogInfo($"Cloned {VanillaGrapplingPointPrefabName} -> {ClonedGrapplingPointPrefabName}");
                }
                catch (System.Exception ex)
                {
                    Jotunn.Logger.LogError($"Failed to clone {VanillaGrapplingPointPrefabName}: {ex}");
                }
            }
        }

        private void CloneKnucklechains()
        {
            try
            {
                // Re-tiered to Ashlands per explicit direction: the intended
                // progression is "get the plain Grappling Hook easily in
                // Mistlands, then upgrade to this fist weapon in Ashlands."
                // That means the recipe deliberately does NOT require a real
                // FistGold (Deep North) - gating an Ashlands-tier, pre-Deep-
                // North item behind Deep North would contradict the whole
                // point. FistGold is still the CustomItem clone source for
                // model/mechanics only (per explicit "use the Deep North
                // fist weapon model" direction) - Jötunn's clone is a
                // design-time template copy, not a crafting requirement, so
                // this is safe to do without needing the player to ever
                // actually own a FistGold.
                //
                // Station: "blackforge" - confirmed real for Ashlands
                // weapon-tier crafting too (Dyrnwyn, Nidhögg both use it),
                // not exclusive to Deep North/Mistlands despite where this
                // mod first used it.
                var itemConfig = new ItemConfig
                {
                    Name = "$item_fistgold_grapple",
                    Description = "$item_fistgold_grapple_description",
                    CraftingStation = "blackforge",
                    Requirements = new[]
                    {
                        new RequirementConfig { Item = VanillaHookPrefabName, Amount = 1 },
                        new RequirementConfig { Item = FlametalPrefabName, Amount = 15, AmountPerLevel = 10 },
                        new RequirementConfig { Item = CharredBonePrefabName, Amount = 3, AmountPerLevel = 2 },
                    },
                };

                var clonedItem = new CustomItem(ClonedItemPrefabName, SourceItemPrefabName, itemConfig);
                ItemManager.Instance.AddItem(clonedItem);

                _clonedItemGameObject = clonedItem.ItemDrop.gameObject;
                ApplyChainTexture();

                Jotunn.Logger.LogInfo($"Cloned {SourceItemPrefabName} -> {ClonedItemPrefabName}");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to clone {SourceItemPrefabName}: {ex}");
            }
        }

        // Chain recolor, per explicit direction. FIXED 2026-09-17 (two
        // rounds):
        //   1. MaterialPropertyBlock.SetPropertyBlock() (same technique
        //      cited for Shield of Frost's silver tint) silently did nothing
        //      visible - confirmed by pulling and reading the real source of
        //      Rexabit/valheim-visuals-modifier (previously only cited
        //      secondhand, never actually verified until now): it does NOT
        //      use MaterialPropertyBlock. It clones the Material asset and
        //      reassigns renderer.sharedMaterials directly, once, at
        //      ObjectDB.Awake time. sharedMaterials is real serialized
        //      renderer state, so it's copied forward whenever Unity
        //      Instantiate()s a new GameObject from that renderer's prefab -
        //      exactly what happens when a player equips an item and
        //      VisEquipment spawns the visual instance. A property block is
        //      a runtime-only overlay, never part of that copied state.
        //   2. The material named "flametal" turned out to be the pre-
        //      Ashlands legacy "Ancient Metal" look (orange _Color tint,
        //      confirmed via the real .mat file) - caught live in testing.
        //      The real current Flametal materials (FlametalArmor_Mat,
        //      Shield_Flametal_mat, the world ore's own Flametal_Mat) are
        //      all pure white _Color (1,1,1,1), meaning the real gray/
        //      metallic look comes entirely from their _MainTex, not a tint.
        // Clones FistGold's real chain material ("nordfistweapon_mat",
        // Valheim's own custom weapon shader - confirmed the same shader
        // FlametalArmor_Mat uses) via Material.Instantiate (preserves the
        // real shader, unlike assigning the source material directly, which
        // for some candidates like "flametal" itself uses Unity's Standard
        // shader instead and would lose whatever weather/snow-cover
        // integration the custom shader has that Standard doesn't).
        private static void ApplyChainTexture()
        {
            if (_clonedItemGameObject == null)
            {
                return;
            }

            try
            {
                var materialName = ChainTextureMaterialName;
                var sourceMaterial = Resources.FindObjectsOfTypeAll<Material>()
                    .FirstOrDefault(m => m.name == materialName);
                if (sourceMaterial == null)
                {
                    Jotunn.Logger.LogWarning(
                        $"Grapple Knuckles: could not find a real material named '{materialName}' to reuse for the chain texture; keeping the original.");
                    return;
                }

                // FIXED 2026-09-17: hardcoding "_MainTex" rendered flat white
                // for every material choice, not just Flametal. Confirmed via
                // decompile - Material.mainTexture only resolves through a
                // shader's [MainTexture]-flagged property (an SRP/URP-era
                // Unity feature Valheim's built-in-pipeline custom shaders
                // don't use) or literally "_MainTex"; if a shader has
                // neither, mainTexture silently returns null. We were then
                // writing that null texture onto the clone while "_Color"
                // (which DOES exist on these shaders) got forced to the
                // source's white tint - a textureless, pure-white material.
                // GetTexturePropertyNames() enumerates whatever texture
                // slots the shader actually declares, whatever they're
                // named, and this copies all of them (main map, normal map,
                // metallic/gloss map, etc.) the target shader also has,
                // instead of assuming "_MainTex" is the right one.
                var texturePropertyNames = sourceMaterial.GetTexturePropertyNames();
                Jotunn.Logger.LogInfo(
                    $"Grapple Knuckles: source material '{materialName}' uses shader '{sourceMaterial.shader.name}', " +
                    $"texture properties: [{string.Join(", ", texturePropertyNames)}].");

                foreach (var renderer in _clonedItemGameObject.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                        {
                            continue;
                        }

                        var clone = new Material(materials[i]);
                        clone.name = materials[i].name + "_" + materialName;

                        foreach (var propertyName in texturePropertyNames)
                        {
                            if (!clone.HasProperty(propertyName))
                            {
                                continue;
                            }

                            clone.SetTexture(propertyName, sourceMaterial.GetTexture(propertyName));
                            clone.SetTextureOffset(propertyName, sourceMaterial.GetTextureOffset(propertyName));
                            clone.SetTextureScale(propertyName, sourceMaterial.GetTextureScale(propertyName));
                        }

                        if (clone.HasProperty("_Color") && sourceMaterial.HasProperty("_Color"))
                        {
                            clone.SetColor("_Color", sourceMaterial.GetColor("_Color"));
                        }

                        materials[i] = clone;
                    }

                    renderer.sharedMaterials = materials;
                }

                Jotunn.Logger.LogInfo($"Grapple Knuckles: chain texture set to '{materialName}'.");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"Grapple Knuckles: chain texture failed, keeping original: {ex}");
            }
        }
    }
}
