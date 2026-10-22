using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.PlayerBody
{
    /// <summary>
    /// Body part (split from PlayerBody/Body/Body.cs in Wave A): player input reading for the body - the drawn weapon's [Attack] FSM /
    /// mouse clicks start throws, melee swings and strikes, plus the learned punch/throw aim. Target: Presentation player input adapter (B05).
    /// </summary>
    internal sealed partial class Body
    {
        private string _throwState = "", _throwFor = "";

        // drawn throwing weapon: its [Attack] FSM leaving "on" = the throw has begun (button pressed)
        private void WatchThrow(string weapon, bool click)
        {
            string st; float tv;
            if (!Game.Attack(weapon, out st, out tv)) { if (click) StartThrow(true); _throwFor = ""; return; }
            if (weapon != _throwFor) { _throwFor = weapon; _throwState = st; return; }
            if (_throwState == "on" && st != "on" && st != "" && st != "InMenu") { if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Throw: " + weapon + " [Attack] on -> " + st); StartThrow(true, true); }
            _throwState = st;
        }
        // melee swing in third person timed to the first-person one: the drawn weapon's arm Animator switches to its swing state on the click;
        // its length (s) = how long her swing lasts, and her clip is sped up to fit it (the raider/Mixamo swings are much slower than the game's)
        private Animator _meleeFp;
        private int _meleeHash0;
        private bool _meleeMeasuring;
        private float _meleeStart, _meleeLen = 0.5f, _meleeSpeed = 1f;
        private string _meleeFor = "";
        private readonly Dictionary<string, float> _meleeLens = new Dictionary<string, float>();   // measured swing length per weapon (a held button repeats the
                                                                                                 // swing state, so the next swings can't be measured)
        private void StartMelee(string weapon)
        {
            float known;
            _meleeStart = Time.time; _upperClip = ""; _meleeLen = _meleeLens.TryGetValue(weapon ?? "", out known) ? known : 0.5f; _meleeMeasuring = false; _meleeFp = null; _meleeFor = weapon ?? "";
            var w = Game.WeaponsParent != null && !string.IsNullOrEmpty(weapon) ? Game.WeaponsParent.Find(weapon) : null;
            if (w != null) _meleeFp = w.GetComponentInChildren<Animator>();
            if (_meleeFp != null && _meleeFp.isActiveAndEnabled) { _meleeHash0 = _meleeFp.GetCurrentAnimatorStateInfo(0).fullPathHash; _meleeMeasuring = true; }
        }

        private bool UpdateMelee(string clipName)
        {
            if (_meleeStart <= 0f) return false;
            if (_meleeMeasuring && _meleeFp != null && Time.time - _meleeStart < 0.3f)
            {
                var st = _meleeFp.IsInTransition(0) ? _meleeFp.GetNextAnimatorStateInfo(0) : _meleeFp.GetCurrentAnimatorStateInfo(0);
                if (st.fullPathHash != _meleeHash0 && st.length > 0.05f)
                {
                    float sp = Mathf.Abs(_meleeFp.speed * st.speedMultiplier);
                    _meleeLen = Mathf.Clamp(st.length / Mathf.Max(0.1f, sp), 0.2f, 2f);
                    _meleeMeasuring = false;
                    _meleeLens[_meleeFor] = _meleeLen;
                    if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Melee: first-person swing " + _meleeLen.ToString("0.00") + " s");
                }
            }
            else _meleeMeasuring = false;
            var c = Clip(clipName);
            _meleeSpeed = c != null ? Mathf.Clamp(c.length / _meleeLen, 0.5f, 5f) : 1f;
            if (Time.time - _meleeStart < _meleeLen) return true;
            _meleeStart = 0f;
            return false;
        }

        // ---- strikes (Mixamo mode): a segment of a clip - From (wind-up) .. Hit (the blow lands) .. To (back on guard), clip seconds, read off the
        // clips' arm-muscle speed (peak = the blow). The game's hit comes the moment the swing starts (its [Attack] FSM SphereCasts in "fire"),
        // so the wind-up is played in StrikeWindup s and the rest stretched over the swing's cycle (0.35 s hands, 0.4 s machete ...).
        private sealed class Strike
        {
            public string Clip, Hand; public float From, Hit, To;
            public Strike(string c, float a, float h, float e, string hand = null) { Clip = c; From = a; Hit = h; To = e; Hand = hand; }
            public string Key { get { return Clip + "@" + Hit.ToString("0.00"); } }   // per blow (one clip can hold several)
        }
        // Punch.fbx (1.7.3): one clip with eight blows, left/right - read off its forearm-stretch curves (Punch.anim): peaks at
        // L .47, L .80, R 1.17, R 1.47, L 1.87, R 2.07, L 2.33, R 2.53 s. A fresh punch starts at the first; chained ones go on in order.
        private const string LH = "mixamorig:LeftHand", RH = "mixamorig:RightHand";
        private static readonly Strike[] PunchSeq = {
            new Strike("Punch", 0.30f, 0.47f, 0.65f, LH), new Strike("Punch", 0.65f, 0.80f, 1.00f, LH),
            new Strike("Punch", 0.97f, 1.17f, 1.32f, RH), new Strike("Punch", 1.30f, 1.47f, 1.65f, RH),
            new Strike("Punch", 1.70f, 1.87f, 2.05f, LH), new Strike("Punch", 1.93f, 2.07f, 2.22f, RH),
            new Strike("Punch", 2.17f, 2.33f, 2.47f, LH), new Strike("Punch", 2.40f, 2.53f, 2.85f, RH) };
        private static readonly Strike[] Punches = { new Strike("Punch1", 0.75f, 1.0f, 1.5f), new Strike("Punch2", 0.3f, 0.5f, 0.95f) };   // right cross, left jab
        private static readonly Strike[] Combo = { new Strike("MeleeCombo", 1.5f, 1.73f, 2.3f), new Strike("MeleeCombo", 0.8f, 1.07f, 1.5f) };   // 2nd blow, 1st blow
        private Strike _strike;
        private bool _striking;
        private float _strikeAt, _strikeCycle = 0.4f, _strikeEnd = -10f, _strikeW, _atkTime;
        private int _punchN, _chainN, _seqN;
        private string _atkFor = "", _atkState = "";

        // the clip of a strike: "Melee1"/"Melee2" are taken as Punch1/Punch2 too; a missing one = a generic share of whatever clip there is
        private static Strike Resolve(Strike s, string alias)
        {
            if (Anims.Get(s.Clip) != null) return s;
            if (alias != null && Anims.Get(alias) != null) return new Strike(alias, s.From, s.Hit, s.To);
            return null;
        }

        private void WatchAttack(Props.Kind kind, string weapon, bool click)
        {
            string st; float tv;
            bool has = Game.Attack(weapon, out st, out tv);
            if (weapon != _atkFor) { _atkFor = weapon; _atkState = st; _atkTime = tv; _chainN = 0; return; }
            bool strike;
            if (has) strike = st != "" && st != "on" && (_atkState == "on" || tv > _atkTime + 0.05f);   // left "on", or the timer restarted (held: on -> fire in one frame)
            else { strike = click; tv = 0.4f; }
            _atkState = st; _atkTime = tv;
            if (strike) BeginStrike(kind, weapon, Mathf.Clamp(tv + Time.deltaTime, 0.2f, 1.5f));
        }

        private void BeginStrike(Props.Kind kind, string weapon, float cycle)
        {
            bool chained = _striking || _meleeStart > 0f || Time.time - _strikeEnd < 0.25f;   // straight after the last one: held button / fast clicks
            Strike s = null;
            if (kind == Props.Kind.None)
            {
                // bare hands: the Punch clip's blows in order (a fresh punch = the first), else Punch1 / Punch2 by turns
                if (Anims.Get("Punch") != null) { _seqN = chained ? (_seqN + 1) % PunchSeq.Length : 0; s = PunchSeq[_seqN]; }
                else for (int k = 0; k < 2 && s == null; k++) { int i = (_punchN + k) % 2; s = Resolve(Punches[i], i == 0 ? "Melee1" : "Melee2"); if (s != null) _punchN = i + 1; }
            }
            else
            {
                // melee weapon: the first swing = the Melee clip timed to the first-person swing (as before); the swings chained after it
                // = MeleeCombo's two blows by turns
                _chainN = chained ? _chainN + 1 : 0;
                if (_chainN > 0) s = Resolve(Combo[(_chainN - 1) % 2], null);
                if (s == null) { _striking = false; _strikeW = 0f; _strike = null; StartMelee(weapon); return; }
                _meleeStart = 0f;
            }
            if (s == null) return;
            _strikeHand = kind == Props.Kind.None ? (s.Hand ?? (s.Clip == "Punch1" || s.Clip == "Melee1" ? RH : LH)) : null;
            _aimMeasured = false;
            _strike = s; _striking = true; _strikeAt = Time.time; _strikeCycle = cycle; _strikeW = 1f;
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Strike: " + s.Clip + " " + s.From.ToString("0.00") + "-" + s.Hit.ToString("0.00") + "-" + s.To.ToString("0.00") + " in " + cycle.ToString("0.00") + " s" + (chained ? " (chained)" : ""));
        }

        // punches land on the middle line in front of her chest: the spine turns (Spine, Spine1, Spine2 a third each) by an angle learned per clip -
        // measured where the fist is when the blow lands (its sideways angle from the chest), corrected on every punch, ramped in over the
        // wind-up and out over the second half of the way back
        private readonly Dictionary<string, float> _aimYaw = new Dictionary<string, float>();
        private string _strikeHand;
        private bool _aimMeasured;
        // the quick grenade leaves from the game's "spawn throw" point (right of her head) straight along the view: her throwing hand (left) is
        // turned (spine yaw, learned per clip like the punches) to be on that line when she lets go
        private Transform _throwSpawn;
        private void ThrowAim()
        {
            if (!_throwAim || Time.time >= _throwUntil || !_upper.IsValid() || _upperClip != _throwClip) return;
            float t = (float)_upper.GetTime(), rel = ThrowRelease;
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
            if (_throwSpawn == null && Game.PlayerCamera != null) _throwSpawn = Game.FindDeep(Game.PlayerCamera, "spawn throw");
            var c = _anim.InverseTransformPoint(chest.position);
            var v = _anim.InverseTransformPoint(hand.position) - c;
            float tx = _throwSpawn != null ? (_anim.InverseTransformPoint(_throwSpawn.position) - c).x : 0f;   // the grenade's line, sideways from her chest
            if (v.z < 0.1f) return;
            float off = (Mathf.Atan2(v.x, v.z) - Mathf.Atan2(tx, Mathf.Max(0.3f, v.z))) * Mathf.Rad2Deg;   // + = hand right of the grenade's line
            _aimYaw[key] = Mathf.Clamp(aim - off * (k > 0.99f ? 1f : 0.5f), -45f, 45f);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Throw aim: hand " + off.ToString("0.0") + " deg off the grenade's line (spawn " + tx.ToString("0.00") + " m) -> spine turn " + _aimYaw[key].ToString("0.0"));
        }

        // 1.7.4: during a strike the chest faces where the game hits (the eye's yaw = the crosshair), whatever the legs / strafe / the clip's own
        // hip turn do: spine, spine1, spine2 turn (a third each, about the world up) so the shoulder line is square to the aim; for punches a
        // learned per-blow offset is added on top so the fist lands on the middle line (measured against the aim, not the body)
        private void PunchAim()
        {
            if (_strike == null || !_striking || Game.PlayerCamera == null) return;
            float el = Time.time - _strikeAt, w = Mathf.Min(CharacterSettings.StrikeWindup.Value, _strikeCycle * 0.4f), back = (_strikeCycle - w) * 0.5f;
            float k = el < w ? Mathf.Clamp01(el / Mathf.Max(0.001f, w * 0.5f)) : el < w + back ? 1f : Mathf.Clamp01(1f - (el - w - back) / Mathf.Max(0.01f, back));
            Transform s0, s1, s2, la, ra, chest;
            if (!Bones.TryGetValue("mixamorig:Spine", out s0) || !Bones.TryGetValue("mixamorig:Spine1", out s1) || !Bones.TryGetValue("mixamorig:Spine2", out s2)
                || !Bones.TryGetValue("mixamorig:LeftArm", out la) || !Bones.TryGetValue("mixamorig:RightArm", out ra)) return;
            chest = s2;
            Vector3 up = Root != null ? Root.transform.up : Vector3.up;
            Vector3 aimF = Vector3.ProjectOnPlane(Game.PlayerCamera.forward, up);
            if (aimF.sqrMagnitude < 1e-4f) return;
            Vector3 face = Vector3.ProjectOnPlane(Vector3.Cross(ra.position - la.position, up), up);
            float square = face.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(face, aimF, up) : 0f;   // turn that makes the chest face the aim
            float learned = 0f;
            if (_strikeHand != null) _aimYaw.TryGetValue(_strike.Key, out learned);
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
            _aimYaw[_strike.Key] = Mathf.Clamp(learned - off * (k > 0.99f ? 1f : 0.5f), -45f, 45f);
            if (Core.Log.L2) Core.Log.Deep(PresentationModule.Instance, "Punch aim: " + _strike.Key + " landed " + off.ToString("0.0") + " deg off the aim (chest squared " + square.ToString("0.0") + ") -> blow offset " + _aimYaw[_strike.Key].ToString("0.0"));
        }
    }
}
