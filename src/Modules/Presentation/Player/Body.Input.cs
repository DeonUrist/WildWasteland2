using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Player-specific hit alignment; all animation selection and clocks belong to CharPlan.</summary>
    internal sealed partial class Body
    {
        private int _alignmentSequence = -1;
        private string _strikeKey;
        /// <summary>Resets native hit-line measurement once for each action selected by the common driver.</summary>
        private void SyncInputAlignment()
        {
            if(_alignmentSequence==_input.ActionSequence)return;
            _alignmentSequence=_input.ActionSequence;_aimMeasured=false;
            _strikeKey=Plan.StrikeClip!=null?Plan.StrikeClip+"@"+Plan.StrikeHit.ToString("0.00"):null;
        }
        // punches land on the middle line in front of her chest: the spine turns (Spine, Spine1, Spine2 a third each) by an angle learned per clip -
        // measured where the fist is when the blow lands (its sideways angle from the chest), corrected on every punch, ramped in over the
        // wind-up and out over the second half of the way back
        private readonly Dictionary<string, float> _aimYaw = new Dictionary<string, float>();
        private string _strikeHand { get { return Plan.StrikeHand; } }
        private bool _aimMeasured;
        // the quick grenade leaves from the game's "spawn throw" point (right of her head) straight along the view: her throwing hand (left) is
        // turned (spine yaw, learned per clip like the punches) to be on that line when she lets go
        private Transform _throwSpawn;
        /// <summary>Quick grenade: turns the spine so the throwing hand releases along the grenade's flight line (measured once per throw).</summary>
        private void ThrowAim()
        {
            if (!_throwAim || Time.time >= _throwUntil || _upperClip != _throwClip) return;
            float t = Plan.UpperTime, rel = ThrowRelease;
            float k = t < rel ? Mathf.Clamp01((t - _throwFrom) / 0.2f) : Mathf.Clamp01(1f - (t - rel - 0.25f) / 0.4f);
            float aim;
            string key = "aim:" + _throwClip;
            _aimYaw.TryGetValue(key, out aim);
            float d = aim * k;
            if (Mathf.Abs(d) > 0.01f)
            {
                Turn("mixamorig:Spine", Vector3.up, d / 3f);
                Turn("mixamorig:Spine1", Vector3.up, d / 3f);
                Turn("mixamorig:Spine2", Vector3.up, d / 3f);
            }
            if (_aimMeasured || t < rel) return;
            _aimMeasured = true;
            Transform hand, chest;
            if (!Bones.TryGetValue("mixamorig:LeftHand", out hand) || !Bones.TryGetValue("mixamorig:Spine2", out chest)) return;
            if (_throwSpawn == null && PlayerState.PlayerCamera != null) _throwSpawn = Game.FindDeep(PlayerState.PlayerCamera, "spawn throw");
            var c = _anim.InverseTransformPoint(chest.position);
            var v = _anim.InverseTransformPoint(hand.position) - c;
            float tx = _throwSpawn != null ? (_anim.InverseTransformPoint(_throwSpawn.position) - c).x : 0f;   // the grenade's line, sideways from her chest
            if (v.z < 0.1f) return;
            float off = (Mathf.Atan2(v.x, v.z) - Mathf.Atan2(tx, Mathf.Max(0.3f, v.z))) * Mathf.Rad2Deg;   // + = hand right of the grenade's line
            _aimYaw[key] = Mathf.Clamp(aim - off * (k > 0.99f ? 1f : 0.5f), -45f, 45f);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Throw aim: hand " + off.ToString("0.0") + " deg off the grenade's line (spawn " + tx.ToString("0.00") + " m) -> spine turn " + _aimYaw[key].ToString("0.0"));
        }

        /// <summary>1.7.4: during a strike the chest faces where the game hits (the eye's yaw = the crosshair), whatever the legs / strafe / the clip's own hip turn do: spine, spine1, spine2 turn (a third each, about the world up) so the shoulder line is square to the aim; for punches a learned per-blow offset is added on top so the fist lands on the middle line (measured against the aim, not the body)</summary>
        private void PunchAim()
        {
            if (Plan.StrikeClip == null || _strikeKey == null || PlayerState.PlayerCamera == null) return;
            float el = Time.time - Plan.StrikeAt, w = Mathf.Min(CharacterSettings.StrikeWindup.Value, Plan.StrikeCycle * 0.4f), back = (Plan.StrikeCycle - w) * 0.5f;
            float k = el < w ? Mathf.Clamp01(el / Mathf.Max(0.001f, w * 0.5f)) : el < w + back ? 1f : Mathf.Clamp01(1f - (el - w - back) / Mathf.Max(0.01f, back));
            Transform s0, s1, s2, la, ra, chest;
            if (!Bones.TryGetValue("mixamorig:Spine", out s0) || !Bones.TryGetValue("mixamorig:Spine1", out s1) || !Bones.TryGetValue("mixamorig:Spine2", out s2)
                || !Bones.TryGetValue("mixamorig:LeftArm", out la) || !Bones.TryGetValue("mixamorig:RightArm", out ra)) return;
            chest = s2;
            Vector3 up = Root != null ? Root.transform.up : Vector3.up;
            Vector3 aimF = Vector3.ProjectOnPlane(PlayerState.PlayerCamera.forward, up);
            if (aimF.sqrMagnitude < 1e-4f) return;
            Vector3 face = Vector3.ProjectOnPlane(Vector3.Cross(ra.position - la.position, up), up);
            float square = face.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(face, aimF, up) : 0f;   // turn that makes the chest face the aim
            float learned = 0f;
            if (_strikeHand != null) _aimYaw.TryGetValue(_strikeKey, out learned);
            float d = Mathf.Clamp(square + learned, -90f, 90f) * k;
            if (Mathf.Abs(d) > 0.01f)
            {
                var q = Quaternion.AngleAxis(d / 3f, up);
                s0.rotation = q * s0.rotation; s1.rotation = q * s1.rotation; s2.rotation = q * s2.rotation;
            }
            if (_strikeHand == null || _aimMeasured || el < w) return;
            _aimMeasured = true;
            Transform hand;
            if (!Bones.TryGetValue(_strikeHand, out hand)) return;
            Vector3 v = Vector3.ProjectOnPlane(hand.position - chest.position, up);
            if (Vector3.Dot(v, aimF.normalized) < 0.1f) return;
            float off = Vector3.SignedAngle(aimF, v, up);   // + = fist right of the aim line
            _aimYaw[_strikeKey] = Mathf.Clamp(learned - off * (k > 0.99f ? 1f : 0.5f), -45f, 45f);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Punch aim: " + _strikeKey + " landed " + off.ToString("0.0") + " deg off the aim (chest squared " + square.ToString("0.0") + ") -> blow offset " + _aimYaw[_strikeKey].ToString("0.0"));
        }
    }
}
