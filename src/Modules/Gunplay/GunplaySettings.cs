using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Modules.Gunplay;

namespace WildWasteland.Modules.Gunplay
{
    /// <summary>
    /// Every config entry of the Gunplay module (README 3.3 "&lt;Id&gt;Settings.cs"). Bound by <see cref="GunplayModule.BindConfig"/> in the
    /// former Guns display order: F6 settings.cfg <c>[Gunplay]</c> feature toggles, <c>[Gunplay / Projectiles|Ranges|Ammo]</c> values, then the
    /// F7 tools.cfg <c>[Gunplay / Tracers|Ballistics|NPC aim|Melee|Ammo]</c> tuning. D12 fresh start: no migration from the old <c>[Guns*]</c> sections.
    /// The ammo entries live in <see cref="AmmoSettings"/> (bound here at their original positions).
    /// </summary>
    internal static class GunplaySettings
    {
        // ---------------- F6 (settings.cfg) : feature toggles, in feature order ----------------
        internal static ConfigEntry<bool> TracersEnabled;      // [Gunplay] Projectiles
        internal static ConfigEntry<bool> PerGunMagazines;
        internal static ConfigEntry<bool> OrbsHitWalls;        // [Gunplay] MonsterProjectilesHitWalls
        internal static ConfigEntry<bool> MeleeOnWheels;
        internal static ConfigEntry<bool> NpcAim;              // gates NpcAimAtBody + LeadTargets
        internal static ConfigEntry<string> Enemies;
        internal static ConfigEntry<bool> IronsightWheelZoom, ToggleIronsights;
        internal static ConfigEntry<bool> SmoothWeaponSwitch;
        // ---------------- F6 values ----------------
        internal static ConfigEntry<float> BulletSpeed, BoltSpeed, HeadshotMultiplier;
        internal static ConfigEntry<bool> TracersDraw, PlayerGunTracers, VehicleDamage, WheelPopOff, LanceIgnoresOwnCar;
        internal static ConfigEntry<float> PistolRange, SmgRange, RifleRange, SniperRange, ShotgunRange, CrossbowRange;
        // ---------------- F7 (tools.cfg) ----------------
        internal static ConfigEntry<Color> TracerColor, BoltColor;
        internal static ConfigEntry<float> TracerGlow, TracerLength, TracerWidth, BoltLength, BoltWidth, MetalSparksScale;
        internal static ConfigEntry<int> MaxTracers, ShotgunPellets;
        internal static ConfigEntry<bool> ImpactEffects, MetalSparks, PlayerTracers, NpcAimAtBody, LeadTargets;
        internal static ConfigEntry<float> FullDamageUntil, NpcHitRadius, NpcShotgunDamage, ShotgunPelletSpread, PlayerBodyRadius, PlayerHeadRadius,
            VehicleDamagePer1, WheelDamageMultiplier, LeadAccuracy, LeadError, MaxLeadTime;
        internal static ConfigEntry<string> StabSound, StabWeapons;
        internal static ConfigEntry<float> PlayerAccuracy, NpcAccuracy;

        /// <summary>Projectiles on while the module runs (IGunsApi.ProjectilesEnabled, HUD ExternalReporter).</summary>
        internal static bool ProjectilesEnabled { get { return GunplayModule.Running && TracersEnabled != null && TracersEnabled.Value; } }
        /// <summary>Fitted-wheel melee owned by this module (Patrols' fallback stands down).</summary>
        internal static bool FittedWheelMeleeActive { get { return GunplayModule.Running && MeleeOnWheels != null && MeleeOnWheels.Value; } }
        /// <summary>[NPC aim] toggle x hidden NpcAimAtBody.</summary>
        internal static bool AimAtBody { get { return NpcAim.Value && NpcAimAtBody.Value; } }
        /// <summary>[NPC aim] toggle x hidden LeadTargets.</summary>
        internal static bool Lead { get { return Enemies.Value != "Stormtroopers" && NpcAim.Value && LeadTargets.Value; } }
        internal static float EnemyMissChance { get { return Enemies.Value == "Stormtroopers" ? 0.5f : Enemies.Value == "Survivors" ? 0.25f : 0f; } }
        /// <summary>Central accuracy source for pellet spread (0..100); future player skills adjust the base value here.</summary>
        internal static float Accuracy(bool player) { return Mathf.Clamp((player ? PlayerAccuracy : NpcAccuracy).Value, 0f, 100f); }

