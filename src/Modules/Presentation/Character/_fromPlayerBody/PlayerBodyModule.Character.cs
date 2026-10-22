using System;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// PlayerBodyModule part (split from PlayerBody/PlayerBodyModule.cs in Wave A): the character body and animation framework -
    /// character choice, first-person body/arms, equipment, pain sounds, weapon sway / ADS easing, animation tuning, model files, the
    /// ModAPI animation service. Target: Presentation (B05).
    /// </summary>
    public sealed partial class PlayerBodyModule
    {
        // ------------------------------------------------------------------ F6 (main cfg)
        internal enum Gender { Female, Male, Merchant, Bodyguard, Sprokka, Pistoleer, Mechanic, OrganicMechanic, Gungirl, Shoota }
        internal static ConfigEntry<Gender> Character;
        internal static ConfigEntry<bool> BodyFirstPerson, EmptyHandArms, FemaleArms, ReplaceDriver, EquipmentOnBody, PainSounds, FirstPersonSwayOn, SmoothAimDownSights;
        internal static ConfigEntry<bool> FullBodyOnlyOnFoot;
        internal static ConfigEntry<bool> FirstPersonArmsOnlyOnFoot;
        internal static bool BodyInCar { get { return BodyFirstPerson.Value && !FullBodyOnlyOnFoot.Value; } }
        internal static bool ArmsInCar { get { return EmptyHandArms.Value && !FirstPersonArmsOnlyOnFoot.Value; } }

        // ------------------------------------------------------------------ F7 (tools.cfg): tuning, offsets (the old hidden H() entries)
        internal static ConfigEntry<bool> MirrorBody, VerboseModApi, FootIK, WalkToStop;
        internal static ConfigEntry<string> ModelFile, TextureFile, ArmsTextureFile, AnimBundleFile;
        internal static ConfigEntry<string> IdleClip, RunClip, RifleClip, PistolClip, MeleeClip;
        internal static ConfigEntry<float> BodyBack, BodyBackDown, RunClipSpeed, WalkStride, RunFrom, AimPitchShare;
        internal static ConfigEntry<float> FirstPersonWeaponBack, FirstPersonChestLean;
        internal static ConfigEntry<float> FpCamX, FpCamY, FpCamZ;
        internal static ConfigEntry<float> JumpClipStart, StrikeWindup;
        // (the 1.7.x crouch entries CrouchRifleLegs / CrouchArmsRestFrame / CrouchArmsPhase / CrouchPistolLean / CrouchPistolArms, unused since 2.0, are not ported)
        internal static ConfigEntry<float> UpperPhase, ClipSprintSpeed, SprintFrom, TurnClipAngle, TurnStartRate, DirBlendSpeed, PumpClipSpeed;
        internal static ConfigEntry<float> RunLegsIn, RunLegsTurn;
        internal static ConfigEntry<float> HipsDrift, ClipWalkSpeed, ClipRunSpeed, ClipCrouchSpeed, ThighSwing, KneeBend, ArmSwing, HipBob, CrouchDrop;

        internal static bool Female { get { return Character != null && Character.Value == Gender.Female; } }
        internal static string BodyModelFile { get { return Female ? ModelFile.Value : "Models/Player_male.glb"; } }
        internal static string BodyTextureFile { get { return Female ? TextureFile.Value : "Models/Player_male.png"; } }

        static readonly AnimationService _service = new AnimationService();

        // ================================================================== config
        /// <summary>Binds [PlayerBody] Character and canonicalises the legacy Pistoleer/Mechanic choices.</summary>
        partial void BindCharacterChoice(ModuleConfig cfg)
        {
            Character = cfg.Player("", "Character", Gender.Male, "Your character's body. Male, Female, Merchant, Bodyguard, Sprokka, OrganicMechanic, Gungirl and Shoota use Wild Wasteland player animations, in singleplayer. Legacy Pistoleer/Mechanic choices map to Bodyguard/Merchant.");
            Character.Value = (Gender)CharacterPortraits.Canonical((int)Character.Value);
        }

        /// <summary>Binds the [PlayerBody] body, arms, driver, equipment, pain-sound, sway and ADS toggles.</summary>
        partial void BindCharacterBody(ModuleConfig cfg)
        {
            BodyFirstPerson = cfg.Player("", "BodyFirstPerson", true, "First person: see the body (legs and torso when you look down, the shadow, the body in the driver's seat). Off = only the first-person arms.");
            FullBodyOnlyOnFoot = cfg.Player("", "FullBodyOnlyOnFoot", false, "Full Body: show the first-person body only on foot, keeping the driver's first-person view clear.");
            EmptyHandArms = cfg.Player("", "FirstPersonArms", true, "First person with nothing in hand: show the body's own arms (idle/walk swing, hands on the wheel in a car). Off = no body arms in first person; the game's weapon and item arms always show.");
            FirstPersonArmsOnlyOnFoot = cfg.Player("", "FirstPersonArmsOnlyOnFoot", false, "First Person Arms: show the empty-handed arms only on foot, hiding the hands on the wheel while driving.");
            FemaleArms = cfg.Player("", "FirstPersonArmSkin", true, "The game's first-person arms and kick leg wear your character's skin (Female; Male keeps the game's own).");
            ReplaceDriver = cfg.Player("", "ReplaceDriver", true, "Your character replaces the game's man in the driver's seat (posed from the seat, guns and throws from the seat) and on the TAB screen picture.");
            EquipmentOnBody = cfg.Player("", "EquipmentOnBody", true, "Show the carried equipment on the body: backpack, long guns on the back, sidearms, blades, binoculars, flashlight (with its glow).");
            PainSounds = cfg.Player("", "FemalePainSounds", true, "Female character: four female hurt sounds and a female death sound.");
            FirstPersonSwayOn = cfg.Player("", "SmoothWeaponSway", true, "First person: frame-rate independent weapon / item sway (the game's own sway stutters with uneven frame times).");
            SmoothAimDownSights = cfg.Player("", "SmoothAimDownSights", true, "First person: the gun eases between hip and sights (right mouse button) instead of jumping. Time in F7.");
        }

        /// <summary>Binds [PlayerBody / First person] (tools.cfg): view offsets and how the body sits under the first-person camera.</summary>
        partial void BindCharacterFirstPersonTools(ModuleConfig cfg)
        {
            const string Fp = "First person";
            FpCamX = Slider(cfg, Fp, "FirstPersonBodyX"); FpCamY = Slider(cfg, Fp, "FirstPersonBodyY"); FpCamZ = Slider(cfg, Fp, "FirstPersonBodyZ");
            BodyBack = cfg.Tool(Fp, "FirstPersonBodyBack", 0.08f, "First person on foot: the head sits this far (m) behind the camera.");
            BodyBackDown = cfg.Tool(Fp, "BodyBackDown", 0.08f, "First person: extra distance behind the camera when looking straight down, m (blended in with the pitch).");
            FirstPersonWeaponBack = cfg.Tool(Fp, "FirstPersonWeaponBack", 0.08f, "First person with a weapon drawn: the body moves this much (m) further back.");
            FirstPersonChestLean = cfg.Tool(Fp, "FirstPersonChestLean", 20f, "First person with a weapon drawn: the chest bends back by this many degrees.");
        }

        /// <summary>First-person offset slider (m, -0.3..0.3; an out-of-range value is clamped). Used by the First person and Car seat sections.</summary>
        static ConfigEntry<float> Slider(ModuleConfig cfg, string sub, string key)
        {
            var e = cfg.Tool(sub, key, 0f, "First-person view offset (m, view right / up / forward): the body moves the opposite way under the camera.", new AcceptableValueRange<float>(-0.3f, 0.3f));
            if (e.Value < -0.3f || e.Value > 0.3f) e.Value = Mathf.Clamp(e.Value, -0.3f, 0.3f);
            return e;
        }

        /// <summary>Binds the animation tuning, fallback clips, model files and ModAPI sections (tools.cfg), then the live Character reaction.</summary>
        partial void BindCharacterTools(ModuleConfig cfg)
        {
            const string Anim = "Animation", Leg = "Animation (fallback clips)", Mdl = "Model", Api = "ModAPI";
            JumpClipStart = cfg.Tool(Anim, "JumpClipStart", 0.2f, "Jump / RifleJump / PistolJump clips start this far in (share of the clip): Mixamo jumps crouch first, the game's jump leaves the ground at once.");
            StrikeWindup = cfg.Tool(Anim, "StrikeWindup", 0.08f, "Melee / bare hands: seconds the wind-up takes before the blow lands (the game hits the moment the swing starts).");
            HipsDrift = cfg.Tool(Anim, "HipsDrift", 0.25f, "Bundle clips: how far (m) the hips may move away from the player sideways/forward (room for the idle sway).");
            ClipWalkSpeed = cfg.Tool(Anim, "BundleWalkSpeed", 1.4f, "Ground speed (m/s) at which the bundle's walk clips play at normal speed.");
            ClipRunSpeed = cfg.Tool(Anim, "BundleRunSpeed", 3.8f, "Ground speed (m/s) at which the bundle's run clips play at normal speed.");
            ClipCrouchSpeed = cfg.Tool(Anim, "BundleCrouchSpeed", 1.0f, "Ground speed (m/s) at which the bundle's crouch walk plays at normal speed.");
            ClipSprintSpeed = cfg.Tool(Anim, "BundleSprintSpeed", 5.5f, "Ground speed (m/s) at which the rifle pack's sprint clips play at normal speed (when the clip has no root motion of its own).");
            RunFrom = cfg.Tool(Anim, "RunFrom", 3.2f, "From this speed (m/s) the run clips take over from the walk.");
            SprintFrom = cfg.Tool(Anim, "SprintFrom", 4.3f, "Ground speed (m/s) above which the rifle pack's sprint clips replace its run clips (the game runs at 5).");
            UpperPhase = cfg.Tool(Anim, "UpperPhase", 0f, "Walking / running upper-body clips play in step with the rifle legs; this shifts them (0..1 of a stride, 0.5 = the other foot).");
            TurnClipAngle = cfg.Tool(Anim, "TurnClipAngle", 70f, "0 = no turn-in-place clips; any other value = on (the clips start with the first degree, see TurnStartRate).");
            TurnStartRate = cfg.Tool(Anim, "TurnStartRate", 8f, "Standing still: turning faster than this (degrees per second) steps the feet with the turn-in-place clips.");
            PumpClipSpeed = cfg.Tool(Anim, "PumpClipSpeed", 2f, "ShotgunPump (cocking the pump shotgun / bolt rifle) plays this fast (x normal).");
            DirBlendSpeed = cfg.Tool(Anim, "DirBlendSpeed", 360f, "How fast (degrees per second) the legs' walking direction turns when the movement keys change.");
            FootIK = cfg.Tool(Anim, "FootIK", true, "Unity's humanoid foot IK on the bundle's idle and walking clips (never on run / sprint / jump clips).");
            RunLegsIn = cfg.Tool(Anim, "RunLegsIn", 10f, "Running (bundle animations): each thigh turned this many degrees toward the middle, the feet kept flat.");
            WalkToStop = cfg.Tool(Anim, "WalkToStop", true, "Relaxed walking forward and stopping: the RifleWalkToStop clip's last step settles the feet.");
            RunLegsTurn = cfg.Tool(Anim, "RunLegsTurn", 75f, "Relaxed running (not aiming): the legs turn up to this many degrees toward the way she runs; the chest keeps facing the camera.");
            CrouchDrop = cfg.Tool(Anim, "CrouchDrop", 0.42f, "How far the hips go down when crouched, m.");
            AimPitchShare = cfg.Tool(Anim, "AimPitch", 0.9f, "Share of the camera pitch the spine follows (aiming up/down).");

            IdleClip = cfg.Tool(Leg, "IdleClip", "merchant_idle", "Without the animation bundle: humanoid clip for standing (the game has: merchant_idle, enemy_1_idle, zombie_idle).");
            RunClip = cfg.Tool(Leg, "RunClip", "enemy_1_run", "Without the bundle: humanoid clip for running.");
            RifleClip = cfg.Tool(Leg, "RifleClip", "enemy_machinegun", "Without the bundle: upper-body clip with a rifle / SMG / shotgun / crossbow drawn.");
            PistolClip = cfg.Tool(Leg, "PistolClip", "sprokka_shoot", "Without the bundle: upper-body clip with a pistol / revolver drawn.");
            MeleeClip = cfg.Tool(Leg, "MeleeClip", "enemy_1_attack", "Upper-body clip played once per melee swing (when the bundle has no Melee clip).");
            RunClipSpeed = cfg.Tool(Leg, "RunClipSpeed", 4.5f, "Without the bundle: ground speed (m/s) at which the run clip plays at normal speed.");
            WalkStride = cfg.Tool(Leg, "WalkStride", 1.25f, "Procedural walk: length of one full walk cycle (two steps), m.");
            ThighSwing = cfg.Tool(Leg, "ThighSwing", 26f, "Procedural walk: thigh swing, degrees.");
            KneeBend = cfg.Tool(Leg, "KneeBend", 38f, "Procedural walk: knee bend in the swing phase, degrees.");
            ArmSwing = cfg.Tool(Leg, "ArmSwing", 16f, "Procedural walk: arm swing without a weapon, degrees.");
            HipBob = cfg.Tool(Leg, "HipBob", 0.025f, "Procedural walk: hip bob, m.");
            MirrorBody = cfg.Tool(Leg, "RightHanded", true, "Raider-clip mode only: mirror the body so the gun is in the right hand.");

            ModelFile = cfg.Tool(Mdl, "Model", "Models/Player_female.glb", "Female body model (.glb/.gltf) relative to plugins/WildWasteland/PlayerBody, rigged to Flexa's skeleton (22 mixamorig bones). Restart the module to reload.");
            TextureFile = cfg.Tool(Mdl, "Texture", "Models/Player_female.png", "Female body texture relative to the module folder.");
            ArmsTextureFile = cfg.Tool(Mdl, "ArmsTexture", "Models/Player2_female_arms.png", "Female replacement for the game's Player2 texture atlas, used only on the first-person arms and kick leg.");
            AnimBundleFile = cfg.Tool(Mdl, "AnimationBundle", "Models/apocaplayer_anims.bundle", "AssetBundle with the humanoid (Mixamo) clips; loaded once per game start.");


            VerboseModApi = cfg.Tool(Api, "VerboseModApi", false, "Log every decision of every ModAPI character (very chatty; needs [Debug] VerboseLog).");

            // live reactions (the handlers only act while the module runs)
            Character.SettingChanged += (s, e) => { var choice = (Gender)CharacterPortraits.Canonical((int)Character.Value); if (choice != Character.Value) { Character.Value = choice; return; } if (Running) BodyRunner.CharacterChanged(); };
        }

        // ================================================================== lifecycle
        /// <summary>Init: the weapon pose table, the aim lift and the female pain sounds (data, was Plugin.Awake), then the ADS / pain-sound patches.</summary>
        partial void InitCharacter()
        {
            GunPose.Bind();
            AimLift.Load(DataDir);
            Scheduler.StartCoroutine(FemalePain.LoadClips());
            AimTransition.Patch(this);
            FemalePain.Patch(this);
        }

        /// <summary>Init: the ModAPI characters job (the old ModApiRunner.LateUpdate, after "Body late").</summary>
        partial void InitCharacterJobs()
        {
            Scheduler.EveryFrame(this, "ModAPI characters", BodyRunner.ModApiTick, Phase.LateUpdate);
        }

        /// <summary>Init (last): publishes the animation service (Core.Services.Animation).</summary>
        partial void InitCharacterService()
        {
            Services.Animation = _service;
        }

        /// <summary>Shutdown (first): withdraws the animation service.</summary>
        partial void ShutdownCharacterService()
        {
            Services.Animation = null;
        }

        /// <summary>Shutdown: writes edited weapon poses and aim lift.</summary>
        partial void ShutdownCharacterSave()
        {
            try { GunPose.Flush(); AimLift.SaveIfDirty(); } catch (Exception e) { Warn("saving poses: " + e.Message); }
        }

        /// <summary>Shutdown: restores the ADS tweens and drops the ModAPI characters and the service's cached handles.</summary>
        partial void ShutdownCharacter()
        {
            try { AimTransition.RestoreAll(); } catch (Exception e) { Warn("aim transition: " + e.Message); }
            try { ModAPI.OnSceneLoaded(); } catch (Exception e) { Warn("ModAPI: " + e.Message); }
            _service.Clear();
        }

        /// <summary>Scene load: ModAPI characters and service handles dropped, ADS tween records swept.</summary>
        partial void SceneCharacter()
        {
            ModAPI.OnSceneLoaded();
            _service.Clear();
            AimTransition.Sweep();
        }
    }
}
