using System;
using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;
using WildWasteland.Modules.Guns;

namespace WildWasteland.Modules.NPC
{
    /// <summary>The module's F7 pages (group "NPC AI & navigation"): live values of every subsystem on top, its F7 settings below.</summary>
    internal static partial class NpcPages
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
                    if (!NpcaiRuntime.Running) l.Text("The NPC AI module is not running: no live values (switch it on in F6 or Wild Wasteland > Modules).");
                    else _live(l);
                }
                if (_cfg != null) _cfg.Draw(l);
            }
        }

        /// <summary>Invariant integer text.</summary>
        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }
        /// <summary>Invariant number text with a format.</summary>
        static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        /// <summary>Full section name "NPC / s" of the old entry shell (Presentation part pages).</summary>
        static string Sec(string s) { return "NPC / " + s; }
        /// <summary>Full section name "NPCAI / s".</summary>
        static string NSec(string s) { return "NPCAI / " + s; }

        // ---- pages / page parts owned by other target modules (Wave A split); called where the moved lines were
        /// <summary>Presentation: the "Female appearance & voices" config page.</summary>
        static partial void AddPresentationPages(NpcModule m, List<ToolPage> pages);
        /// <summary>Tools: the "Pathfinding baked data" block of the Debug page (map highlight, reset / export of maps).</summary>
        static partial void DrawToolsMapData(ToolLayout l);
        /// <summary>Tools: the "POI pathfinding" page (editor / setup).</summary>
        static partial void AddToolsPages(NpcModule m, List<ToolPage> pages);

        /// <summary>F7 pages of the old NPC entry shell: NPC population (Women of Wasteland config), then the Presentation and Tools pages.</summary>
        internal static IEnumerable<ToolPage> BuildShell(NpcModule m)
        {
            var pages = new List<ToolPage>();
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "NPC population", m.Config.Main, Sec("Women of Wasteland"))
            { Intro = "Female Flexa/Sprokka population and crew conversion. Existing females retain their identity when new conversion is off. Disable standalone Women of Wasteland and restart to use these built-in controls." });
            AddPresentationPages(m, pages);
            pages.Add(new ConfigPage(m, ToolGroups.Npc, "NPC visuals", m.Config.Tools, Sec("Brain"), Sec("NpcAim"), Sec("Behaviour"))
            { Intro = "Gunman animation (Apocaplayer clips, aim pose), reload sounds and the lying-dog pose. These entries stay in the old [NPC / ...] sections until Presentation takes them over." });
            AddToolsPages(m, pages);
            return pages;
        }

        /// <summary>The NPCAI module's F7 pages (live values on top, tuning sections below) in their old order.</summary>
        internal static void Build(IModule m, List<ToolPage> pages)
        {

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
                l.Value("Animation framework", ApBody.Available ? "in use (gunmen use the player's clips)" : "not available (game clips)");   // refs-ok: REFACTOR-REQUEST B07-11/B07-13 (Compatibility / Presentation status query without contract member)
                l.Value("Guns integration", Services.Guns != null ? (WeaponRanges.ProjectilesEnabled ? "Guns module, projectiles on" : "Guns module, vanilla hitscan") : "off (fallback ranges)");

            }));

            pages.Add(new LivePage(m, "Detection", "Sight cones per creature group, light, hearing ranges, ghosts, taunts, the player's shout, engines and explosions (F6 has SightCone / SightRange / DarkSightRange / FootstepRadius).", l =>
            {
                l.Value("Agents / alert / ghosts", I(Senses.AgentCount) + " / " + I(Senses.AlertCount) + " / " + I(Senses.GhostCount));
                l.Value("Light now", F(Senses.LightLevel(), "0.00") + " (sight " + F(Senses.SightRange(), "0") + " m)");
                l.Value("Storms", NpcaiSettings.StormsEnabled.Value ? I(Storm.StormCount) + " sandstorm(s), " + I(Storm.FunnelCount) + " funnel(s)" + (Storm.DustBridge ? (Storm.Dust ? ", ApocaDustStorm dust active" : ", ApocaDustStorm bridge on") : "") : "off (F6 Storms)");
                l.Value("Shout", NpcaiSettings.ShoutModifier.Value + " + " + NpcaiSettings.ShoutKey.Value + " ([Keys] Shout)");
            }, NSec("Detection"), NSec("Senses")));

            pages.Add(new LivePage(m, "Navigation & baking", "Vanilla POI maps load from the shipped pack. Custom or missing maps bake once and persist in config/WildWasteland/NPCAI/NavMaps/Custom. Reset/export controls are on the NPC Debug page.", l =>
            {
                l.Value("Structures", I(Nav.StructureCount) + " found, " + I(Nav.BakedCount) + " baked");
                l.Value("Loaded maps", I(Nav.LoadedMapCount) + " (" + I(Nav.VanillaMapCount) + " vanilla / " + I(Nav.LoadedMapCount - Nav.VanillaMapCount) + " custom cache)");
                l.Value("Baking", Nav.BakingText);
                l.Value("ms per cell", F(Nav.MsPerCell, "0.000"));
                l.Value("Exit picks", I(Nav.PickQueued) + " queued / running (1 ms a frame); move masks " + I(Nav.MaskedCount) + " of " + I(Nav.BakedCount) + " baked maps");
                l.Value("Friends passing through", I(Passthrough.Pairs) + " pair(s)");
            }, NSec("Nav")));

            pages.Add(new LivePage(m, "Brain & combat", "How NPCs steer, hold, kneel, take cover and pace their bursts; magazines and reloads.", l =>
            {
                l.Value("Brains", I(Brain.Count) + " registered, " + I(Brain.EngagedCount) + " engaged");
                l.Value("Think interval", F(Brain.Interval, "0.00") + " s" + (NpcaiSettings.ScaleWithActors.Value ? " (scales with engaged NPCs)" : ""));
            }, NSec("Brain"), NSec("NpcAim")));

            pages.Add(new LivePage(m, "Idle & movement", "Camp raiders' way home, camp walks and search rounds; the speed rules while turning and the blocked check.", l =>
            {
                l.Value("Idle controllers", I(Idle.Count) + " (" + I(Idle.Walking) + " walking)");
            }, NSec("Idle"), NSec("Movement")));

            pages.Add(new LivePage(m, "Animals", "Burrowing / hunting scorpions, ambush predators, pouncing spiders, dogs (bites, retargeted animations, Dog Companion). The feature switches are in F6.", l =>
            {
                l.Value("Burrowers", I(Burrow.Count) + " (" + I(Burrow.Hidden) + " under the sand now)");
                l.Value("Pouncers", I(Pounce.Count) + " (" + I(Pounce.Airborne) + " in the air)");
                l.Value("Dogs", I(DogBite.Count) + " (" + I(DogAnimations.DogCount) + " animated)");
                l.Value("Dog animation bundle", DogAnimations.BundleLoaded ? I(DogAnimations.ClipCount) + " clips (NPC/Models/npcai_dogs.bundle)" : "not loaded - procedural bite");
                l.Value("Dog Companion", DogCompanionCompat.Present ? (NpcaiSettings.DogCompanionCompatEnabled.Value ? I(DogCompanionCompat.Count) + " companion(s) driven" : "installed, compatibility off (F6)") : "not installed");   // refs-ok: REFACTOR-REQUEST B07-11/B07-13 (Compatibility / Presentation status query without contract member)
            }, NSec("Behaviour"), NSec("Burrow"), NSec("Hunt"), NSec("Ambush"), NSec("Pounce"), NSec("Dogs")));

            pages.Add(new LivePage(m, "Hazards (nests, webs, storms)", "Spider nests and their webs (built in slices while you are far away, layouts kept in config/WildWasteland/NPCAI/nest-layouts.txt) and sandstorms.", l =>
            {
                l.Value("Nests", I(Nests.Count) + " (" + I(Nests.Ready) + " built), " + I(Nests.Pending) + " spider(s) not placed yet");
                l.Value("Webs", I(Webs.Count));
                l.Value("Storms", NpcaiSettings.StormsEnabled.Value ? I(Storm.StormCount) + " sandstorm(s), " + I(Storm.FunnelCount) + " funnel(s)" : "off (F6 Storms)");
            }, NSec("Nests"), NSec("Webs")));

        }
    }
}
