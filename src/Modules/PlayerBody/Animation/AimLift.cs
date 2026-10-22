using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WildWasteland.Modules.PlayerBody
{
    // (2.1.7) Aiming a gun, the game shoots from the camera (her eyes) - but the Mixamo aim clips hold the rifle ~30 cm lower, so the shots seemed
    // to leave from her mouth. While an aim / fire clip moves her hands, both hands are lifted (and pushed forward) together by a two-bone arm IK
    // (shoulders stay, elbows bend, the grip and the weapon pose in the hand are untouched) and her head tilts down onto the stock.
    // Per weapon and aim clip: lift (cm), head down (degrees), forward (cm) - in config/WildWasteland/PlayerBody/aim-lift.txt (its own file: weapon-poses.txt
    // is never touched). WeaponAdjustment on: Page Up / Page Down = lift, Home / End = head, Insert / Delete = forward, live.
    // The math here has no Unity types (tools/locotest checks it outside the game).
    internal static class AimLift
    {
        public static readonly string[] AimClips = { "RifleAim", "RifleCrouchAim", "RifleFire", "RifleCrouchFire", "PistolFire" };
        public static bool IsAimClip(string clip) { return Array.IndexOf(AimClips, clip) >= 0; }

        // the defaults when there is no entry: rifles lifted most of the way to the eyes (the right hand sits ~32 cm under them in RifleAim,
        // the sights ~8 cm over the grip), the head tilted onto the stock; pistols as they are (PistolFire holds the gun at eye level)
        public static float[] Default(string clip)
        {
            var v = new float[13];
            if (clip != null && clip.StartsWith("Rifle")) { v[0] = 15f; v[1] = 10f; }
            return v;
        }

        private static readonly Dictionary<string, float[]> _v = new Dictionary<string, float[]>();
        // (2.1.9) Denis's tuned values, hard-coded (aim-lift.txt overrides them; the same "weapon|clip[@crouch]=lift, head, forward")
        // (2.2.3) Denis's final tuning (config/WildWasteland/PlayerBody/aim-lift.txt of 2026-10-08 02:43), standing and crouched
        private static readonly string[] BuiltinData =
        {
            "22_pipe_pistol|PistolFire=9.7, -12, 0",
            "22_pipe_pistol|PistolFire@crouch=3.5, -12, 0",
            "22_pipe_revolver|PistolFire=12.1, -28.8, 0",
            "22_pipe_revolver|PistolFire@crouch=8.8, -28.8, 0",
            "22_pipe_smg|PistolFire=11.9, -23.6, 0",
            "22_pipe_smg|PistolFire@crouch=1.1, -23.6, 0",
            "akm_drum|RifleAim=11.5, -10.6, 0",
            "akm_drum|RifleCrouchAim@crouch=10.6, -28.8, 0",
            "akm_trash|RifleAim=8.1, -0.4, 0",
            "akm_trash|RifleCrouchAim@crouch=12.1, -9.4, 0",
            "akms|RifleAim=11.4, -24, 0",
            "akms|RifleCrouchAim@crouch=10.1, -14.6, 0",
            "borz_smg|PistolFire=-0.6, 0, 0",
            "crossbow|RifleAim=12.8, -11.4, 0",
            "crossbow|RifleCrouchAim@crouch=12.6, -5.2, 0",
            "folk_17|PistolFire=9.9, 0, 0",
            "folk_17|PistolFire@crouch=4.2, 0.6, 0",
            "m16a1|RifleAim=12.9, -30, 0",
            "m16a1|RifleCrouchAim@crouch=15, -12.2, 0",
            "redmark_m11_scoped|RifleAim=11.2, -6.6, 0",
            "redmark_m11_scoped|RifleCrouchAim@crouch=15, -17.8, 0",
            "redmark_m11|RifleAim=10.1, 9, 0",
            "rochester_m24_chopped|RifleAim=7, -1.4, 0",
            "rochester_m24|RifleAim=11.6, -15.4, 0",
            "rochester_m24|RifleCrouchAim@crouch=11.4, -3.4, 0",
            "slamberg_500_chopped|RifleAim=12.3, -8.2, 0",
            "slamberg_500_chopped|RifleCrouchAim@crouch=15, -8.4, 0",
            "slamberg_500|RifleAim=11.6, -10, -2.8",
            "slamberg_500|RifleCrouchAim@crouch=17.7, -19.4, 0",
            "slamfire_shotgun|RifleAim=8.1, -17.2, 0",
            "slamfire_shotgun|RifleCrouchAim@crouch=12, -17.6, 0"
        };
        private static Dictionary<string, float[]> _builtin;
        private static float[] Builtin(string key)
        {
            if (_builtin == null)
            {
                _builtin = new Dictionary<string, float[]>();
                foreach (var line in BuiltinData)
                {
                    int eq = line.IndexOf('='); if (eq <= 0) continue;
                    var p = line.Substring(eq + 1).Split(',');
                    if (p.Length < 3) continue;
                    var v = new float[13]; bool ok = true;
                    for (int i = 0; i < 3; i++) ok &= float.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
                    if (ok) _builtin[line.Substring(0, eq).Trim()] = v;
                }
            }
            float[] r; return _builtin.TryGetValue(key, out r) ? r : null;
        }
        private static string _file;
        public static void Load(string dir)
        {
            _file = Path.Combine(dir, "aim-lift.txt");
            EditWeapon = EditClip = null; EditFrame = -1; _armKeys = null; Revision++;
            _v.Clear();
            if (!File.Exists(_file)) return;
            foreach (var raw in File.ReadAllLines(_file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('='); if (eq <= 0) continue;
                var p = line.Substring(eq + 1).Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) continue;
                var v = new float[13]; bool ok = true;
                for (int i = 0; i < Math.Min(p.Length, v.Length); i++) ok &= float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
                if (ok) _v[line.Substring(0, eq).Trim()] = v;
            }
        }
        // (WW) module Shutdown / F7 save button: write only when something was changed since the last save
        internal static bool Dirty;
        internal static void SaveIfDirty() { if (Dirty) Save(); }
        internal static int UserCount { get { return _v.Count; } }
        internal static string FilePath { get { return _file; } }
        public static void Save()
        {
            if (_file == null) return;
            Dirty = false;
            var sb = new StringBuilder();
            sb.AppendLine("# Wild Wasteland (PlayerBody) aim lift: weapon|aim clip[@crouch] = lift (cm), head down (degrees), forward (cm). Edited live with WeaponAdjustment on:");
            sb.AppendLine("# Page Up / Page Down = lift, Home / End = head, Insert / Delete = forward (while she aims, or the aim clip picked with numpad 9/3).");
            sb.AppendLine("# Optional values after forward: left arm up/right (cm), pitch/yaw/roll (degrees), then right arm up/right, pitch/yaw/roll. Missing values are zero; edited with F7 weapon pose buttons.");
            var keys = new List<string>(_v.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys) sb.AppendLine(k + " = " + string.Join(", ", Array.ConvertAll(_v[k], x => x.ToString("0.#", CultureInfo.InvariantCulture))));
            File.WriteAllText(_file, sb.ToString());
        }
        // (2.1.8) standing and crouched have their own values: the key is the clip + "@crouch" when crouched; a crouched aim without its own entry
        // starts from the standing one (so a tuned weapon doesn't jump back to the default)
        public static string Key(string clip, bool crouched) { return crouched ? clip + "@crouch" : clip; }
        // (WW perf) resolved per (weapon, clip, crouched) until the next Set/Load (Revision): Get ran a few concats per call, every frame.
        // The returned arrays are never written by the callers (edits Clone first); Default(clip) is shared per clip from here on.
        private static readonly Dictionary<string, Dictionary<string, float[]>>[] _got = { new Dictionary<string, Dictionary<string, float[]>>(), new Dictionary<string, Dictionary<string, float[]>>() };
        private static int _gotRevision = int.MinValue;
        public static float[] Get(string weapon, string clip, bool crouched)
        {
            if (weapon == null || clip == null) return Resolve(weapon, clip, crouched);
            if (_gotRevision != Revision) { _got[0].Clear(); _got[1].Clear(); _gotRevision = Revision; }
            var byWeapon = _got[crouched ? 1 : 0];
            Dictionary<string, float[]> byClip;
            if (!byWeapon.TryGetValue(weapon, out byClip)) byWeapon[weapon] = byClip = new Dictionary<string, float[]>();
            float[] r;
            if (!byClip.TryGetValue(clip, out r)) byClip[clip] = r = Resolve(weapon, clip, crouched);
            return r;
        }
        private static float[] Resolve(string weapon, string clip, bool crouched)
        {
            float[] v;
            string k = weapon + "|" + Key(clip, crouched), k0 = weapon + "|" + clip;
            if (_v.TryGetValue(k, out v)) return v;
            if ((v = Builtin(k)) != null) return v;
            if (crouched && _v.TryGetValue(k0, out v)) return v;
            if (crouched && (v = Builtin(k0)) != null) return v;
            return Default(clip);
        }
        public static bool Own(string weapon, string clip, bool crouched) { string k = weapon + "|" + Key(clip, crouched); return _v.ContainsKey(k) || Builtin(k) != null; }
        public static void Set(string weapon, string clip, bool crouched, float[] v) { _v[weapon + "|" + Key(clip, crouched)] = v; Dirty = true; Revision++; }

        internal static string EditWeapon, EditClip;
        internal static int Revision;
        internal static int EditFrame = -1;
        internal static bool EditCrouched;
        internal static bool CanEditArms { get { return PlayerBodyModule.Running && Game.Ready && !Game.Dead && !Game.InCar && GunPose.Editing && ThirdPerson.On && EditFrame >= UnityEngine.Time.frameCount - 1 && GunPose.CurrentFrame >= UnityEngine.Time.frameCount - 1 && !string.IsNullOrEmpty(EditWeapon) && !string.IsNullOrEmpty(EditClip); } }
        static float[] _armKeys;
        static int _armKeysRevision;
        static string _armWeapon, _armClip; static bool _armCrouched;
        internal static void EndArmKeys() { if (_armKeys != null) SaveIfDirty(); _armKeys = null; }
        internal static void ArmKeys(bool left, UnityEngine.Vector3 move, UnityEngine.Vector3 rot, float dt)
        {
            if (!CanEditArms || move == UnityEngine.Vector3.zero && rot == UnityEngine.Vector3.zero) { EndArmKeys(); return; }
            if (_armKeys == null || _armWeapon != EditWeapon || _armClip != EditClip || _armCrouched != EditCrouched || _armKeysRevision != Revision)
            {
                EndArmKeys(); _armWeapon = EditWeapon; _armClip = EditClip; _armCrouched = EditCrouched;
                _armKeys = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone(); Set(EditWeapon, EditClip, EditCrouched, _armKeys); _armKeysRevision = Revision;
            }
            int i = left ? 3 : 8;
            _armKeys[i] = UnityEngine.Mathf.Clamp(_armKeys[i] + move.y * dt, -45f, 45f);
            _armKeys[i + 1] = UnityEngine.Mathf.Clamp(_armKeys[i + 1] + move.x * dt, -45f, 45f);
            _armKeys[i + 2] = UnityEngine.Mathf.Clamp(_armKeys[i + 2] - rot.x * dt, -90f, 90f);
            _armKeys[i + 3] = UnityEngine.Mathf.Clamp(_armKeys[i + 3] + rot.y * dt, -90f, 90f);
            _armKeys[i + 4] = UnityEngine.Mathf.Clamp(_armKeys[i + 4] + rot.z * dt, -90f, 90f);
            Dirty = true;
        }
        internal static void NudgeArm(bool left, int axis, float delta)
        {
            if (!CanEditArms) return;
            var v = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone();
            int i = 3 + (left ? 0 : 5) + axis;
            float limit = axis < 2 ? 45f : 90f;
            v[i] = Math.Max(-limit, Math.Min(limit, v[i] + delta));
            Set(EditWeapon, EditClip, EditCrouched, v); Save();
        }
        internal static void ResetArm(bool left)
        {
            if (!CanEditArms) return;
            var v = (float[])Get(EditWeapon, EditClip, EditCrouched).Clone();
            Array.Clear(v, 3 + (left ? 0 : 5), 5);
            Set(EditWeapon, EditClip, EditCrouched, v); Save();
        }

        // ---- two-bone IK: the new elbow position so the hand reaches t, the arm bending in the plane it bends in now
        //   s, e, h = shoulder, elbow, hand positions; returns the elbow (the caller turns the upper arm onto it, then the forearm onto t)
        // (WW perf) the same math on plain doubles, no arrays (same operations in the same order as the array version below)
        public static void Elbow(double sx, double sy, double sz, double ex, double ey, double ez, double hx, double hy, double hz, double tx, double ty, double tz,
                                 out double rx, out double ry, out double rz)
        {
            rx = ex; ry = ey; rz = ez;
            double dx = sx - ex, dy = sy - ey, dz = sz - ez;
            double a = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx = ex - hx; dy = ey - hy; dz = ez - hz;
            double b = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double stx = tx - sx, sty = ty - sy, stz = tz - sz;
            double stl = Math.Sqrt(stx * stx + sty * sty + stz * stz), c = stl;
            if (c < 1e-6 || a < 1e-6 || b < 1e-6) return;
            c = Math.Max(Math.Abs(a - b) + 1e-4, Math.Min(a + b - 1e-4, c));
            double k = 1.0 / stl;
            double ux = stx * k, uy = sty * k, uz = stz * k;
            double sex = ex - sx, sey = ey - sy, sez = ez - sz;
            double dot = sex * ux + sey * uy + sez * uz;
            double px = sex - ux * dot, py = sey - uy * dot, pz = sez - uz * dot;
            double pl = Math.Sqrt(px * px + py * py + pz * pz);
            if (pl < 1e-6)
            {
                double m = -uy;
                px = 0.0 - ux * m; py = -1.0 - uy * m; pz = 0.0 - uz * m;
                pl = Math.Sqrt(px * px + py * py + pz * pz);
                if (pl < 1e-6) return;
            }
            k = 1.0 / pl;
            px = px * k; py = py * k; pz = pz * k;
            double cosA = Math.Max(-1.0, Math.Min(1.0, (a * a + c * c - b * b) / (2 * a * c))), sinA = Math.Sqrt(1 - cosA * cosA);
            double ka = a * cosA, kb = a * sinA;
            rx = sx + (ux * ka + px * kb); ry = sy + (uy * ka + py * kb); rz = sz + (uz * ka + pz * kb);
        }
        public static double[] Elbow(double[] s, double[] e, double[] h, double[] t)
        {
            double a = Dist(s, e), b = Dist(e, h);
            var st = Sub(t, s); double c = Len(st);
            if (c < 1e-6 || a < 1e-6 || b < 1e-6) return (double[])e.Clone();
            c = Math.Max(Math.Abs(a - b) + 1e-4, Math.Min(a + b - 1e-4, c));
            var u = Mul(st, 1.0 / Len(st));
            var se = Sub(e, s);
            var p = Sub(se, Mul(u, Dot(se, u)));          // where the elbow sticks out from the shoulder-target line
            double pl = Len(p);
            if (pl < 1e-6) { p = Sub(new[] { 0.0, -1.0, 0.0 }, Mul(u, -u[1])); pl = Len(p); if (pl < 1e-6) return (double[])e.Clone(); }
            p = Mul(p, 1.0 / pl);
            double cosA = Math.Max(-1.0, Math.Min(1.0, (a * a + c * c - b * b) / (2 * a * c))), sinA = Math.Sqrt(1 - cosA * cosA);
            return Add(s, Add(Mul(u, a * cosA), Mul(p, a * sinA)));
        }
        static double[] Sub(double[] x, double[] y) { return new[] { x[0] - y[0], x[1] - y[1], x[2] - y[2] }; }
        static double[] Add(double[] x, double[] y) { return new[] { x[0] + y[0], x[1] + y[1], x[2] + y[2] }; }
        static double[] Mul(double[] x, double k) { return new[] { x[0] * k, x[1] * k, x[2] * k }; }
        static double Dot(double[] x, double[] y) { return x[0] * y[0] + x[1] * y[1] + x[2] * y[2]; }
        static double Len(double[] x) { return Math.Sqrt(Dot(x, x)); }
        static double Dist(double[] x, double[] y) { return Len(Sub(x, y)); }
    }
}
