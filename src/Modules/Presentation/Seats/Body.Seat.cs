using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;

using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// Body part (split from PlayerBody/Body/Body.cs in Wave A): the seated pose in the driver's seat - gun arms from clips over the
    /// seat pose, learned chest yaw per clip, car throws, wheel-arm IK hand-off. Target: Presentation SeatPose (B06).
    /// </summary>
    internal sealed partial class Body
    {
        // in a car: the pose comes from the car's seated driver. With a gun drawn (bundle clips) her arms come from the gun clips instead -
        // RifleIdle / PistolIdle held, RifleFire / PistolFire while shooting, the Reload clips while reloading - played on the upper-body layer
        // of her graph; after the Animator wrote them, their local rotations (shoulders, arms, forearms, hands) are kept over the seat pose,
        // so hips, legs and spine stay seated. The gun model sits in her right hand at the weapon's Idle / Fire / Reload pose; shown in third person.
        private static readonly string[] CarArmBones = { "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
                                                         "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
                                                         "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2" };
        private readonly Quaternion[] _carArms = new Quaternion[CarArmBones.Length];
        private bool _carArmsReady;
        private string _carWeapon = "";
        private bool _carTurned, _carTurnedReady;
        // seated, her arms come from standing gun clips whose stance twists the whole body - without that twist the gun points off to a side
        // (≈45 degrees left). Per clip, an extra chest yaw is learned from where the barrel points against the aim (sideways only), every frame
        private readonly Dictionary<string, float> _carYawFix = new Dictionary<string, float>();
        /// <summary>Learns the extra chest yaw per car aiming clip from where the barrel points against the aim (sideways only).</summary>
        private void LearnCarYaw(Transform player, float aimYaw)
        {
            if (_prop == null || _propBarrel == Vector3.zero || string.IsNullOrEmpty(_upperClip)) return;
            var l = Quaternion.Inverse(player.rotation) * _prop.transform.TransformDirection(_propBarrel);
            if (new Vector2(l.x, l.z).sqrMagnitude < 0.25f) return;   // pointing up/down: no sideways reading
            float barrelYaw = Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
            float err = Mathf.DeltaAngle(Mathf.Clamp(aimYaw, -90f, 90f), barrelYaw);   // + = barrel right of the aim
            if (Mathf.Abs(err) > 120f) return;   // the barrel axis runs butt-first on this model: no use
            float corr; _carYawFix.TryGetValue(_upperClip, out corr);
            _carYawFix[_upperClip] = Mathf.Clamp(corr - err * 0.25f, -90f, 90f);
        }
        private readonly Dictionary<string, Quaternion> _carAll = new Dictionary<string, Quaternion>();

        // chest to the aim: yaw over Spine/Spine1/Spine2, pitch like on foot
        /// <summary>Turns the chest to the aim from the seat: yaw over the three spine bones, pitch as on foot.</summary>
        private void AimPitch(float pitch, float yaw)
        {
            if (Mathf.Abs(yaw) > 0.1f)
            {
                Turn("mixamorig:Spine", Vector3.up, yaw / 3f);
                Turn("mixamorig:Spine1", Vector3.up, yaw / 3f);
                Turn("mixamorig:Spine2", Vector3.up, yaw / 3f);
            }
            float p = pitch * CharacterSettings.AimPitchShare.Value;
            if (Mathf.Abs(p) > 0.5f)
            {
                Turn("mixamorig:Spine", Vector3.right, p * 0.3f);
                Turn("mixamorig:Spine1", Vector3.right, p * 0.3f);
                Turn("mixamorig:Spine2", Vector3.right, p * 0.3f);
                Turn("mixamorig:Neck", Vector3.right, p * 0.1f);
            }
        }

        private int _wheelPoseFrame = -1;
        private string _wheelArmOwner = "off";
        /// <summary>Applies the steering-wheel arms after the seat pose (LateUpdateLast job through BodyRunner.WheelTick).</summary>
        internal void LateWheelArms()
        {
            if (_wheelPoseFrame != Time.frameCount) return;
            if (_wheelArmOwner == "rest") WheelArms.RestPassengerArms(Bones, "wheel");
            else if (WheelArms.MissingWheel) WheelArms.RestPassengerArms(Bones, _wheelArmOwner);
            else WheelArms.Solve(Bones, _wheelArmOwner);
        }

        /// <summary>Late update of the body in the driver seat (pose copied from the seat, aiming and throws); exceptions are logged once.</summary>
        public void LateCar(Transform player, bool firstPerson)
        {
            float dt = Time.deltaTime;
            if (!_inCar && _mixamo) StartPoseFade(0.3f, false);   // into the driver's seat: the standing pose eases into the seat
            LateCarInner(player, firstPerson);
            if (_mixamo) { ApplyPoseFade(dt, player); CapturePose(player); }
            if (firstPerson && Core.PlayerState.PlayerCamera != null)
            {
                Transform head;
                if (Bones.TryGetValue("mixamorig:Head", out head))
                {
                    var camera = Core.PlayerState.PlayerCamera;
                    var yaw = Quaternion.Euler(0f, camera.eulerAngles.y, 0f);
                    var eye = CharacterSettings.BodyInCar ? ThirdPerson.CarShiftEye(camera) : camera.position;
                    // Match the existing model eye height; only the local view moves after the seated pose was captured.
                    Root.transform.position += eye - yaw * Vector3.forward * CharacterSettings.BodyBack.Value - Vector3.up * .075f - head.position;
                }
            }
        }

        /// <summary>The driver-seat pose: copies the seated man pose, places the hips, blends aiming / throwing clips and hides parts in first person.</summary>
        private void LateCarInner(Transform player, bool firstPerson)
        {
            if (!_inCar) { _inCar = true; _animator.enabled = true; _crouch = _prone = 0f; UpdateProp(""); _carArmsReady = false; _carWeapon = ""; }
            Root.transform.localScale = Vector3.one;
            Root.transform.SetPositionAndRotation(player.position, player.rotation);
            _anim.localPosition = Vector3.zero; _anim.localRotation = Quaternion.identity;
            string weapon = Game.DrawnWeapon;
            var kind = Props.KindOf(weapon);
            float aimYaw = 0f, aimPitch = 0f;
            if (Core.PlayerState.PlayerCamera != null)
            {
                var l = Quaternion.Inverse(player.rotation) * Core.PlayerState.PlayerCamera.forward;
                aimYaw = Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
                aimPitch = -Mathf.Asin(Mathf.Clamp(l.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            bool turnIntent = _carTurned ? Mathf.Abs(aimYaw)>80f : Mathf.Abs(aimYaw)>95f;
            _animator.enabled = true;
            _input.Step(Time.deltaTime,firstPerson,false,true,turnIntent,0f,0f);
            SyncInputAlignment();
            bool throwing = _mixamo && _layers.IsValid() && Time.time < _throwUntil && Anims.Get(_throwClip) != null;
            bool gun = _mixamo && _layers.IsValid() && (kind == Props.Kind.Rifle || kind == Props.Kind.Pistol);
            bool melee = _mixamo && _layers.IsValid() && kind == Props.Kind.Melee;
            bool fists = _mixamo && _layers.IsValid() && weapon == "hands";
            bool punching = fists && _upperW > .001f;
            bool arms = gun || melee || fists || throwing || _mixamo && _layers.IsValid() && kind == Props.Kind.Throw;
            _wheelPoseFrame = Time.frameCount;
            _wheelArmOwner = arms ? (throwing ? "throw" : melee ? "melee" : gun ? "gun" : "held item") : weapon != "" ? "held item" : Game.Binoculars ? "binoculars" : Game.ToolInHand ? "tool" : Game.GameArmsShown ? "native arms" : "wheel";
            if (!throwing && fists) _wheelArmOwner = punching ? "punch" : "rest";
            // the lance leaving her hand / the grenade holstering the gun changes the weapon mid-throw: that must not reset the throw's arms
            if (weapon != _carWeapon) { _carWeapon = weapon; if (!throwing) { _carArmsReady = false; _carTurnedReady = false; } }

            // past 90 degrees to a side she gets off the seat: turned to the aim, crouched, hips at the seat's height (back below 80)
            bool turned = arms && turnIntent;
            if (turned != _carTurned) { _carTurned = turned; _carTurnedReady = false; StartPoseFade(0.25f, true); }
            string fixKey = throwing ? "throw:" + _throwClip : _upperClip ?? "";
            float headYaw = aimYaw, headPitch = aimPitch;

            bool animated = arms && _carArmsReady && _animator.enabled;
            int carBoneCount = melee || fists || throwing ? CarArmBones.Length : 8;
            if (turned && animated && _carTurnedReady)
            {
                // the whole body from the Animator (crouch idle + gun clip / throw); only the seat's hip position is used
                foreach (var kv in Bones) _carAll[kv.Key] = kv.Value.localRotation;
                Transform hips; Vector3 hipsLocal = Vector3.zero;
                bool hasHips = Bones.TryGetValue("mixamorig:Hips", out hips);
                if (hasHips) hipsLocal = hips.localPosition;
                CarSeat.Pose(Bones, _bindLocal);
                Vector3 seatHips = hasHips ? hips.position : Root.transform.position;
                foreach (var kv in Bones) { Quaternion q; if (_carAll.TryGetValue(kv.Key, out q)) kv.Value.localRotation = q; }
                if (hasHips) hips.localPosition = hipsLocal;
                Root.transform.rotation = player.rotation * Quaternion.Euler(0f, aimYaw, 0f);
                if (hasHips) Root.transform.position += seatHips - hips.position;
                float tcorr = 0f; if (throwing) _carYawFix.TryGetValue(fixKey, out tcorr);
                AimPitch(aimPitch, tcorr);
                headYaw = -tcorr; headPitch = aimPitch * (1f - CharacterSettings.AimPitchShare.Value);
                if (throwing) LearnCarThrow(player, aimYaw, true);
            }
            else
            {
                // seated: the common driver's evaluated arms over the seat pose, the chest turned to the aim
                if (animated)
                    for (int i = 0; i < carBoneCount; i++) { Transform b; if (Bones.TryGetValue(CarArmBones[i], out b)) _carArms[i] = b.localRotation; }
                CarSeat.Pose(Bones, _bindLocal);
                if (fists && !throwing && animated) WheelArms.RestPassengerArms(Bones, "wheel");
                if (animated)
                {
                    if (!fists || punching || throwing)
                        for (int i = 0; i < carBoneCount; i++) { Transform b; if (Bones.TryGetValue(CarArmBones[i], out b)) b.localRotation = fists && !throwing ? Quaternion.Slerp(b.localRotation, _carArms[i], _upperW) : _carArms[i]; }
                    float corr; _carYawFix.TryGetValue(fixKey, out corr);
                    AimPitch(aimPitch, Mathf.Clamp(aimYaw, -90f, 90f) + corr);
                    headYaw = aimYaw - Mathf.Clamp(aimYaw, -90f, 90f) - corr; headPitch = aimPitch * (1f - CharacterSettings.AimPitchShare.Value);
                    if (throwing) LearnCarThrow(player, aimYaw, false);
                }
            }
            headYaw = Mathf.Clamp(headYaw, -85f, 85f); headPitch = Mathf.Clamp(headPitch, -65f, 65f);
            Turn("mixamorig:Neck", Vector3.up, headYaw * .35f); Turn("mixamorig:Head", Vector3.up, headYaw * .65f);
            Turn("mixamorig:Neck", Vector3.right, headPitch * .35f); Turn("mixamorig:Head", Vector3.right, headPitch * .65f);
            if (!arms)
            {
                if (_animator.enabled) _animator.enabled = false;
                _carArmsReady = false; _carTurned = false;
                // a lance held in the seat (not being thrown): in her hand, the seat's arms
                if (kind == Props.Kind.Throw) { UpdateProp(weapon); ShowProp(!firstPerson); } else UpdateProp("");
                return;
            }
            if (turned) _carTurnedReady = true;
            _carArmsReady = true;
            // the weapon in her right hand (the gun; the lance until it leaves the hand)
            UpdateProp(gun || melee || kind == Props.Kind.Throw ? weapon : "");
            if (_prop != null)
            {
                var w = Plan.PoseW;
                Vector3 p; Quaternion r;
                GunPose.Blend(_propFor, w, _propPos, _propRot, out p, out r);
                _prop.transform.localPosition = p; _prop.transform.localRotation = r;
                if (gun && !throwing && !turned && animated) LearnCarYaw(player, aimYaw);
                LanceAlign(kind);
            }
            ShowProp(!firstPerson);
        }

        // a throw from the seat goes where she aims: when her throwing hand passes the release point of the clip, its sideways angle from her
        // chest is compared with the aim and the chest turn for that clip is corrected (per clip, learned throw by throw like the punches)
        /// <summary>Learns the chest turn per throw clip from where the throwing hand passes the release point compared with the aim.</summary>
        private void LearnCarThrow(Transform player, float aimYaw, bool turned)
        {
            if (_aimMeasured || _upperClip != _throwClip || Plan.UpperTime < ThrowRelease) return;
            _aimMeasured = true;
            Transform hand, chest;
            if (!Bones.TryGetValue(_throwClip == "ThrowRight" ? "mixamorig:RightHand" : "mixamorig:LeftHand", out hand) || !Bones.TryGetValue("mixamorig:Spine2", out chest)) return;
            var v = Quaternion.Inverse(player.rotation) * (hand.position - chest.position);
            if (new Vector2(v.x, v.z).sqrMagnitude < 0.01f) return;
            float handYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float target = turned ? aimYaw : Mathf.Clamp(aimYaw, -90f, 90f);
            float err = Mathf.DeltaAngle(target, handYaw);   // + = hand right of the aim
            if (Mathf.Abs(err) > 120f) return;
            string key = "throw:" + _throwClip;
            float corr; _carYawFix.TryGetValue(key, out corr);
            _carYawFix[key] = Mathf.Clamp(corr - err, -90f, 90f);
            if (Log.L2) Log.Deep(PresentationModule.WorldOwner, "Car throw aim: " + _throwClip + " hand " + err.ToString("0.0") + " deg off -> chest turn " + _carYawFix[key].ToString("0.0"));
        }
    }
}
