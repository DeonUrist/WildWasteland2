using System;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>Detached current weapon/arm authoring state; distances are centimetres and rotations degrees.</summary>
    public sealed class CharacterPoseDraft
    {
        public string Weapon, Clip, AimWeapon, AimClip, Status, Note, AimLine, FilePath;
        public float[] Shown, Aim;
        public string[] Clips;
        public int Frame, AimFrame, Selected, UserCount, BuiltinCount, AimCount;
        public bool Editing, ThirdPerson, CanEditArms, Crouched;
    }
    /// <summary>Input result supplied by the editor at the presentation update point.</summary>
    public struct PoseEditMotion { public Vector3 Move, Rotation; public bool Apply; }
    /// <summary>Real pose storage and live animation context. Tools owns input, pages and hint drawing.</summary>
    public interface ICharacterPoseAuthoring
    {
        /// <summary>Captures detached current weapon, animation and arm values.</summary>
        CharacterPoseDraft Capture();
        /// <summary>Registers owner-scoped input callbacks at the existing animation call points.</summary>
        void Register(IModule owner, Func<PoseEditMotion> weaponInput, Action<string,string,bool,float,float> aimInput);
        /// <summary>Flushes edits and releases callbacks for the exact editor owner.</summary>
        void Remove(IModule owner);
        /// <summary>Enables live pose authoring while an editor owner is active.</summary>
        bool Editing { get; set; }
        /// <summary>Selects weapon, left arm or right arm for runtime authoring.</summary>
        int Target { get; set; }
        /// <summary>Cycles the actual current-weapon animation preview.</summary>
        void StepClip(int delta);
        /// <summary>Returns preview playback to the animation selected by gameplay.</summary>
        void ResetSelection();
        /// <summary>Stores all six current weapon pose dimensions from a detached buffer.</summary>
        void SetWeapon(float[] values);
        /// <summary>Deletes the current own weapon pose and restores the normal fallback.</summary>
        void DeleteWeapon();
        /// <summary>Stores the complete thirteen-dimensional aim, head and arm record.</summary>
        void SetAim(string weapon, string clip, bool crouched, float[] values);
        /// <summary>Returns a detached complete aim, head and arm record.</summary>
        float[] GetAim(string weapon, string clip, bool crouched);
        /// <summary>Applies bounded current-arm movement and rotation.</summary>
        void ArmMotion(bool left, Vector3 move, Vector3 rotation, float seconds);
        /// <summary>Commits pending weapon and arm motion when input ends.</summary>
        void EndMotion();
        /// <summary>Adjusts one current-arm dimension and saves it.</summary>
        void NudgeArm(bool left, int axis, float delta);
        /// <summary>Resets one current arm while preserving the rest of the pose.</summary>
        void ResetArm(bool left);
        /// <summary>Atomically saves the current authored data through its real owner.</summary>
        void Save();
        /// <summary>Drops unsaved edits and reloads authored pose files.</summary>
        void Reload();
    }
    /// <summary>Detached fitted-wheel state and full 60/96-value setup; never a live mutable IK buffer.</summary>
    public sealed class WheelGripDraft
    {
        public string Key, Name, Owner, FilePath;
        public float[] Values;
        public bool Available, Handlebars, Editing;
        public int Preview, Pose;
        public float TowardDriver, RimDiameter;
        public Vector2 Requested, Reach;
        public bool LeftLimited, RightLimited;
    }
    /// <summary>Actual wheel IK setup storage and preview control; editor input and rendering belong to Tools.</summary>
    public interface IWheelGripAuthoring
    {
        /// <summary>Captures detached fitted-wheel setup, preview and reach diagnostics.</summary>
        WheelGripDraft Capture();
        /// <summary>Registers wheel input at the existing runtime update point.</summary>
        void Register(IModule owner, Action input);
        /// <summary>Flushes edits and releases callbacks for the exact editor owner.</summary>
        void Remove(IModule owner);
        /// <summary>Sets bounded authoring preview intent without altering native steering.</summary>
        void Preview(bool editing, int pose, int preview);
        /// <summary>Validates and applies the full current-wheel setup, including wheel-side mirroring.</summary>
        void SetValues(string key, float[] values, int editedPose, int editedSide);
        /// <summary>Restarts steering stroke interpolation after replacing a complete copied setup.</summary>
        void ResetMotion();
        /// <summary>Atomically saves the current authored data through its real owner.</summary>
        void Save();
        /// <summary>Restores the fitted wheel defaults and saves them.</summary>
        void Reset();
    }
}
