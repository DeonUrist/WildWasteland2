using System;
using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    internal static class FemalePain
    {
        private static readonly string[] Names = { "FEMPAIN1", "FEMPAIN2", "FEMPAIN3", "FEMPAIN4" };
        private static readonly AudioClip[] Clips = new AudioClip[Names.Length];
        private static FsmObject[] _pain;
        private static FsmFloat[] _weights;
        private static FsmObject _death;
        private static readonly AudioClip[] Shouts = new AudioClip[2];
        /// <summary>The player's shout clip for the chosen character (female / male).</summary>
        internal static AudioClip PlayerShout { get { return Shouts[CharacterSettings.Female ? 1 : 0]; } }

        /// <summary>(WW) the clips are decoded once per game session (module Init), the patch is a module patch through Core.Patches</summary>
        internal static IEnumerator LoadClips()
        {
            yield return null;
            if (!PresentationModule.Running) yield break;
            for (int i = 0; i < Names.Length; i++)
            {
                if (Clips[i] != null) continue;
                try { Clips[i] = Audio.LoadWav(PresentationModule.AssetPath("Sounds/Female/" + Names[i] + ".wav"), Names[i]); if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Female pain loaded: " + Names[i]); }
                catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Female pain " + Names[i] + ": " + e.Message); }
            }
            _pain = new FsmObject[Clips.Length]; _weights = new FsmFloat[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                if (Clips[i] == null) { _pain = null; break; }
                _pain[i] = new FsmObject { ObjectType = typeof(AudioClip), Value = Clips[i] };
                _weights[i] = new FsmFloat { Value = 1f };
            }
            if (_death == null)
            {
                try { _death = new FsmObject { ObjectType = typeof(AudioClip), Value = Audio.LoadWav(PresentationModule.AssetPath("Sounds/Female/FEMDEATH.wav"), "FEMDEATH", 0.2f) }; Core.Log.Deep(PresentationModule.Instance, "Female death loaded: FEMDEATH"); }
                catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Female death: " + e.Message); }
            }
            for (int i = 0; i < Shouts.Length; i++)
            {
                if (Shouts[i] != null) continue;
                string name = i == 0 ? "shout_male" : "shout_female";
                try
                {
                    Shouts[i] = Audio.LoadWav(PresentationModule.AssetPath("Sounds/" + name + ".wav"), name, 0.35f);
                    if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Shout sound loaded: " + name + ".wav");
                }
                catch (Exception e) { Core.Log.Warn(PresentationModule.Instance, "Shout sound " + name + ": " + e.Message); }
            }
        }

        /// <summary>Patches PlayRandomSound.OnEnter and SetAudioClip.OnEnter (prefix + finalizer) for the female pain and death sounds.</summary>
        public static void Patch(Core.IModule owner)
        {
            Core.Patches.Patch(owner, HarmonyLib.AccessTools.Method(typeof(PlayRandomSound), "OnEnter"),
                prefix: HarmonyLib.AccessTools.Method(typeof(FemalePain), nameof(Before)),
                finalizer: HarmonyLib.AccessTools.Method(typeof(FemalePain), nameof(Finally)));
            Core.Patches.Patch(owner, HarmonyLib.AccessTools.Method(typeof(SetAudioClip), "OnEnter"),
                prefix: HarmonyLib.AccessTools.Method(typeof(FemalePain), nameof(BeforeDeath)),
                finalizer: HarmonyLib.AccessTools.Method(typeof(FemalePain), nameof(FinallyDeath)));
        }

        /// <summary>The native clips swapped out for one action call.</summary>
        internal struct SoundState { internal FsmObject[] Clips; internal FsmFloat[] Weights; }

        /// <summary>PlayRandomSound.OnEnter prefix (every random sound; filter: DamageEffectSound FSMs of the player, Female and PainSounds on): plays the female pain clips.</summary>
        internal static void Before(PlayRandomSound __instance, out SoundState __state)
        {
            __state = new SoundState();
            if (!PresentationModule.Running || !CharacterSettings.PainSounds.Value || !CharacterSettings.Female || _pain == null || __instance.Fsm == null || __instance.audioClips == null) return;
            string fsm = __instance.Fsm.Name;
            if (fsm != "DamageEffectSound" && fsm != "DamageEffectSound_InCar") return;
            var owner = __instance.Owner;
            if (owner == null) return;
            bool player = PlayerState.Player != null ? owner == PlayerState.Player || owner.transform.IsChildOf(PlayerState.Player.transform)
                : owner.name == "Player" || owner.name == "head" && owner.transform.parent != null && owner.transform.parent.name == "Player";
            if (!player) return;
            __state.Clips = __instance.audioClips; __state.Weights = __instance.weights;
            __instance.audioClips = _pain; __instance.weights = _weights;
            PresentationEvents.Voice(Entities.Player, "FEMPAIN", owner.transform.position);
        }

        /// <summary>PlayRandomSound.OnEnter finalizer: the native clips back.</summary>
        internal static Exception Finally(PlayRandomSound __instance, SoundState __state, Exception __exception)
        {
            if (__state.Clips != null) { __instance.audioClips = __state.Clips; __instance.weights = __state.Weights; }
            return __exception;
        }

        /// <summary>SetAudioClip.OnEnter prefix (filter: the player's Health FSM in playerDeath): the female death sound.</summary>
        internal static void BeforeDeath(SetAudioClip __instance, out FsmObject __state)
        {
            __state = null;
            if (!PresentationModule.Running || !CharacterSettings.PainSounds.Value || !CharacterSettings.Female || _death == null || __instance.Fsm == null || __instance.audioClip == null) return;
            if (__instance.Fsm.Name != "Health" || __instance.Fsm.ActiveStateName != "playerDeath") return;
            var owner = __instance.Owner;
            if (owner == null || (PlayerState.Player != null ? owner != PlayerState.Player : owner.name != "Player")) return;
            __state = __instance.audioClip; __instance.audioClip = _death;
            PresentationEvents.Voice(Entities.Player, "FEMDEATH", owner.transform.position);
            Core.Log.Deep(PresentationModule.Instance, "Female death: FEMDEATH selected");
        }

        /// <summary>SetAudioClip.OnEnter finalizer: the native clip back.</summary>
        internal static Exception FinallyDeath(SetAudioClip __instance, FsmObject __state, Exception __exception)
        {
            if (__state != null) __instance.audioClip = __state;
            return __exception;
        }
    }
}
