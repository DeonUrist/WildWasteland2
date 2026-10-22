using System;
using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    /// <summary>The module's F7 pages (group "NPC AI & navigation"): live values of every subsystem on top, its F7 settings below.</summary>
    internal static class NpcPages
    {
        /// <summary>Live lines (optional) followed by a ConfigPage over tools.cfg sections.</summary>
        private sealed class LivePage : ToolPage
        {
            readonly Action<ToolLayout> _live;
            readonly ConfigPage _cfg;
            public LivePage(IModule owner, string title, string intro, Action<ToolLayout> live, params string[] sections)
            {
                Owner = owner; Group = ToolGroups.Npc; Title = title; _live = live;
                if (sections != null && sections.Length > 0) _cfg = new ConfigPage(owner, ToolGroups.Npc, title, ((ModuleBase)owner).Config.Tools, sections) { Intro = intro };
            }
            public override void Draw(ToolLayout l)
            {
                if (_live != null)
                {
                    if (!NpcModule.Running) l.Text("The NPC module is not running: no live values (switch it on in F6 or Wild Wasteland > Modules).");
                    else _live(l);
                }
                if (_cfg != null) _cfg.Draw(l);
            }
        }

        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        static string Sec(string s) { return "NPC / " + s; }

        // per-job time of this module (avg ms per frame since the last reset, worst ms)
        static void Jobs(ToolLayout l, IModule m)
        {
            double frames = Math.Max(1, Scheduler.Frames), total = 0, worst = 0;
            foreach (var j in Scheduler.AllJobs())
            {
                if (j.Owner != m) continue;
                total += j.TotalMs; if (j.MaxMs > worst) worst = j.MaxMs;
                l.Value(j.Name, F(j.TotalMs / frames, "0.000") + " ms avg, last " + F(j.LastMs, "0.000") + ", worst " + F(j.MaxMs, "0.000"));
            }
            l.Value("All NPC jobs", F(total / frames, "0.000") + " ms/frame avg, worst job " + F(worst, "0.000") + " ms");
        }

        internal static IEnumerable<ToolPage> Build(NpcModule m)
        {
            var pages = new List<ToolPage>();
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "NPC population", m.Config.Main, Sec("Women of Wasteland"))
            { Intro = "Female Flexa/Sprokka population and crew conversion. Existing females retain their identity when new conversion is off. Disable standalone Women of Wasteland and restart to use these built-in controls." });
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "Female appearance & voices", m.Config.Main, Sec("Gungirl"), Sec("Shoota"))
            { Intro = "Women of Wasteland models, textures, accessories and voice settings. Paths are relative to plugins/WildWasteland/NPC. Model, texture and voice changes require a restart." });

            pages.Add(new LivePage(m, "AI overview", null, l =>
            {
                l.Text("NPC Overhaul inside Wild Wasteland: senses, structure maps, brains, idle walks, animals and hazards. Times are from the scheduler (reset them on Debug & performance > Performance).");
                l.Heading("Live");
                l.Value("Agents (senses)", I(Senses.AgentCount) + " registered, " + I(Senses.AlertCount) + " alert, " + I(Senses.GhostCount) + " ghosts");
                l.Value("Brains", I(Brain.Count) + " registered, " + I(Brain.EngagedCount) + " engaged, think every " + F(Brain.Interval, "0.00") + " s");
                l.Value("Structures", I(Nav.StructureCount) + " found, " + I(Nav.BakedCount) + " baked");
                l.Value("Baking", Nav.BakingText);
                l.Value("Bake cost", F(Nav.MsPerCell, "0.000") + " ms per cell" + (Nav.Needed ? " (an NPC waits: x4 budget)" : Nav.Urgent ? " (player at it: x3 budget)" : ""));
                l.Value("Idle controllers", I(Idle.Count) + " (" + I(Idle.Walking) + " walking)");
                l.Value("Animation framework", ApBody.Available ? "in use (gunmen use the player's clips)" : "not available (game clips)");
                l.Value("Guns integration", Services.Guns != null ? (WeaponRanges.ProjectilesEnabled ? "Guns module, projectiles on" : "Guns module, vanilla hitscan") : "off (fallback ranges)");
                l.Heading("Debug overlay");
                l.Toggle(NpcModule.ShowNav, "Show navigation, ghosts and NPC states in the world");
                l.Toggle(NpcModule.NavDump, "NavDump: map pictures (BMP) to config/WildWasteland/NPC/NavDump");
                l.Toggle(NpcModule.VerboseLog, "Verbose NPC log");
                l.Heading("Frame time");
                Jobs(l, m);
            }));

            pages.Add(new LivePage(m, "Detection", "Sight cones per creature group, light, hearing ranges, ghosts, taunts, the player's shout, engines and explosions (F6 has SightCone / SightRange / DarkSightRange / FootstepRadius).", l =>
            {
                l.Value("Agents / alert / ghosts", I(Senses.AgentCount) + " / " + I(Senses.AlertCount) + " / " + I(Senses.GhostCount));
                l.Value("Light now", F(Senses.LightLevel(), "0.00") + " (sight " + F(Senses.SightRange(), "0") + " m)");
                l.Value("Storms", NpcModule.StormsEnabled.Value ? I(Storm.StormCount) + " sandstorm(s), " + I(Storm.FunnelCount) + " funnel(s)" + (Storm.DustBridge ? (Storm.Dust ? ", ApocaDustStorm dust active" : ", ApocaDustStorm bridge on") : "") : "off (F6 Storms)");
                l.Value("Shout", NpcModule.ShoutModifier.Value + " + " + NpcModule.ShoutKey.Value + " ([Keys] Shout)");
            }, Sec("Detection"), Sec("Senses")));

            pages.Add(new LivePage(m, "Navigation & baking", "Vanilla POI maps load from the shipped pack. Custom or missing maps bake once and persist in config/WildWasteland/NPC/NavMaps/Custom. Reset/export controls are on the NPC Debug page.", l =>
            {
                l.Value("Structures", I(Nav.StructureCount) + " found, " + I(Nav.BakedCount) + " baked");
                l.Value("Loaded maps", I(Nav.LoadedMapCount) + " (" + I(Nav.VanillaMapCount) + " vanilla / " + I(Nav.LoadedMapCount - Nav.VanillaMapCount) + " custom cache)");
                l.Value("Baking", Nav.BakingText);
                l.Value("ms per cell", F(Nav.MsPerCell, "0.000"));
                l.Value("Exit picks", I(Nav.PickQueued) + " queued / running (1 ms a frame); move masks " + I(Nav.MaskedCount) + " of " + I(Nav.BakedCount) + " baked maps");
                l.Value("Friends passing through", I(Passthrough.Pairs) + " pair(s)");
            }, Sec("Nav")));

            pages.Add(new LivePage(m, "Brain & combat", "How NPCs steer, hold, kneel, take cover and pace their bursts; magazines and reloads.", l =>
            {
                l.Value("Brains", I(Brain.Count) + " registered, " + I(Brain.EngagedCount) + " engaged");
                l.Value("Think interval", F(Brain.Interval, "0.00") + " s" + (NpcModule.ScaleWithActors.Value ? " (scales with engaged NPCs)" : ""));
            }, Sec("Brain"), Sec("NpcAim")));

            pages.Add(new LivePage(m, "Idle & movement", "Camp raiders' way home, camp walks and search rounds; the speed rules while turning and the blocked check.", l =>
            {
                l.Value("Idle controllers", I(Idle.Count) + " (" + I(Idle.Walking) + " walking)");
            }, Sec("Idle"), Sec("Movement")));

            pages.Add(new LivePage(m, "Animals", "Burrowing / hunting scorpions, ambush predators, pouncing spiders, dogs (bites, retargeted animations, Dog Companion). The feature switches are in F6.", l =>
            {
                l.Value("Burrowers", I(Burrow.Count) + " (" + I(Burrow.Hidden) + " under the sand now)");
                l.Value("Pouncers", I(Pounce.Count) + " (" + I(Pounce.Airborne) + " in the air)");
                l.Value("Dogs", I(DogBite.Count) + " (" + I(DogAnimations.DogCount) + " animated)");
                l.Value("Dog animation bundle", DogAnimations.BundleLoaded ? I(DogAnimations.ClipCount) + " clips (NPC/Models/npcai_dogs.bundle)" : "not loaded - procedural bite");
                l.Value("Dog Companion", DogCompanionCompat.Present ? (NpcModule.DogCompanionCompatEnabled.Value ? I(DogCompanionCompat.Count) + " companion(s) driven" : "installed, compatibility off (F6)") : "not installed");
            }, Sec("Behaviour"), Sec("Burrow"), Sec("Hunt"), Sec("Ambush"), Sec("Pounce"), Sec("Dogs")));

            pages.Add(new LivePage(m, "Hazards (nests, webs, storms)", "Spider nests and their webs (built in slices while you are far away, layouts kept in config/WildWasteland/NPC/nest-layouts.txt) and sandstorms.", l =>
            {
                l.Value("Nests", I(Nests.Count) + " (" + I(Nests.Ready) + " built), " + I(Nests.Pending) + " spider(s) not placed yet");
                l.Value("Webs", I(Webs.Count));
                l.Value("Storms", NpcModule.StormsEnabled.Value ? I(Storm.StormCount) + " sandstorm(s), " + I(Storm.FunnelCount) + " funnel(s)" : "off (F6 Storms)");
            }, Sec("Nests"), Sec("Webs")));

            pages.Add(new LivePage(m, "Debug", "World overlay, map pictures and the NPC log. Hunting a problem: VerboseLog + NavDump, reproduce, then read BepInEx/LogOutput.log and the BMPs.", l =>
            {
                l.Toggle(NpcModule.ShowNav, "Show navigation, ghosts and NPC states in the world");
                l.Value("Senses", Senses.Status());
                l.Value("Brain", Brain.Status());
                l.Value("Nav", Nav.Status());
                if (l.Button("Write the NPC status to the log", true, false, 360))
                    Core.Log.Always("[NPC] senses " + Senses.Status() + "; brain " + Brain.Status() + "; nav " + Nav.Status() + "; baking " + Nav.BakingText + "; nests " + Nests.Count + "; webs " + Webs.Count);
                l.Space(); l.Heading("Pathfinding baked data");
                l.Toggle(NpcModule.HighlightUnbakedMapPois, "Highlight unbaked POIs on map (light purple)");
                if (l.Button("Reset custom pathfinding baked data", true, false, 430)) Nav.ResetCustomMaps();
                if (l.Button("Export vanilla pathfinding maps", true, false, 430)) Nav.ExportVanillaMaps();
                l.Text("Reset clears locally baked maps and rebuilds nearby custom maps. Shipped vanilla maps and vanilla exports are retained. Export collects release candidates from a clean vanilla setup.");
                if (!string.IsNullOrEmpty(Nav.MapActionStatus)) l.Text(Nav.MapActionStatus);
            }, Sec("Debug")) { IsTool = true });

            pages.Add(new ActionPage(m, ToolGroups.Debug, "POI pathfinding", PoiNavEditor.DrawPage) { DebugOnly = true, IsTool = true });
            return pages;
        }
    }
}
