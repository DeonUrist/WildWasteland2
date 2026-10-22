using System;
using System.Collections.Generic;
using System.Globalization;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.NPC
{
    /// <summary>NpcPages part owned by Debug: the overlay / log toggles and job times of "AI overview" and the NPC "Debug" page.</summary>
    internal static partial class NpcPages
    {
        /// <summary>Per-job time of this module (avg ms per frame since the last reset, worst ms).</summary>
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

        /// <summary>End of "AI overview": the debug overlay / NavDump / verbose toggles and the frame time per job.</summary>
        static partial void DrawAiOverviewDebug(ToolLayout l, IModule m)
        {
            l.Heading("Debug overlay");
            l.Toggle(NpcModule.ShowNav, "Show navigation, ghosts and NPC states in the world");
            l.Toggle(NpcModule.NavDump, "NavDump: map pictures (BMP) to config/WildWasteland/NPC/NavDump");
            l.Toggle(NpcModule.VerboseLog, "Verbose NPC log");
            l.Heading("Frame time");
            Jobs(l, m);
        }

        /// <summary>The NPC "Debug" page: overlay switch, Senses / Brain / Nav status, log button; then the Tools baked-data block.</summary>
        static partial void AddDebugPages(NpcModule m, List<ToolPage> pages)
        {
            pages.Add(new LivePage(m, "Debug", "World overlay, map pictures and the NPC log. Hunting a problem: VerboseLog + NavDump, reproduce, then read BepInEx/LogOutput.log and the BMPs.", l =>
            {
                l.Toggle(NpcModule.ShowNav, "Show navigation, ghosts and NPC states in the world");
                l.Value("Senses", Senses.Status());
                l.Value("Brain", Brain.Status());
                l.Value("Nav", Nav.Status());
                if (l.Button("Write the NPC status to the log", true, false, 360))
                    Core.Log.Always("[NPC] senses " + Senses.Status() + "; brain " + Brain.Status() + "; nav " + Nav.Status() + "; baking " + Nav.BakingText + "; nests " + Nests.Count + "; webs " + Webs.Count);
                DrawToolsMapData(l);
            }, Sec("Debug")) { IsTool = true });
        }
    }
}