        /// <summary>Binds every entry in display order (F6 toggles, F6 values, F7 tuning) and hooks the live reactions.</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            // feature toggles first (Apocasetter display order)
            TracersEnabled = cfg.Player("", "Projectiles", true,
                "Real bullets for NPC and player guns: flight time, falloff with distance, headshots and vehicle part damage. Off = the game's own instant hits; the NPC module controls NPC decisions separately.");
            PerGunMagazines = cfg.Player("", "PerGunMagazines", true,
                "Every physical gun keeps its own magazine and chamber state (saved with the game). Off = the game's own shared ammo per gun type; magazines are not saved while off.");
            AmmoSettings.Enabled = cfg.Player("", "Unloading", true,
                "Hold Reload to unload the current gun. Short presses reload on release. Per-gun magazines remain tracked when this is off.");
            AmmoSettings.DropBackpackOverflow = cfg.Player("", "BackpackOverflow", true,
                "Drop excess ammo when switching to a smaller backpack or removing it. Failed drops retain the ammo and retry.");
            AmmoSettings.DropArmorOverflow = cfg.Player("", "ArmorOverflow", true,
                "Drop one armor plate for each full 50 points of excess armor when switching to lower-capacity body armor. Excess below 50 produces no plate. Failed drops retain armor and retry.");
            OrbsHitWalls = cfg.Player("", "MonsterProjectilesHitWalls", true,
                "Projectiles of monsters without guns (Arachnid, Teacher and the like) are stopped by walls, terrain and vehicles; car glass, grids, doors and plates let them through as often as a pistol bullet. Off = the game's own projectiles, which fly through everything. Needs Projectiles.");
            MeleeOnWheels = cfg.Player("", "MeleeOnWheels", true,
                "Your melee weapons hit wheels hard (x [Gunplay / Ballistics] WheelDamageMultiplier), also fitted wheels the game's own swing passes through; a wheel brought to 0 pops off (WheelPopOff). Off = the game's own melee damage on wheels (the Patrols module's simpler fitted-wheel melee takes over).");
            NpcAim = cfg.Player("", "NpcAim", true,
                "NPC gunmen aim at the centre of your body and lead moving targets (tuning in F7 > Weapons & HUD > NPC aim). Off = the game's own aim point (your head) and no lead. Needs Projectiles.");
            Enemies = cfg.Player("", "Enemies", "Survivors",
                "Enemy aim: Mercs retain current accuracy; Survivors aim wide on an extra 25% of shots; Stormtroopers aim wide on an extra 50% and never lead moving targets. Includes gunmen, bosses, vehicle crews and mutant orbs. Gun accuracy needs Projectiles.",
                new AcceptableValueList<string>("Stormtroopers", "Survivors", "Mercs"));
            IronsightWheelZoom = cfg.Player("", "IronsightWheelZoom", true,
                "First person: while aiming down sights, scroll up to zoom in smoothly and down to zoom out. Up to 3x additional zoom, including scopes. Leaving sights resets the zoom.");
            ToggleIronsights = cfg.Player("", "ToggleIronsights", false,
                "Press Aim Down Sights once to aim and again to stop aiming. Off: hold the button as in the original game.");
            SmoothWeaponSwitch = cfg.Player("", "SmoothWeaponSwitch", true,
                "First person: lower the current weapon and raise the next weapon smoothly when drawing, holstering or switching slots.");

