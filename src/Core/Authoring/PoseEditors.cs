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
        CharacterPoseDraft Capture();
        void Register(IModule owner, Func<PoseEditMotion> weaponInput, Action<string,string,bool,float,float> aimInput);
        void Remove(IModule owner);
        bool Editing { get; set; }
        int Target { get; set; }
        void StepClip(int delta);
        void ResetSelection();
        void SetWeapon(float[] values);
        void DeleteWeapon();
        void SetAim(string weapon, string clip, bool crouched, float[] values);
        float[] GetAim(string weapon, string clip, bool crouched);
        void ArmMotion(bool left, Vector3 move, Vector3 rotation, float seconds);
        void EndMotion();
        void NudgeArm(bool left, int axis, float delta);
        void ResetArm(bool left);
        void Save();
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
        WheelGripDraft Capture();
        void Register(IModule owner, Action input);
        void Remove(IModule owner);
        void Preview(bool editing, int pose, int preview);
        void SetValues(string key, float[] values, int editedPose, int editedSide);
        void Save();
        void Reset();
    }
}
