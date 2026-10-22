using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;
namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>Player-only fitting and prop authoring around the common humanoid driver.</summary>
    internal sealed partial class Body
    {
        private string _poseListFor;
        private Props.Kind _poseListKind;
        private bool _poseListSet;
        /// <summary>Feeds native player input to the common driver, then applies capsule/view-specific corrections.</summary>
        private void LateCharacter(View view,float dt,Quaternion yaw,float camPitch,bool crouched)
        {
            if(_snap)Plan.Snap();
            string weapon=Game.DrawnWeapon;var kind=Props.KindOf(weapon);
            SetPoseList(kind,weapon);
            _input.Step(dt,view==View.FirstPerson,crouched,false,false,camPitch,_prone);
            SyncInputAlignment();
            UpdateProp(weapon);ShowProp(view==View.ThirdPerson);
            if(_crouch>.001f && Plan.ActionW>.001f && Plan.ActionClip.Contains("Jump"))
            {
                Transform head;
                if(Bones.TryGetValue("mixamorig:Head",out head))
                {
                    float top=PlayerState.Player.transform.position.y+PlayerState.StandingHeight*.5f;
                    Crouch(_crouch*Plan.ActionW,Mathf.Max(0f,head.position.y-(top-.14f)));
                }
            }
            else if((Anims.Get("RifleCrouchIdle")==null || Anims.Get("RifleCrouchWalk")==null) && _crouch>.001f)Crouch(_crouch);
            AimKeys(weapon,Plan.AimLiftW>.001f?Plan.AimLiftClip:null,dt);
            AimLift.EditWeapon=GunPose.Editing && ThirdPerson.On?weapon:null;
            AimLift.EditClip=DominantPose();AimLift.EditCrouched=Plan.CrouchW>.5f;AimLift.EditFrame=Time.frameCount;
            ThrowAim();PunchAim();PoseProp(kind,yaw);LanceAlign(kind);
        }
        /// <summary>Publishes the current weapon's available authoring previews.</summary>
        private void SetPoseList(Props.Kind kind, string weapon)
        {
            if (_poseListSet && kind == _poseListKind && string.Equals(weapon ?? "", _poseListFor, StringComparison.Ordinal)) return;   // (WW perf) was kind+"|"+weapon per frame
            _poseListSet = true; _poseListKind = kind; _poseListFor = weapon ?? "";
            var l = new List<string>();
            string idle = "Idle";
            if (kind == Props.Kind.Rifle)
            {
                idle = "RifleIdle";
                for (int i = 0; i < LocoPlan.BN; i++) if (!l.Contains(Plan.SlotClip[i])) l.Add(Plan.SlotClip[i]);
                foreach (var n in new[] { "ShotgunPump", "Idle", "RifleWalkLow", "RifleRunLow", "WalkStrafeLeft", "WalkStrafeRight", "WalkBack", "RifleWalkToStop", "LeftTurn", "RightTurn", "RifleAim", "RifleFire", "RifleCrouchAim", "RifleCrouchFire", "RifleReload", "RifleJumpUp", "RifleJumpLoop", "RifleJumpDown", "RifleTurnLeft", "RifleTurnRight", "RifleCrouchTurnLeft", "RifleCrouchTurnRight", "Kick" })
                    if (Anims.Get(n) != null && !l.Contains(n)) l.Add(n);
            }
            else if (kind == Props.Kind.Pistol)
            {
                idle = "PistolIdle";
                foreach (var n in new[] { "PistolIdle", "PistolRun", "PistolFire", "PistolReload", "PistolJump", "Kick" }) if (Anims.Get(n) != null) l.Add(n);
            }
            else
            {
                foreach (var n in new[] { "Idle", "Walk", "WalkBack", "Run", "CrouchIdle" }) if (Anims.Get(n) != null) l.Add(n);
                if (kind == Props.Kind.Throw) { foreach (var n in new[] { "Throw", "ThrowRight" }) if (Anims.Get(n) != null) l.Add(n); }
                else foreach (var n in new[] { "Melee", "MeleeCombo" }) if (Anims.Get(n) != null) l.Add(n);
                foreach (var n in new[] { "Kick", "Jump" }) if (Anims.Get(n) != null) l.Add(n);
            }
            GunPose.SetPoseList(l, idle);
        }

        /// <summary>The pose clip with the most weight this frame (the editor's preview clip first).</summary>
        private string DominantPose()
        {
            if (Plan.Preview != null) return Plan.Preview;
            string best = null; float bw = -1f;
            foreach (var kv in _poseWn) if (kv.Value > bw) { bw = kv.Value; best = kv.Key; }
            return best ?? "Idle";
        }

    }
}