            BulletSpeed = cfg.Player("Projectiles", "BulletSpeed", 250f, "Bullet speed, m/s.", new AcceptableValueRange<float>(20f, 2000f));
            BoltSpeed = cfg.Player("Projectiles", "BoltSpeed", 60f, "Crossbow bolt speed, m/s.", new AcceptableValueRange<float>(10f, 500f));
            HeadshotMultiplier = cfg.Player("Projectiles", "HeadshotMultiplier", 1.5f,
                "Damage of a bullet in the head - yours on NPCs, NPCs' on you and on each other (replaces the game's own x2 on NPC heads).", new AcceptableValueRange<float>(0.5f, 5f));
            TracersDraw = cfg.Player("Projectiles", "Tracers", true, "Draw the glowing trail of every bullet in flight (crossbow bolts are always drawn).");
            PlayerGunTracers = cfg.Player("Projectiles", "PlayerGunTracers", true, "Draw tracers for your own shots too.");
            VehicleDamage = cfg.Player("Projectiles", "VehicleDamage", true, "Bullets damage the vehicle parts they hit (20 bullet damage = 1 % condition; wheels much more). Off = the game's own rule.");
            WheelPopOff = cfg.Player("Projectiles", "WheelPopOff", true, "A wheel shot or slashed to 0 condition jumps off its car.");
            LanceIgnoresOwnCar = cfg.Player("Projectiles", "LanceIgnoresOwnCar", true, "A blast lance thrown from the driver's seat flies out of your own car instead of blowing up on it.");

            PistolRange = cfg.Player("Ranges", "PistolRange", 60f, "Pistols/revolvers: damage falls off linearly with distance - half at 50 % of this range, the bullet is gone at 100 %. Metres.", new AcceptableValueRange<float>(5f, 1000f));
            SmgRange = cfg.Player("Ranges", "SmgRange", 70f, "SMGs, falloff range in metres (half damage at half range).", new AcceptableValueRange<float>(5f, 1000f));
            RifleRange = cfg.Player("Ranges", "RifleRange", 120f, "Automatic rifles and machine guns, falloff range in metres.", new AcceptableValueRange<float>(5f, 1000f));
            SniperRange = cfg.Player("Ranges", "SniperRange", 250f, "Sniper/scoped rifles, falloff range in metres.", new AcceptableValueRange<float>(5f, 1000f));
            ShotgunRange = cfg.Player("Ranges", "ShotgunRange", 35f, "Shotguns, falloff range in metres.", new AcceptableValueRange<float>(5f, 1000f));
            CrossbowRange = cfg.Player("Ranges", "CrossbowRange", 90f, "Crossbows, falloff range in metres.", new AcceptableValueRange<float>(5f, 1000f));

            AmmoSettings.HoldSeconds = cfg.Player("Ammo", "HoldSeconds", 0.35f,
                "Seconds to hold Reload before unloading; a shorter press reloads on release.", new AcceptableValueRange<float>(0.1f, 2f));
            AmmoSettings.FallbackKey = cfg.Hotkey("Gunplay: unload fallback key", Key.R,
                "Gunplay: key polled for hold-to-unload only if the game's Reload input axis cannot be read (normally the game's own Reload binding is used).");

