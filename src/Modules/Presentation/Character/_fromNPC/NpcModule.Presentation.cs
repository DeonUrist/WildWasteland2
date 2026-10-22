using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    /// <summary>
    /// NpcModule part owned by Presentation (Wave A split of NpcModule.cs / NpcModule.Config.cs): the dog animation bundle, Women of
    /// Wasteland lifecycle, the rig / visual LateUpdate jobs, the Pounce carcass hook, the taunt AudioPlay patch and the presentation
    /// config entries. Every method is a partial method called from the NPC entry at the position of the lines it replaced.
    /// </summary>
    public sealed partial class NpcModule
    {
        internal static ConfigEntry<bool> ApocaplayerClips, AimPose, DogAnimationsEnabled, DogsLieDown;
        internal static ConfigEntry<float> ReloadVolume, ReloadSoundRange, DogLieFrontLift;

        /// <summary>Loads the dog animation bundle and installs its hooks / patch (DogAnimations.Initialize).</summary>
        partial void InitPresentationDogs()
        {
            DogAnimations.Initialize(this);
        }

        /// <summary>Starts Women of Wasteland (female bodies, voices, providers, jobs).</summary>
        partial void InitPresentationWomen()
        {
            WomenFeature.Initialize(this);
        }

        /// <summary>Registers the rig builder (LateUpdate, sliced) and the visual LateUpdate ticks of Burrow, Pounce and DogBite, in the old order.</summary>
        partial void InitPresentationJobs()
        {
            Scheduler.Sliced(this, "NPC.Rigs", 1000, Brain.BuildRig, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Burrow.Late", Burrow.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "Pounce.Late", Pounce.LateTick, Phase.LateUpdate);
            Scheduler.EveryFrame(this, "DogBite.Late", DogBite.LateTick, Phase.LateUpdate);
        }

        /// <summary>Shutdown step "Women of Wasteland": restores female bodies, stops voices.</summary>
        static partial void ShutdownPresentationWomen()
        {
            Step("Women of Wasteland", WomenFeature.Shutdown);
        }

        /// <summary>Scene load: Gungirl caches.</summary>
        static partial void SceneLoadedPresentation()
        {
            Gungirl.OnSceneLoaded();
        }

        /// <summary>World ready: Gungirl's world scan.</summary>
        static partial void WorldReadyPresentation()
        {
            Gungirl.BeginScan();
        }

        /// <summary>CreateObject.OnEnter postfix: the carcass of a spider killed mid-leap flies on (Pounce.AfterCreateObject).</summary>
        partial void InstallHooksPresentation()
        {
            Hooks.CreateObjectEnter.Postfix(this, Pounce.AfterCreateObject);
        }

        /// <summary>AudioPlay.OnEnter prefix: NPC combat taunt audio (Senses.BeforeAudioPlay); module-only patch.</summary>
        partial void InstallPatchesPresentation()
        {
            Patch(typeof(AudioPlay), "OnEnter", typeof(Senses), "BeforeAudioPlay", true);
        }

        /// <summary>F6 toggles DogAnimations and DogsLieDown (between DogBite and DogJumpAttack).</summary>
        static partial void BindPresentationToggles()
        {
            DogAnimationsEnabled = B("DogAnimations", true, "Retargeted animations on hounds, Grimhound and nightwalkers. Requires NPC/Models/npcai_dogs.bundle; otherwise the existing procedural bite remains active.");
            DogsLieDown = B("DogsLieDown", true, "Non-boss quadrupeds alternate standing and lying idle, spending a random 10-25 seconds in each pose. Threats interrupt rest through get-up.");
        }

        /// <summary>tools.cfg [NPC / Behaviour] DogLieFrontLift (after DogIdlePoseMaxSeconds).</summary>
        static partial void BindPresentationDogPose()
        {
            DogLieFrontLift = H("Behaviour", "DogLieFrontLift", 0f, new ConfigDescription("Lying quadrupeds: the front half (chest, front legs, head) is tilted up at the waist until the shoulders stand as high over the hips as they do standing. This adds to (or, negative, takes from) that height, m.", new AcceptableValueRange<float>(-0.15f, 0.3f)));
        }

        /// <summary>tools.cfg [NPC / NpcAim] ReloadVolume and ReloadSoundRange (after SingleShotReload).</summary>
        static partial void BindPresentationReload()
        {
            ReloadVolume = H("NpcAim", "ReloadVolume", 1f, new ConfigDescription("Volume of a gunman's reload sounds (the clips the player's copy of the gun plays while reloading). 0 = silent.", new AcceptableValueRange<float>(0f, 1f)));
            ReloadSoundRange = H("NpcAim", "ReloadSoundRange", 35f, new ConfigDescription("How far a reload is heard, m (linear falloff from 2 m).", new AcceptableValueRange<float>(5f, 100f)));
        }

        /// <summary>tools.cfg [NPC / Brain] AimPose (after TurnRate).</summary>
        static partial void BindPresentationAimPose()
        {
            AimPose = H("Brain", "AimPose", true,
                "A gunman holding a shooting position keeps the gun up and aimed at you between bursts (the game lowered it to the idle pose and raised it only for the shot).");
        }

        /// <summary>tools.cfg [NPC / Brain] ApocaplayerClips (after StuckGiveUpCount).</summary>
        static partial void BindPresentationClips()
        {
            ApocaplayerClips = H("Brain", "ApocaplayerClips", true, "With Apocaplayer 2.2.0+ installed: gunmen are animated by its ModAPI - the player's own clips and logic (8-way walk / run / sprint, crouch for rifles and pistols, aim and fire, reloads incl. one round at a time, pump / bolt after a burst, turns in place, hops) with the gun in the right hand at the player's weapon poses. false = the game's own clips (also a quick A/B for performance).");
        }
    }
}
