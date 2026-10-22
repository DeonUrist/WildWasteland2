using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.PlayerBody
{
    // Where each weapon sits in her RIGHT HAND, per animation clip: ABSOLUTE local position (cm) and rotation (degrees, Euler x, y, z) of the
    // weapon model under the hand bone. Nothing in the game changes these: no AutoGrip, no base line, no follow-the-left-hand - only the numpad
    // (WeaponAdjustment) edits them.
    //   (2.0) entries are keyed by the CLIP NAME that moves her hands (RifleWalkForwardLeft, PistolFire, Kick ...), one entry per clip.
    //   lookup per (weapon, clip): config/Apocaplayer/weapon-poses.txt  >  BuiltinPoses (hard-coded in the mod)  >  the weapon kind's idle clip
    //   (RifleIdle / PistolIdle, set by Body)  >  the weapon's old "Idle" entry  >  the raider's grip.
    // Every save also writes config/Apocaplayer/BuiltinPoses.generated.cs = the complete table as C#: copy it over BuiltinPoses.cs in the repo
    // to hard-code the poses.
    // (WW2, D12) the 0.5.x / 0.6.x grip conversion (weapon-grips.cfg, [Weapon grip] lines, legacy-base-lines.txt) is gone: fresh start.
    internal static partial class GunPose
    {
        // the clips the drawn weapon can show (set by Body.SetPoseList): the numpad 9/3 cycle, in that order
        private static readonly List<string> _list = new List<string>();
        private static string _idleName = "Idle";
        private static int PoseIndex(string name) { int i = _list.IndexOf(name); return i >= 0 ? i : 1000; }

        private static string _dir, _file, _export;
        private static readonly Dictionary<string, float[]> _user = new Dictionary<string, float[]>();      // "weapon|Anim" -> x,y,z,rx,ry,rz
        private static readonly Dictionary<string, float[]> _builtin = new Dictionary<string, float[]>();

        /// <summary>Module Init: loads the built-in table and the user's weapon-poses.txt from config/WildWasteland/PlayerBody.</summary>
        public static void Bind()
        {
            _dir = PlayerBodyModule.DataDir;
            Directory.CreateDirectory(_dir);
            _builtin.Clear(); _live = null; _liveKey = null; Dirty();
            EditorTarget = 0; _binding = -1;
            _file = Path.Combine(_dir, "weapon-poses.txt");
            _export = Path.Combine(_dir, "BuiltinPoses.generated.cs");
            foreach (var line in BuiltinPoses.Data)
            {
                var p = line.Split('|');
                float[] v;
                if (p.Length == 3 && (v = Parse(p[2])) != null) _builtin[p[0] + "|" + p[1]] = v;
            }
            LoadUser();
            PlayerBodyModule.Info("Weapon poses: " + _user.Count + " in weapon-poses.txt, " + _builtin.Count + " built in");
        }

        private static float[] Parse(string s)
        {
            var parts = (s ?? "").Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) return null;
            var v = new float[6];
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
            return v;
        }

        private static string Fmt(float[] v) { return string.Join(", ", Array.ConvertAll(v, x => x.ToString("0.###", CultureInfo.InvariantCulture))); }

        private static void LoadUser()
        {
            _user.Clear(); Dirty();
            if (!File.Exists(_file)) return;
            foreach (var raw in File.ReadAllLines(_file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                var v = Parse(line.Substring(eq + 1));
                if (v != null && key.IndexOf('|') > 0) _user[key] = v;
            }
        }

        // ---------------- values
        public static bool HasAny(string weapon)
        {
            string pre = weapon + "|";
            foreach (var k in _user.Keys) if (k.StartsWith(pre, StringComparison.Ordinal)) return true;
            foreach (var k in _builtin.Keys) if (k.StartsWith(pre, StringComparison.Ordinal)) return true;
            return false;
        }

        public static float[] Abs(Vector3 p, Quaternion r)
        {
            var e = r.eulerAngles;
            return new[] { p.x * 100f, p.y * 100f, p.z * 100f, Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z) };
        }
        // (WW perf) the same into a caller's reused float[6] (no garbage per frame)
        public static float[] Abs(Vector3 p, Quaternion r, float[] into)
        {
            var e = r.eulerAngles;
            into[0] = p.x * 100f; into[1] = p.y * 100f; into[2] = p.z * 100f;
            into[3] = Mathf.DeltaAngle(0f, e.x); into[4] = Mathf.DeltaAngle(0f, e.y); into[5] = Mathf.DeltaAngle(0f, e.z);
            return into;
        }
        private static readonly float[] _blendDef = new float[6];   // Blend/BlendFor's default (only read inside the call)

        // (WW perf) resolved (weapon, clip) -> file/built-in entry (null = none), so Own doesn't build "weapon|clip" per call per frame;
        // cleared whenever _user/_builtin change (Dirty)
        private static readonly Dictionary<string, Dictionary<string, float[]>> _own = new Dictionary<string, Dictionary<string, float[]>>();
        private static void Dirty() { _own.Clear(); }

        private static string _liveKey;
        private static float[] _live;

        // the clip's own entry (live edit > file > built in), null when it has none
        public static float[] Own(string weapon, string clip)
        {
            if (string.IsNullOrEmpty(clip)) return null;
            if (weapon == null) weapon = "";   // (weapon + "|" made null "")
            if (_live != null && _liveWeapon == weapon && _livePose == clip) return _live;   // (= _liveKey == weapon|clip)
            Dictionary<string, float[]> byClip;
            if (!_own.TryGetValue(weapon, out byClip)) _own[weapon] = byClip = new Dictionary<string, float[]>();
            float[] v;
            if (byClip.TryGetValue(clip, out v)) return v;
            string k = weapon + "|" + clip;
            if (!_user.TryGetValue(k, out v) && !_builtin.TryGetValue(k, out v)) v = null;
            byClip[clip] = v;
            return v;
        }

        // ---------------- the drawn weapon's clips (set by Body): the numpad cycle, and the kind's idle clip = the fallback for untuned ones
        public static void SetPoseList(List<string> names, string idleName)
        {
            string was = _sel >= 0 && _sel < _list.Count ? _list[_sel] : null;
            _list.Clear(); _list.AddRange(names);
            _idleName = idleName ?? "Idle";
            _sel = was != null ? _list.IndexOf(was) : -1;   // another weapon: stay on the same clip when it has it (to copy a pose across weapons)
        }
        private static void Put(string weapon, string clip, float[] v)
        {
            if (v == null) _user.Remove(weapon + "|" + clip);
            else _user[weapon + "|" + clip] = (float[])v.Clone();
            Dirty();
        }

        public static bool HasPose(string weapon, string clip) { return !string.IsNullOrEmpty(weapon) && Own(weapon, clip) != null; }

        // what the clip shows: its own entry, else the kind's idle clip, else the weapon's old Idle entry, else the raider grip (def)
        public static float[] Effective(string weapon, string clip, float[] def)
        {
            return Own(weapon, clip) ?? Own(weapon, _idleName) ?? Own(weapon, "Idle") ?? def;
        }
        // (2.2.0, ModAPI) the same for a character that is not the player: its own kind's idle clip as the fallback (not the drawn weapon's)
        public static float[] EffectiveFor(string weapon, string clip, string idleName, float[] def)
        {
            return Own(weapon, clip) ?? Own(weapon, idleName) ?? Own(weapon, "Idle") ?? def;
        }
        public static void BlendFor(string weapon, Dictionary<string, float> w, string idleName, Vector3 defPos, Quaternion defRot, out Vector3 pos, out Quaternion rot)
        {
            var def = Abs(defPos, defRot, _blendDef);
            pos = Vector3.zero; float sum = 0f; var q = Vector4.zero; Quaternion first = Quaternion.identity; bool any = false;
            foreach (var kv in w)
            {
                if (kv.Value <= 0.0001f) continue;
                var v = EffectiveFor(weapon, kv.Key, idleName, def);
                pos += new Vector3(v[0], v[1], v[2]) * (0.01f * kv.Value);
                var r = Quaternion.Euler(v[3], v[4], v[5]);
                if (!any) { first = r; any = true; }
                if (Quaternion.Dot(first, r) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                q += new Vector4(r.x, r.y, r.z, r.w) * kv.Value;
                sum += kv.Value;
            }
            if (sum <= 0f) { var v = EffectiveFor(weapon, idleName, idleName, def); pos = new Vector3(v[0], v[1], v[2]) * 0.01f; rot = Quaternion.Euler(v[3], v[4], v[5]); return; }
            pos /= sum;
            q.Normalize();
            rot = new Quaternion(q.x, q.y, q.z, q.w);
        }

        // blend of the clips' poses by weight -> hand-local position (m) and rotation
        public static void Blend(string weapon, Dictionary<string, float> w, Vector3 defPos, Quaternion defRot, out Vector3 pos, out Quaternion rot)
        {
            var def = Abs(defPos, defRot, _blendDef);
            pos = Vector3.zero; float sum = 0f; var q = Vector4.zero; Quaternion first = Quaternion.identity; bool any = false;
            foreach (var kv in w)
            {
                if (kv.Value <= 0.0001f) continue;
                var v = Effective(weapon, kv.Key, def);
                pos += new Vector3(v[0], v[1], v[2]) * (0.01f * kv.Value);
                var r = Quaternion.Euler(v[3], v[4], v[5]);
                if (!any) { first = r; any = true; }
                if (Quaternion.Dot(first, r) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                q += new Vector4(r.x, r.y, r.z, r.w) * kv.Value;
                sum += kv.Value;
            }
            if (sum <= 0f) { var v = Effective(weapon, _idleName, def); pos = new Vector3(v[0], v[1], v[2]) * 0.01f; rot = Quaternion.Euler(v[3], v[4], v[5]); return; }
            pos /= sum;
            q.Normalize();
            rot = new Quaternion(q.x, q.y, q.z, q.w);
        }

        public static void SetLive(string weapon, string clip, float[] v)
        {
            string k = weapon + "|" + clip;
            if (_liveKey != k) Flush();
            _liveKey = k; _live = v; _liveWeapon = weapon ?? ""; _livePose = clip;
        }
        private static string _liveWeapon, _livePose;

        // writes the edited pose (once, when the keys are let go / the weapon or animation changes)
        public static void Flush()
        {
            if (_live != null && _liveKey != null) { var v = _live; _live = null; _liveKey = null; Put(_liveWeapon, _livePose, v); Save(); }
            _live = null; _liveKey = null;
        }

        private static void Save()
        {
            try
            {
                var keys = new List<string>(_user.Keys); keys.Sort(Compare);
                var sb = new StringBuilder();
                sb.AppendLine("# Wild Wasteland (PlayerBody) weapon poses: weapon|Animation = x, y, z (cm), rotation x, y, z (degrees) of the weapon model in her right hand.");
                sb.AppendLine("# Absolute values, edited in game with WeaponAdjustment (numpad). Missing animation = the weapon's Idle, else the raider's grip.");
                foreach (var k in keys) sb.Append(k).Append(" = ").AppendLine(Fmt(_user[k]));
                File.WriteAllText(_file, sb.ToString());
                ExportBuiltinTable();   // (Wave A) the C# export lives with the editor (Developer)
            }
            catch (Exception e) { PlayerBodyModule.Warn("Weapon poses: saving failed: " + e.Message); }
        }

        // weapon, then animation in clip order
        private static int Compare(string a, string b)
        {
            int ia = a.IndexOf('|'), ib = b.IndexOf('|');
            int c = string.CompareOrdinal(a.Substring(0, ia), b.Substring(0, ib));
            if (c != 0) return c;
            return PoseIndex(a.Substring(ia + 1)).CompareTo(PoseIndex(b.Substring(ib + 1)));
        }

    }
}