            // ---------------- F7 tools.cfg ----------------
            TracerColor = cfg.Tool("Tracers", "TracerColor", new Color(1f, 0.78f, 0.35f, 1f), "Bullet tracer colour (RGBA hex). Unlit: same brightness day and night.");
            TracerGlow = cfg.Tool("Tracers", "Glow", 1.5f, "Brightness multiplier of tracers and bolts.", new AcceptableValueRange<float>(0f, 8f));
            TracerLength = cfg.Tool("Tracers", "TracerLength", 4f, "Tracer line length, m.", new AcceptableValueRange<float>(0.1f, 30f));
            TracerWidth = cfg.Tool("Tracers", "TracerWidth", 0.1f, "Tracer line width, m.", new AcceptableValueRange<float>(0.005f, 0.5f));
            BoltColor = cfg.Tool("Tracers", "BoltColor", new Color(0.85f, 0.72f, 0.5f, 1f), "Crossbow bolt colour (RGBA hex).");
            BoltLength = cfg.Tool("Tracers", "BoltLength", 0.8f, "Bolt line length, m.", new AcceptableValueRange<float>(0.1f, 5f));
            BoltWidth = cfg.Tool("Tracers", "BoltWidth", 0.04f, "Bolt line width, m.", new AcceptableValueRange<float>(0.005f, 0.5f));
            MaxTracers = cfg.Tool("Tracers", "MaxTracers", 300, "Most bullets in flight at once; shots above this hit instantly (vanilla style) instead.", new AcceptableValueRange<int>(16, 2000));
            ImpactEffects = cfg.Tool("Tracers", "ImpactEffects", true,
                "NPC bullets that hit the world show the same impact (sparks, sound) your own hits do; the game showed nothing for their misses.");
            MetalSparks = cfg.Tool("Tracers", "MetalSparks", true,
                "Extra sparks, smoke and a bullet mark on anything that is part of a car - attached parts, the body and frame, loose parts - except wheels (any bullet), using the game's MetalImpact effect.");
            MetalSparksScale = cfg.Tool("Tracers", "MetalSparksScale", 0.1f, "Size of the metal sparks effect (1 = the prefab's own, demo-scene size).", new AcceptableValueRange<float>(0.05f, 4f));

            PlayerTracers = cfg.Tool("Ballistics", "PlayerGuns", true,
                "Your own guns follow the same rules: visible bullets with travel time, the same range falloff and vehicle-part hits.");
            FullDamageUntil = cfg.Tool("Ballistics", "FullDamageUntil", 50f,
                "Every gun (NPC and yours) does full damage until this % of its range, then the damage falls off to 0 at the range. 0 = falloff from the muzzle (half damage at 50 %).",
                new AcceptableValueRange<float>(0f, 99f));
            NpcHitRadius = cfg.Tool("Ballistics", "NpcHitRadius", 0.05f,
                "Thickness of NPC bullets, m, added to the target's hitbox (your body/head shapes, other targets' colliders). 0 = hairline. Your own bullets are always a hairline.",
                new AcceptableValueRange<float>(0f, 0.5f));
            NpcShotgunDamage = cfg.Tool("Ballistics", "NpcShotgunDamage", 1.7f,
                "Damage multiplier for NPC shotgun pellets (the game's shotgunners do 5-8 per ray x 4 rays per blast, a third of a rifle burst).",
                new AcceptableValueRange<float>(0f, 5f));
            ShotgunPellets = cfg.Tool("Ballistics", "ShotgunPellets", 3, "Pellets per vanilla shotgun ray (a vanilla blast is 4 rays; 3 = 12 pellets). The blast's damage is split between them.", new AcceptableValueRange<int>(1, 8));
            ShotgunPelletSpread = cfg.Tool("Ballistics", "ShotgunPelletSpread", 1f, "Extra spread of each pellet, degrees.", new AcceptableValueRange<float>(0f, 15f));
            PlayerAccuracy = cfg.Tool("Ballistics", "PlayerAccuracy", 100f,
                "Player shotgun pellet distribution, %: 0 favours the rim, 50 is uniform across the circle, 100 favours the centre. Full spread remains possible; applies to the next shot.", new AcceptableValueRange<float>(0f, 100f));
            NpcAccuracy = cfg.Tool("Ballistics", "NpcAccuracy", 50f,
                "NPC shotgun pellet distribution, %: 0 favours the rim, 50 is uniform across the circle, 100 favours the centre. Applies to extra pellet spread; NPC aim and lead are separate.", new AcceptableValueRange<float>(0f, 100f));
            PlayerBodyRadius = cfg.Tool("Ballistics", "PlayerBodyRadius", 0.22f,
                "Your body as NPC bullets see it: a capsule from your feet to your neck with this radius, m (the game's own collider is only 0.17-0.20).",
                new AcceptableValueRange<float>(0.05f, 0.6f));
            PlayerHeadRadius = cfg.Tool("Ballistics", "PlayerHeadRadius", 0.14f,
                "Your head as NPC bullets see it: a sphere of this radius at the top of your body, m.", new AcceptableValueRange<float>(0.05f, 0.4f));
            VehicleDamagePer1 = cfg.Tool("Ballistics", "VehicleDamagePer1", 20f, "Bullet damage that takes 1 % off a vehicle part condition.", new AcceptableValueRange<float>(1f, 1000f));
            WheelDamageMultiplier = cfg.Tool("Ballistics", "WheelDamageMultiplier", 5f,
                "Wheels (and tyres) lose this many times more condition per bullet - and per blow of your melee weapons - than other vehicle parts, so shooting or slashing the wheels is the way to stop a car.",
                new AcceptableValueRange<float>(0f, 100f));

