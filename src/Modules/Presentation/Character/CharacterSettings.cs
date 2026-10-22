using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>The character choices of the player (the enum names are the stored config values; Pistoleer/Mechanic are legacy aliases).</summary>
    internal enum CharacterChoice { Female, Male, Merchant, Bodyguard, Sprokka, Pistoleer, Mechanic, OrganicMechanic, Gungirl, Shoota }

    /// <summary>
    /// Every config entry of the Presentation character part (B05): the player's character and first-person body, animation tuning and
    /// model files, the NPC animation/sound switches, the dog animation switches and Women of Wasteland. Bound by
    /// <see cref="PresentationModule.BindConfig"/> into [Presentation] / [Presentation / &lt;Sub&gt;] (D12: no migration from the old
    /// [PlayerBody ...] / [NPC ...] sections). Bind order = display order.
    /// </summary>
    internal static class CharacterSettings
    {
        // ------------------------------------------------------------------ settings.cfg [Presentation]: the player's body
        internal static ConfigEntry<CharacterChoice> Character;
        internal static ConfigEntry<bool> BodyFirstPerson, FullBodyOnlyOnFoot, EmptyHandArms, FirstPersonArmsOnlyOnFoot, FemaleArms, ReplaceDriver, EquipmentOnBody, PainSounds, FirstPersonSwayOn, SmoothAimDownSights;

        // ------------------------------------------------------------------ tools.cfg [Presentation / First person|Animation|Animation (fallback clips)|Model]
        internal static ConfigEntry<bool> MirrorBody, FootIK, WalkToStop;
        internal static ConfigEntry<string> ModelFile, TextureFile, ArmsTextureFile, AnimBundleFile;
        internal static ConfigEntry<string> IdleClip, RunClip, RifleClip, PistolClip, MeleeClip;
        internal static ConfigEntry<float> BodyBack, BodyBackDown, RunClipSpeed, WalkStride, RunFrom, AimPitchShare;
        internal static ConfigEntry<float> FirstPersonWeaponBack, FirstPersonChestLean;
        internal static ConfigEntry<float> FpCamX, FpCamY, FpCamZ;
        internal static ConfigEntry<float> JumpClipStart, StrikeWindup;
        internal static ConfigEntry<float> UpperPhase, ClipSprintSpeed, SprintFrom, TurnClipAngle, TurnStartRate, DirBlendSpeed, PumpClipSpeed;
        internal static ConfigEntry<float> RunLegsIn, RunLegsTurn;
        internal static ConfigEntry<float> HipsDrift, ClipWalkSpeed, ClipRunSpeed, ClipCrouchSpeed, ThighSwing, KneeBend, ArmSwing, HipBob, CrouchDrop;

        // ------------------------------------------------------------------ NPC characters (ex [NPC] / [NPC / Brain|NpcAim|Behaviour])
        internal static ConfigEntry<bool> DogAnimationsEnabled, DogsLieDown, ApocaplayerClips, AimPose, AnimationLod;
        internal static ConfigEntry<float> ReloadVolume, ReloadSoundRange, DogLieFrontLift;

        // ------------------------------------------------------------------ Women of Wasteland (ex [NPC / Women of Wasteland|Gungirl|Shoota])
        internal static ConfigEntry<bool> EnableFemaleNpc, FemaleCrew, GungirlVoiceMatch;
        internal static ConfigEntry<int> FemalePopulation;
        internal static ConfigEntry<float> GungirlVoiceVolume, GungirlVoiceIntervalMin, GungirlVoiceIntervalMax;
        internal static ConfigEntry<string> GungirlModel, GungirlTexture, GungirlVoice, GungirlHideParts, ShootaModel, ShootaTexture, ShootaHideParts;
        internal static ConfigEntry<Key> FemaleSpawnKey;

        /// <summary>Section of the female appearance page ("Gungirl", "Shoota", "Women of Wasteland").</summary>
        internal const string WomenSection = "Women of Wasteland";

        /// <summary>True when the chosen character is the Female body.</summary>
        internal static bool Female { get { return Character != null && Character.Value == CharacterChoice.Female; } }
        /// <summary>First-person body shown in the driver's seat (BodyFirstPerson and not FullBodyOnlyOnFoot).</summary>
        internal static bool BodyInCar { get { return BodyFirstPerson.Value && !FullBodyOnlyOnFoot.Value; } }
        /// <summary>Empty-handed first-person arms shown on the wheel (FirstPersonArms and not FirstPersonArmsOnlyOnFoot).</summary>
        internal static bool ArmsInCar { get { return EmptyHandArms.Value && !FirstPersonArmsOnlyOnFoot.Value; } }
        /// <summary>Body model file of the chosen character (Female: the Model entry).</summary>
        internal static string BodyModelFile { get { return Female ? ModelFile.Value : "Models/Player_male.glb"; } }
        /// <summary>Body texture file of the chosen character (Female: the Texture entry).</summary>
        internal static string BodyTextureFile { get { return Female ? TextureFile.Value : "Models/Player_male.png"; } }

        /// <summary>True once <see cref="Bind"/> ran (bind is idempotent: one set of entries per game start).</summary>
        internal static bool Bound { get { return Character != null; } }

        /// <summary>Binds every entry in display order: player body (F6), NPC/dog switches, Women of Wasteland, then the tools.cfg tuning.</summary>
        internal static void Bind(ModuleConfig cfg)
        {
            if (Bound) return;
            BindPlayer(cfg);
            BindNpc(cfg);
            BindWomen(cfg);
            BindFirstPersonTools(cfg);
            BindAnimationTools(cfg);
            BindNpcTools(cfg);
            Character.SettingChanged += OnCharacterChanged;
        }

        /// <summary>[Presentation] Character (canonicalised legacy choices) and the first-person body / arms / driver / equipment / sound / sway / ADS toggles.</summary>
        static void BindPlayer(ModuleConfig cfg)
        {
            Character = cfg.Player("", "Character", CharacterChoice.Male, "Your character's body. Male, Female, Merchant, Bodyguard, Sprokka, OrganicMechanic, Gungirl and Shoota use Wild Wasteland player animations, in singleplayer. Legacy Pistoleer/Mechanic choices map to Bodyguard/Merchant.");
            Character.Value = (CharacterChoice)CharacterPortraits.Canonical((int)Character.Value);
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

        /// <summary>[Presentation / Dogs] DogAnimations and DogsLieDown (ex [NPC] toggles; the behaviour switches stay with NPCAI).</summary>
        static void BindNpc(ModuleConfig cfg)
        {
            DogAnimationsEnabled = cfg.Player("Dogs", "DogAnimations", true, "Retargeted animations on hounds, Grimhound and nightwalkers. Requires Presentation/Models/npcai_dogs.bundle; otherwise the existing procedural bite remains active.");
            DogsLieDown = cfg.Player("Dogs", "DogsLieDown", true, "Non-boss quadrupeds alternate standing and lying idle, spending a random 10-25 seconds in each pose. Threats interrupt rest through get-up.");
        }

        /// <summary>Binds one Women of Wasteland player setting; the "General" group maps to [Presentation / Women of Wasteland].</summary>
        static ConfigEntry<T> Woman<T>(ModuleConfig cfg, string section, string key, T value, string description, AcceptableValueBase range = null)
        {
            string sub = section == "General" ? WomenSection : section;
            return range == null ? cfg.Player(sub, key, value, description) : cfg.Player(sub, key, value, description, range);
        }

        /// <summary>Women of Wasteland: female population share, crew conversion, Gungirl/Shoota models, accessories and voices, the debug spawn key.</summary>
        static void BindWomen(ModuleConfig cfg)
        {
            EnableFemaleNpc = Woman(cfg, "General", "EnableFemaleNpc", true, "Convert a share of newly spawned Flexa and Sprokka raiders to Gungirl and Shoota. Existing females keep their identity when off.");
            FemalePopulation = Woman(cfg, "General", "FemalePopulation", 33, "Percentage of eligible new raiders and crew converted to female variants.", new AcceptableValueRange<int>(0, 100));
            FemaleCrew = Woman(cfg, "General", "FemaleCrew", true, "Apply the female population chance to new Flexa/Sprokka drivers, passengers and turret gunners. Explicit female template choices still apply when off.");
            GungirlModel = Woman(cfg, "Gungirl", "Model", "Models/Flexa_female.glb", "Gungirl model, relative to the Presentation asset folder. Restart to reload.");
            GungirlTexture = Woman(cfg, "Gungirl", "Texture", "Models/flexa_female.png", "Gungirl texture, relative to the Presentation asset folder. Restart to reload.");
            GungirlHideParts = Woman(cfg, "Gungirl", "HideParts", "beard, headband, armband", "Comma-separated attachment name prefixes hidden on newly dressed Gungirls.");
            ShootaModel = Woman(cfg, "Shoota", "Model", "Models/Sprokka_female.glb", "Shoota model, relative to the Presentation asset folder. Restart to reload.");
            ShootaTexture = Woman(cfg, "Shoota", "Texture", "Models/sprokka_female.png", "Shoota texture, relative to the Presentation asset folder. Restart to reload.");
            ShootaHideParts = Woman(cfg, "Shoota", "HideParts", "armor2, armband", "Comma-separated attachment name prefixes hidden on newly dressed Shootas.");
            GungirlVoice = Woman(cfg, "Gungirl", "Voice", "Sounds/Gungirl", "Female voice folder, relative to the Presentation asset folder. WAV and OGG; OGG wins. Restart to reload.");
            GungirlVoiceMatch = Woman(cfg, "Gungirl", "VoiceMatchLoudness", true, "Match female voice loudness to the native clips.");
            GungirlVoiceVolume = Woman(cfg, "Gungirl", "VoiceVolume", 1f, "Female voice volume multiplier.", new AcceptableValueRange<float>(0f, 4f));
            GungirlVoiceIntervalMin = Woman(cfg, "Gungirl", "VoiceIntervalMin", 2f, "Shortest combat shout pause for newly dressed females, seconds.", new AcceptableValueRange<float>(0f, 60f));
            GungirlVoiceIntervalMax = Woman(cfg, "Gungirl", "VoiceIntervalMax", 8f, "Longest combat shout pause for newly dressed females, seconds.", new AcceptableValueRange<float>(0f, 60f));
            FemaleSpawnKey = cfg.Hotkey("Spawn female raider", Key.None, "Debug: spawn a Gungirl six metres ahead.");
        }

        /// <summary>[Presentation / First person] (tools.cfg): view offsets and how the body sits under the first-person camera.</summary>
        static void BindFirstPersonTools(ModuleConfig cfg)
        {
            const string Fp = "First person";
            FpCamX = Slider(cfg, Fp, "FirstPersonBodyX"); FpCamY = Slider(cfg, Fp, "FirstPersonBodyY"); FpCamZ = Slider(cfg, Fp, "FirstPersonBodyZ");
            BodyBack = cfg.Tool(Fp, "FirstPersonBodyBack", 0.08f, "First person on foot: the head sits this far (m) behind the camera.");
            BodyBackDown = cfg.Tool(Fp, "BodyBackDown", 0.08f, "First person: extra distance behind the camera when looking straight down, m (blended in with the pitch).");
            FirstPersonWeaponBack = cfg.Tool(Fp, "FirstPersonWeaponBack", 0.08f, "First person with a weapon drawn: the body moves this much (m) further back.");
            FirstPersonChestLean = cfg.Tool(Fp, "FirstPersonChestLean", 20f, "First person with a weapon drawn: the chest bends back by this many degrees.");
        }

        /// <summary>First-person offset slider (m, -0.3..0.3; an out-of-range value is clamped). Also used by the seat part (Car seat section).</summary>
        internal static ConfigEntry<float> Slider(ModuleConfig cfg, string sub, string key)
        {
            var e = cfg.Tool(sub, key, 0f, "First-person view offset (m, view right / up / forward): the body moves the opposite way under the camera.", new AcceptableValueRange<float>(-0.3f, 0.3f));
            if (e.Value < -0.3f || e.Value > 0.3f) e.Value = Mathf.Clamp(e.Value, -0.3f, 0.3f);
            return e;
        }

        /// <summary>[Presentation / Animation|Animation (fallback clips)|Model] (tools.cfg): clip tuning, the non-bundle fallback clips and the model files.</summary>
        static void BindAnimationTools(ModuleConfig cfg)
        {
            const string Anim = "Animation", Leg = "Animation (fallback clips)", Mdl = "Model";
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

            ModelFile = cfg.Tool(Mdl, "Model", "Models/Player_female.glb", "Female body model (.glb/.gltf) relative to plugins/WildWasteland/Presentation, rigged to Flexa's skeleton (22 mixamorig bones). Restart the module to reload.");
            TextureFile = cfg.Tool(Mdl, "Texture", "Models/Player_female.png", "Female body texture relative to the module folder.");
            ArmsTextureFile = cfg.Tool(Mdl, "ArmsTexture", "Models/Player2_female_arms.png", "Female replacement for the game's Player2 texture atlas, used only on the first-person arms and kick leg.");
            AnimBundleFile = cfg.Tool(Mdl, "AnimationBundle", "Models/apocaplayer_anims.bundle", "AssetBundle with the humanoid (Mixamo) clips; loaded once per game start.");
        }

        /// <summary>[Presentation / NPC animation|NPC sounds|Dog poses] (tools.cfg): ModAPI clips and aim pose on gunmen, reload sounds, lying dogs.</summary>
        static void BindNpcTools(ModuleConfig cfg)
        {
            ApocaplayerClips = cfg.Tool("NPC animation", "ApocaplayerClips", true, "Gunmen are animated with the player's own clips and logic (8-way walk / run / sprint, crouch for rifles and pistols, aim and fire, reloads incl. one round at a time, pump / bolt after a burst, turns in place, hops) with the gun in the right hand at the player's weapon poses. false = the game's own clips (also a quick A/B for performance).");
            AimPose = cfg.Tool("NPC animation", "AimPose", true, "A gunman holding a shooting position keeps the gun up and aimed at you between bursts (the game lowered it to the idle pose and raised it only for the shot).");
            AnimationLod = cfg.Tool("NPC animation", "AnimationLod", true, "Performance: a character animated with the player's clips that no camera sees and that is more than 40 m away updates its animation logic every third frame (the game still poses it every frame). false = every character every frame (as before 2.0).");
            ReloadVolume = cfg.Tool("NPC sounds", "ReloadVolume", 1f, "Volume of a gunman's reload sounds (the clips the player's copy of the gun plays while reloading). 0 = silent.", new AcceptableValueRange<float>(0f, 1f));
            ReloadSoundRange = cfg.Tool("NPC sounds", "ReloadSoundRange", 35f, "How far a reload is heard, m (linear falloff from 2 m).", new AcceptableValueRange<float>(5f, 100f));
            DogLieFrontLift = cfg.Tool("Dog poses", "DogLieFrontLift", 0f, "Lying quadrupeds: the front half (chest, front legs, head) is tilted up at the waist until the shoulders stand as high over the hips as they do standing. This adds to (or, negative, takes from) that height, m.", new AcceptableValueRange<float>(-0.15f, 0.3f));
        }

        /// <summary>Character entry changed: a legacy alias is canonicalised first; a real change rebuilds the body while the module runs.</summary>
        static void OnCharacterChanged(object sender, EventArgs e)
        {
            var choice = (CharacterChoice)CharacterPortraits.Canonical((int)Character.Value);
            if (choice != Character.Value) { Character.Value = choice; return; }
            if (PresentationModule.Running) BodyRunner.CharacterChanged();
            PresentationEvents.ModelChanged(Entities.Player, Character.Value.ToString(), null);
        }
    }
}
