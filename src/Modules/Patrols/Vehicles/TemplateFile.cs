using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>
    /// The car template file (JSON): frame, fitted parts, bumpers, trailer, suspension adjustment (assembly structure) plus the crew,
    /// turret, cargo and faction fields Patrols reads. Read/written with Core Json, NOT Unity's JsonUtility (it drops the nested
    /// TemplatePart[] array in a BepInEx plugin). Optional trailer data keeps files without a trailer valid.
    /// </summary>
    public class TemplateFile
    {
        public int schema = 2;
        public float explodeChance=50f;
        public string faction="Scrapyard";
        public string name = "";            // template name (spawner menu, logs); "" = the file name
        public string body = "";            // frame prefab (PipeRat, Poloska, TinyTyrant, Junker, Rustcargo, Halfbreed ...)
        public string kind = "";            // one or more kinds, ';'-separated: "small", "junker", "truck", "small;motorcycle" ...
                                            // ("" = guessed from the body like the built-in park); every listed kind counts
        public bool spawns = true;          // false = only in the spawner menu, never picked by patrols / convoys
        public float weight = 1f;           // relative pick chance among the templates of the same kind (built-ins are 1)
        public string rams = "";            // None | Pedestrians | Cars ("" = Cars for trucks, Pedestrians otherwise)
        public string driver = "";          // crew prefab (Scraffa, Spanna, Sprokka, Boltjaw, Flexa, Lugnut, Scrud); "" = nobody
        public string passenger = "";
        public string rider = "";           // 2.1.0: a blast-lance thrower on the roof (crew prefab); "" = none. Not for trucks / motorcycles
        public float[] riderPos = new float[0];   // frame-local spot of the rider's feet; empty = the top of the car, computed at build
        // 2.2.0 human turrets: rider = turret 1 ("Warboy" = Scraffa with blast lances, or a ranged crew prefab = a gunner); rider2 / rider3 =
        // turrets 2 and 3 (cars only; a motorcycle takes turret 1 only, trucks none), each with its own spot
        public string rider2 = "", rider3 = "";
        public float[] riderPos2 = new float[0], riderPos3 = new float[0];
        public string rider4 = ""; // trailers have four spots; towing vehicles retain their existing limits
        public float[] riderPos4 = new float[0];
        public bool riderStanding, riderStanding2, riderStanding3, riderStanding4; // false = sitting
        public TemplateFile trailer; // its own frame, fitted parts and frame-local turret positions
        /// <summary>Crew prefab of turret slot i (0..3).</summary>
        public string Turret(int i) { CheckSlot(i); return i == 0 ? rider : i == 1 ? rider2 : i == 2 ? rider3 : rider4; }
        /// <summary>Frame-local spot of turret slot i (empty = computed at build).</summary>
        public float[] TurretPos(int i) { CheckSlot(i); return i == 0 ? riderPos : i == 1 ? riderPos2 : i == 2 ? riderPos3 : riderPos4; }
        /// <summary>True when turret slot i stands (false = sits).</summary>
        public bool TurretStanding(int i) { CheckSlot(i); return i == 0 ? riderStanding : i == 1 ? riderStanding2 : i == 2 ? riderStanding3 : riderStanding4; }
        /// <summary>Sets standing/sitting of turret slot i.</summary>
        public void SetTurretStanding(int i, bool value) { CheckSlot(i); if(i==0)riderStanding=value;else if(i==1)riderStanding2=value;else if(i==2)riderStanding3=value;else riderStanding4=value; }
        /// <summary>Sets the crew prefab of turret slot i.</summary>
        public void SetTurret(int i, string v) { CheckSlot(i); v = v ?? ""; if (i == 0) rider = v; else if (i == 1) rider2 = v; else if (i == 2) rider3 = v; else rider4 = v; }
        /// <summary>Sets the frame-local spot of turret slot i.</summary>
        public void SetTurretPos(int i, float[] p) { CheckSlot(i); p = p ?? new float[0]; if (i == 0) riderPos = p; else if (i == 1) riderPos2 = p; else if (i == 2) riderPos3 = p; else riderPos4 = p; }
        /// <summary>Throws for a turret slot outside 0..3.</summary>
        private static void CheckSlot(int i) { if (i < 0 || i >= 4) throw new ArgumentOutOfRangeException(nameof(i)); }
        public string lootPreset = "";
        public string cargo = "";           // "" none | "Random" (rolled by the [Loot] chances) | a loot key (Food ...) | an item spec "a:2;b:1-3"
        public string[] bumpers = new string[0];   // a front bumper rolled per build ("" = none), only if no part sits on hinge_bumper_front
        public bool fillFuel = true, releaseHandbrake = true;
        public float fuelLitres = -1f;
        // 2.3.0: the suspension lift kit's adjustment (Part Adjustment tool): wheel spacing x1..1.5 and the height of the whole assembly
        // in metres (+ = mounts up / body down). Only with a kit on hinge_suspension; standard = 1 / 0 (not written then)
        public float suspensionWidth = 1f, suspensionHeight = 0f;
        public TemplatePart[] parts = new TemplatePart[0];
        public string source = "";          // who wrote it (Apocatemplater version, date, the car it came from) - information only
        /// <summary>True for a cargo truck (Rustcargo body or a "truck" kind): its lootPreset is a fixed cargo option.</summary>
        private bool CargoTruck { get { return string.Equals(body, "Rustcargo", StringComparison.OrdinalIgnoreCase) || Array.Exists((kind ?? "").Split(';', ','), k => k.Trim().Equals("truck", StringComparison.OrdinalIgnoreCase)); } }

        // ------------------------------------------------------------ write
        /// <summary>Writes a 3-float array entry (nothing when it is not 3 long).</summary>
        private static void V3(StringBuilder sb, string key, float[] v)
        {
            if (v == null || v.Length != 3) return;
            sb.Append("    \"").Append(key).Append("\": [").Append(v[0].ToString("0.####", CultureInfo.InvariantCulture)).Append(", ").Append(v[1].ToString("0.####", CultureInfo.InvariantCulture)).Append(", ").Append(v[2].ToString("0.####", CultureInfo.InvariantCulture)).Append("],\n");
        }

        /// <summary>Serialises the template in the stable field order of the existing files.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            P(sb, "schema", schema); P(sb, "name", name); P(sb, "body", body); P(sb, "kind", kind);
            P(sb,"explodeChance",explodeChance);
            P(sb,"faction",faction);
            if (!CargoTruck || !string.IsNullOrEmpty(lootPreset)) P(sb, "lootPreset", lootPreset);   // a truck's lootPreset = a fixed cargo option (editor "E")
            P(sb, "rams", rams); P(sb, "driver", driver); P(sb, "passenger", passenger);
            P(sb, "rider", rider);
            V3(sb, "riderPos", riderPos);
            if (!string.IsNullOrEmpty(rider2)) P(sb, "rider2", rider2);
            V3(sb, "riderPos2", riderPos2);
            if (!string.IsNullOrEmpty(rider3)) P(sb, "rider3", rider3);
            V3(sb, "riderPos3", riderPos3);
            if (!string.IsNullOrEmpty(rider4)) P(sb, "rider4", rider4);
            V3(sb, "riderPos4", riderPos4);
            P(sb,"riderStanding",riderStanding);P(sb,"riderStanding2",riderStanding2);
            P(sb,"riderStanding3",riderStanding3);P(sb,"riderStanding4",riderStanding4);
            if (trailer != null) sb.Append("    \"trailer\": ").Append(trailer.ToJson().TrimEnd()).Append(",\n");

            sb.Append("    \"bumpers\": [");
            for (int i = 0; i < (bumpers ?? new string[0]).Length; i++) { if (i > 0) sb.Append(", "); Json.Str(sb, bumpers[i]); }
            sb.Append("],\n");
            P(sb, "fillFuel", fillFuel); P(sb, "releaseHandbrake", releaseHandbrake);
            if (suspensionWidth != 1f) P(sb, "suspensionWidth", suspensionWidth);
            if (suspensionHeight != 0f) P(sb, "suspensionHeight", suspensionHeight);
            if (fuelLitres >= 0) P(sb, "fuelLitres", fuelLitres);
            sb.Append("    \"parts\": [");
            var ps = parts ?? new TemplatePart[0];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("        { \"parent\": ").Append(p.parent).Append(", \"hinge\": "); Json.Str(sb, p.hinge);
                sb.Append(", \"prefab\": "); Json.Str(sb, p.prefab);
                Arr(sb, "hingePos", p.hingePos); Arr(sb, "hingeRot", p.hingeRot); Arr(sb, "pos", p.pos); Arr(sb, "rot", p.rot);
                sb.Append(" }");
            }
            sb.Append(ps.Length > 0 ? "\n    ],\n" : "],\n");
            sb.Append("    \"source\": "); Json.Str(sb, source); sb.Append("\n}\n");
            return sb.ToString();
        }

        /// <summary>Writes one scalar entry (string, bool, float with InvariantCulture, other).</summary>
        private static void P(StringBuilder sb, string k, object v)
        {
            sb.Append("    \"").Append(k).Append("\": ");
            if (v is string) Json.Str(sb, (string)v);
            else if (v is bool) sb.Append((bool)v ? "true" : "false");
            else if (v is float) sb.Append(((float)v).ToString("0.####", CultureInfo.InvariantCulture));
            else sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
            sb.Append(",\n");
        }

        /// <summary>Writes an inline float array of a part (left out when empty).</summary>
        private static void Arr(StringBuilder sb, string k, float[] a)
        {
            if (a == null || a.Length == 0) return;   // empty = untouched: left out of the file
            sb.Append(", \"").Append(k).Append("\": [");
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(a[i].ToString("0.####", CultureInfo.InvariantCulture)); }
            sb.Append(']');
        }

        // ------------------------------------------------------------ read
        /// <summary>Parses a template file; throws FormatException for invalid content.</summary>
        public static TemplateFile FromJson(string text)
        {
            var root = Json.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new FormatException("the file is not a JSON object");
            return FromObject(root, false);
        }

        /// <summary>Reads a template (or its single nested trailer) from a parsed JSON object.</summary>
        private static TemplateFile FromObject(Dictionary<string, object> root, bool isTrailer)
        {
            var f = new TemplateFile();
            f.schema = (int)Json.Num(root, "schema", f.schema);
            f.name = Json.Text(root, "name", f.name); f.body = Json.Text(root, "body", f.body);
            f.explodeChance=Json.Num(root,"explodeChance",Explode.DefaultChance(f.name));   // refs-ok: REFACTOR-REQUEST B12-1 (Patrols wreck chance default)
            if(float.IsNaN(f.explodeChance)||float.IsInfinity(f.explodeChance))throw new FormatException("Invalid explodeChance");
            f.explodeChance=Math.Max(0f,Math.Min(100f,f.explodeChance));
            f.kind = Json.Text(root, "kind", f.kind);
            f.spawns = Json.Bool(root, "spawns", f.spawns); f.weight = Json.Num(root, "weight", f.weight);
            f.rams = Json.Text(root, "rams", f.rams); f.driver = Json.Text(root, "driver", f.driver); f.passenger = Json.Text(root, "passenger", f.passenger);
            f.rider = Json.Text(root, "rider", f.rider); f.riderPos = Json.Floats(root, "riderPos");
            f.rider2 = Json.Text(root, "rider2", f.rider2); f.riderPos2 = Json.Floats(root, "riderPos2");
            f.rider3 = Json.Text(root, "rider3", f.rider3); f.riderPos3 = Json.Floats(root, "riderPos3");
            f.rider4 = Json.Text(root, "rider4", f.rider4); f.riderPos4 = Json.Floats(root, "riderPos4");
            f.riderStanding=Json.Bool(root,"riderStanding",false);f.riderStanding2=Json.Bool(root,"riderStanding2",false);
            f.riderStanding3=Json.Bool(root,"riderStanding3",false);f.riderStanding4=Json.Bool(root,"riderStanding4",false);
            object trailerObject;
            if (root.TryGetValue("trailer", out trailerObject) && trailerObject != null)
            {
                if (isTrailer || !(trailerObject is Dictionary<string, object>)) throw new FormatException("trailer must be a single trailer object");
                f.trailer = FromObject((Dictionary<string, object>)trailerObject, true);
                if (string.IsNullOrWhiteSpace(f.trailer.body)) throw new FormatException("trailer has no body");
            }
            f.lootPreset = f.CargoTruck ? "" : Json.Text(root, "lootPreset", f.lootPreset);
            f.cargo = Json.Text(root, "cargo", f.cargo);
            f.fillFuel = Json.Bool(root, "fillFuel", f.fillFuel); f.releaseHandbrake = Json.Bool(root, "releaseHandbrake", f.releaseHandbrake);
            f.fuelLitres = Json.Num(root, "fuelLitres", -1f);
            f.suspensionWidth = Json.Num(root, "suspensionWidth", 1f); f.suspensionHeight = Json.Num(root, "suspensionHeight", 0f);
            f.source = Json.Text(root, "source", f.source);
            object o;
            if (root.TryGetValue("bumpers", out o) && o is List<object>)
            {
                var l = new List<string>();
                foreach (var b in (List<object>)o) l.Add(b as string ?? "");
                f.bumpers = l.ToArray();
            }
            if (root.TryGetValue("parts", out o) && o is List<object>)
            {
                var l = new List<TemplatePart>();
                foreach (var e in (List<object>)o)
                {
                    var d = e as Dictionary<string, object>;
                    if (d == null) continue;
                    var p = new TemplatePart();
                    p.parent = (int)Json.Num(d, "parent", p.parent);
                    p.hinge = Json.Text(d, "hinge", p.hinge); p.prefab = Json.Text(d, "prefab", p.prefab);
                    p.hingePos = Json.Floats(d, "hingePos"); p.hingeRot = Json.Floats(d, "hingeRot");
                    p.pos = Json.Floats(d, "pos"); p.rot = Json.Floats(d, "rot");
                    l.Add(p);
                }
                f.parts = l.ToArray();
            }
            f.faction=TrafficFactions.Normalize(Json.Text(root,"faction",CrewFactions.Infer(f)));   // refs-ok: REFACTOR-REQUEST B12-1 (Patrols faction inference)
            return f;
        }
    }

}