            NpcAimAtBody = cfg.Tool("NPC aim", "NpcAimAtBody", true,
                "NPCs aim at the centre of your body. Off = the game's own aim point, your head, which with the game's aim jitter sends many shots over your head. (Also needs F6 [Gunplay] NpcAim.)");
            LeadTargets = cfg.Tool("NPC aim", "LeadTargets", true,
                "NPCs aim where a moving target will be when the bullet arrives (the game's hitscan never had to). Changing direction still beats them. (Also needs F6 [Gunplay] NpcAim.)");
            LeadAccuracy = cfg.Tool("NPC aim", "LeadAccuracy", 0.75f,
                "How much of the ideal lead NPCs apply: 1 = perfect prediction, 0 = none. Each NPC rolls a personal skill between half of this and all of it.",
                new AcceptableValueRange<float>(0f, 1f));
            LeadError = cfg.Tool("NPC aim", "LeadError", 30f, "Random error on the NPC's estimate of your speed, +/- %, per shot.", new AcceptableValueRange<float>(0f, 100f));
            MaxLeadTime = cfg.Tool("NPC aim", "MaxLeadTime", 1.5f, "Longest flight time NPCs lead for, s (bolts at long range).", new AcceptableValueRange<float>(0f, 5f));

            StabWeapons = cfg.Tool("Melee", "StabWeapons", "knife,shiv,machete",
                "Melee weapons (name contains one of these) that use StabSound when hitting living actors or corpses instead of the game's knife hit. Every melee hit on a corpse shows blood.");
            StabSound = cfg.Tool("Melee", "StabSound", "Sounds/knifestab.wav",
                "The stab sound for blades on living actors and corpses (WAV, relative to BepInEx\\plugins\\WildWasteland\\Gunplay\\). Missing file: the game's knife hit.");

            AmmoSettings.PhaseTimeout = cfg.Tool("Ammo", "AnimationTimeout", 4f,
                "Seconds to wait for an unload animation phase before completing it.", new AcceptableValueRange<float>(1f, 10f));

            TracersEnabled.SettingChanged += OnProjectilesChanged;
            PerGunMagazines.SettingChanged += OnPerGunMagazinesChanged;
        }

        /// <summary>[Gunplay] Projectiles changed: the HUD's vanilla observer follows at once.</summary>
        static void OnProjectilesChanged(object sender, EventArgs e) { GunplayModule.SyncHud(); }

        /// <summary>[Gunplay] PerGunMagazines changed at runtime: start from a fresh ledger.</summary>
        static void OnPerGunMagazinesChanged(object sender, EventArgs e) { if (GunplayModule.Running) PerGunAmmo.ToggleChanged(); }
    }
}
