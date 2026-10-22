// Config-binding harness for Wild Wasteland (see run.sh). Runs ModuleConfig/BindConfig of every module on plain mono:
// anything that throws here throws in Plugin.Awake in the game. Prints: BindConfig OK/FAILED per module, SHARED ENTRY (two
// statics of one module hold the same ConfigEntry = duplicate Bind), FOREIGN SECTION (section not prefixed with the module id),
// cross-module (section,key) collisions on the main and tools files, clashes with Core's own entries, and the ModuleHost.BindAll run.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using WildWasteland.Core;

class StdoutListener : ILogListener
{
    public void LogEvent(object sender, LogEventArgs e) { Console.WriteLine("[" + e.Level + "] " + e.Data); }
    public void Dispose() { }
}

static class Harness
{
    static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : "harness-root");
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "config"));
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "plugins", "WildWasteland"));
        File.WriteAllText(Path.Combine(root, "Apocalypter.exe"), "");
        var setPath = typeof(BepInEx.Paths).GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.NonPublic);
        setPath.Invoke(null, new object[] { Path.Combine(root, "Apocalypter.exe"), null, null, null });
        Logger.Listeners.Add(new StdoutListener());
        Log.Source = Logger.CreateLogSource("WW");
        Log.Verbose = true;
        Assets.Init(Path.Combine(root, "BepInEx", "plugins", "WildWasteland", "WildWasteland.dll"));
        Legacy.FirstRunMain = Legacy.FirstRunTools = true;
        int problems = 0;

        var modules = new IModule[] {
            new WildWasteland.Modules.PlayerBody.PlayerBodyModule(),
            new WildWasteland.Modules.NPC.NpcModule(),
            new WildWasteland.Modules.Guns.GunsModule(),
            new WildWasteland.Modules.HUD.HudModule(),
            new WildWasteland.Modules.Patrols.PatrolsModule() };

        // 1) each module alone on fresh files
        var mainSets = new Dictionary<string, HashSet<string>>();
        var toolSets = new Dictionary<string, HashSet<string>>();
        var asm = typeof(ModuleHost).Assembly;
        foreach (var m in modules)
        {
            Console.WriteLine("==== " + m.Id);
            var main = new ConfigFile(Path.Combine(root, "BepInEx", "config", m.Id + ".main.cfg"), false);
            var tools = new ConfigFile(Path.Combine(root, "BepInEx", "config", m.Id + ".tools.cfg"), false);
            var cfg = (ModuleConfig)Activator.CreateInstance(typeof(ModuleConfig), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { m.Id, main, tools }, null);
            typeof(ModuleConfig).GetProperty("Enabled").SetValue(cfg, main.Bind(m.Id, "Enabled", true, "x"), null);
            typeof(ModuleBase).GetProperty("Config").SetValue(m, cfg, null);
            try { m.BindConfig(cfg); Console.WriteLine("BindConfig OK: main " + main.Count + " tools " + tools.Count); }
            catch (Exception e) { problems++; Console.WriteLine("BindConfig FAILED: " + e); }
            var ms = new HashSet<string>(); foreach (var d in main.Keys) ms.Add(d.Section + "|" + d.Key); mainSets[m.Id] = ms;
            var ts = new HashSet<string>(); foreach (var d in tools.Keys) ts.Add(d.Section + "|" + d.Key); toolSets[m.Id] = ts;
            var owners = new Dictionary<ConfigEntryBase, string>();
            string ns = m.GetType().Namespace;
            foreach (var t in asm.GetTypes())
            {
                if (t.Namespace == null || !t.Namespace.StartsWith(ns)) continue;
                foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!typeof(ConfigEntryBase).IsAssignableFrom(f.FieldType)) continue;
                    var v = f.GetValue(null) as ConfigEntryBase; if (v == null) continue;
                    string me = t.Name + "." + f.Name, other;
                    if (owners.TryGetValue(v, out other)) Console.WriteLine("  SHARED ENTRY (alias or duplicate Bind): " + other + " and " + me + " -> [" + v.Definition.Section + "] " + v.Definition.Key);
                    else owners[v] = me;
                }
            }
            foreach (var d in main.Keys) if (d.Section != "Keys" && !d.Section.StartsWith(m.Id)) { problems++; Console.WriteLine("  FOREIGN MAIN SECTION: [" + d.Section + "] " + d.Key); }
            foreach (var d in tools.Keys) if (!d.Section.StartsWith(m.Id)) { problems++; Console.WriteLine("  FOREIGN TOOLS SECTION: [" + d.Section + "] " + d.Key); }
        }
        // 2) cross-module collisions
        Console.WriteLine("==== cross-module collisions");
        var ids = new List<string>(mainSets.Keys);
        for (int i = 0; i < ids.Count; i++) for (int j = i + 1; j < ids.Count; j++)
        {
            foreach (var k in mainSets[ids[i]]) if (mainSets[ids[j]].Contains(k)) { problems++; Console.WriteLine("  MAIN " + ids[i] + " & " + ids[j] + ": " + k); }
            foreach (var k in toolSets[ids[i]]) if (toolSets[ids[j]].Contains(k)) { problems++; Console.WriteLine("  TOOLS " + ids[i] + " & " + ids[j] + ": " + k); }
        }
        var core = new[] { "General|Apocasetter", "Keys|Tool window", "Debug|VerboseLog" };
        foreach (var id in ids) foreach (var k in core) if (mainSets[id].Contains(k)) { problems++; Console.WriteLine("  CORE MAIN clash " + id + ": " + k); }
        foreach (var id in ids) if (toolSets[id].Contains("Core|Frame budget (ms)")) { problems++; Console.WriteLine("  CORE TOOLS clash " + id); }

        // 3) the real thing: ModuleHost.BindAll on shared files, as Plugin.Awake does
        Console.WriteLine("==== ModuleHost.BindAll (shared files)");
        var mainAll = new ConfigFile(Path.Combine(root, "BepInEx", "config", "all.main.cfg"), true);
        var toolsAll = new ConfigFile(Path.Combine(root, "BepInEx", "config", "all.tools.cfg"), true);
        mainAll.Bind("General", "Apocasetter", true, "x");
        mainAll.Bind("Keys", "Tool window", UnityEngine.InputSystem.Key.F7, "x");
        foreach (var m in modules) ModuleHost.Add(m);
        typeof(ModuleHost).GetMethod("BindAll", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { mainAll, toolsAll });
        mainAll.Bind("Debug", "VerboseLog", false, "x");
        toolsAll.Bind("Core", "Frame budget (ms)", 2f, new ConfigDescription("x", new AcceptableValueRange<float>(0.5f, 8f)));
        foreach (var e in ModuleHost.Entries)
        {
            Console.WriteLine("  " + e.Module.Id + ": " + e.State + (e.LastError != null ? " " + e.LastError : ""));
            if (e.State == ModuleState.InitFailed) problems++;
        }
        Console.WriteLine("main entries " + mainAll.Count + ", tools entries " + toolsAll.Count);
        mainAll.Save(); toolsAll.Save();
        Console.WriteLine(problems == 0 ? "RESULT: OK" : "RESULT: " + problems + " PROBLEM(S)");
        return problems == 0 ? 0 : 1;
    }
}
