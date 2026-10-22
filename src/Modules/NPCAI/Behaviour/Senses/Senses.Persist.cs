using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Modules.NPC
{
    /// <summary>Senses part: the per-slot senses sidecar: written on save, restored after a load once the saved NPCs are back.</summary>
    internal static partial class Senses
    {
        /// <summary>(WW) Core.SaveBus.StateChanged: the SaveLoadGame state changes the old Persist.Tick polled for itself (same reactions, same order)</summary>
        internal static void OnSaveLoadState(string previous, string current)
        {
            if (!NpcaiRuntime.Running || current == null) return;
            Persist.OnState(current);
        }

        // ---------- persistence ----------
        // A sidecar per save slot (BepInEx/config/WildWasteland/NPCAI/Saves/<SaveGameN.es3>.senses.txt)
        // written when the game saves, read after a load once the NPCs (found by their saved names) are back. NPC names are unique per world
        // (the Health FSM numbers them). File format unchanged (v1).
        internal static class Persist
        {
            private static string _loadSlot; private static bool _restoring, _loadPending;
            private static Coroutine _co;
            private static int _gen;        // bumped by every reset / clear / new load: a running Restore sees it and stops
            /// <summary>True while a load's senses restore is matching saved NPCs.</summary>
            internal static bool Restoring { get { return _restoring; } }

            /// <summary>Scene load: forgets the pending load and cancels a running restore.</summary>
            internal static void ResetForScene() { _loadSlot = null; _restoring = false; _loadPending = false; _gen++; }
            /// <summary>Module stop: cancels a running restore coroutine.</summary>
            internal static void Shutdown() { _gen++; _restoring = false; _loadPending = false; if (_co != null) { Core.Scheduler.StopCoroutine(_co); _co = null; } }

            /// <summary>SaveLoadGame state change: SaveGame writes the sidecar, LoadGame arms the restore, setSeed clears, the first isPlay after a load starts the restore.</summary>
            internal static void OnState(string state)
            {
                try
                {
                    if (state == "SaveGame") Save(Slot());
                    if (state == "LoadGame") { _loadSlot = Slot(); _loadPending = true; _gen++; _restoring = false; }   // a new load cancels a restore in progress
                    if (state == "setSeed") Clear();
                    if (state == "isPlay" && _loadPending && !_restoring)
                    {
                        _loadPending = false;
                        // the slot read while loading can be stale (SaveFile may still name the previous slot): read it again now that the game is in play
                        string s = Slot(); if (string.IsNullOrEmpty(s)) s = _loadSlot;
                        _loadSlot = null;
                        if (!string.IsNullOrEmpty(s)) _co = Core.Scheduler.StartCoroutine(Restore(s));
                    }
                }
                catch (Exception e) { Core.SaveBus.ReportFailure(NpcaiRuntime.Module, "NPC senses persistence failed: " + e.Message); NpcaiRuntime.Error("Senses persistence: " + e); }
            }

            /// <summary>Current save slot file name (ES3 default path, else the SaveLoadGame SaveFile variable).</summary>
            private static string Slot()
            {
                try { string p = ES3Settings.defaultSettings.path; if (!string.IsNullOrEmpty(p)) return Path.GetFileName(p); } catch (Exception) { }
                var slg = Core.GameRefs.SaveLoadGame;
                var v = slg != null ? slg.FsmVariables.GetFsmString("SaveFile") : null;
                return v != null ? v.Value : null;
            }

            // the save file names Saves writes or the game's own slots (same rule as Saves' SaveRuntime.IsSaveFile / SaveBus.Slot)
            private static readonly System.Text.RegularExpressions.Regex SlotFile = new System.Text.RegularExpressions.Regex(@"^(SaveGame([1-9]|10)|WW_(live|save|auto)-[a-fA-F0-9]{32})\.es3$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            /// <summary>Sidecar path config/WildWasteland/NPCAI/Saves/&lt;slot&gt;.senses.txt for a valid save file name (false otherwise).</summary>
            private static bool TryPath(string slot, out string path)
            {
                path = null;
                string name = Path.GetFileName(slot ?? "");
                if (SlotFile.IsMatch(name)) { path = Path.Combine(Path.Combine(NpcaiRuntime.DataDir ?? Core.Assets.ModuleConfigDir("NPCAI"), "Saves"), name + ".senses.txt"); return true; }
                if (string.IsNullOrEmpty(name) || !name.StartsWith("SaveGame", StringComparison.OrdinalIgnoreCase)) return false;
                foreach (char c in name) if (!(char.IsLetterOrDigit(c) || c == '.')) return false;
                path = Path.Combine(Path.Combine(NpcaiRuntime.DataDir ?? Core.Assets.ModuleConfigDir("NPCAI"), "Saves"), name + ".senses.txt");
                return true;
            }

            /// <summary>New world: every agent's alert state and every ghost are dropped.</summary>
            private static void Clear()
            {
                _gen++;
                foreach (var kv in _agents) { var a = kv.Value; a.State = State.Idle; a.Target = null; a.Ghost = null; a.GhostPrio = Src.None; a.Heard.Clear(); a.Done.Clear(); }
                for (int i = _ghosts.Count - 1; i >= 0; i--) KillGhost(i);
            }

            /// <summary>Writes the senses sidecar of a slot (v1: ghosts and alert agents with targets, ghosts and search time) through an atomic replace.</summary>
            private static void Save(string slot)
            {
                string path;
                if (!TryPath(slot, out path)) { NpcaiRuntime.Warn("Senses: no save slot to write the senses for (" + slot + ")"); return; }
                var sb = new StringBuilder();
                var ci = CultureInfo.InvariantCulture;
                float now = Time.time;
                sb.Append("v1 ").Append(Path.GetFileName(slot)).Append('\n');
                foreach (var g in _ghosts)
                    sb.Append("ghost ").Append(g.Id).Append(' ').Append(g.Pos.x.ToString("0.00", ci)).Append(' ').Append(g.Pos.y.ToString("0.00", ci)).Append(' ').Append(g.Pos.z.ToString("0.00", ci))
                      .Append(' ').Append((int)g.Src).Append(' ').Append((now - g.Born).ToString("0", ci)).Append(' ').Append(g.About.Replace('\n', ' ')).Append('\n');
                int n = 0;
                foreach (var kv in _agents)
                {
                    var a = kv.Value;
                    if (a.Owner == null || a.State == State.Idle) continue;
                    string target = a.Target == null ? "-" : (a.Target == _player ? "Player" : a.Target.name);
                    sb.Append("agent ").Append(a.Owner.name.Replace(' ', '_')).Append(' ').Append((int)(a.State == State.Hide ? State.Combat : a.State)).Append(' ').Append(target.Replace(' ', '_')).Append(' ')
                      .Append(a.Ghost != null ? a.Ghost.Id : -1).Append(' ').Append((int)a.GhostPrio).Append(' ').Append(Mathf.Max(0f, a.SearchUntil - now).ToString("0.0", ci)).Append(' ')
                      .Append(a.LastSeen.x.ToString("0.00", ci)).Append(' ').Append(a.LastSeen.y.ToString("0.00", ci)).Append(' ').Append(a.LastSeen.z.ToString("0.00", ci)).Append('\n');
                    n++;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", sb.ToString());
                Core.AtomicFile.Replace(path + ".tmp", path);   // (D17) same-directory replace, no Delete + Move window
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: saved " + _ghosts.Count + " ghost(s), " + n + " alert NPC(s) for " + Path.GetFileName(slot));
            }

            /// <summary>Restores the senses sidecar after a load: recreates the ghosts, then matches saved agents by name for up to 20 s as the NPCs register.</summary>
            private static IEnumerator Restore(string slot)
            {
                _restoring = true;
                string path;
                if (!TryPath(slot, out path)) { _restoring = false; yield break; }
                if (!File.Exists(path)) { _restoring = false; yield break; }
                string[] lines;
                try { lines = File.ReadAllLines(path); } catch (Exception e) { NpcaiRuntime.Warn("Senses: cannot read " + path + ": " + e.Message); _restoring = false; yield break; }
                if (lines.Length == 0 || !lines[0].StartsWith("v1 ")) { _restoring = false; yield break; }
                if (!string.Equals(lines[0].Substring(3).Trim(), Path.GetFileName(slot), StringComparison.OrdinalIgnoreCase))
                { NpcaiRuntime.Warn("Senses: " + path + " was written for another slot, not restored"); _restoring = false; yield break; }
                Clear();
                int gen = _gen;
                _nextPlayerFind = 0f; FindPlayer();
                var ci = CultureInfo.InvariantCulture;
                var ghostById = new Dictionary<int, Ghost>();
                var pending = new List<string[]>();
                float now = Time.time;
                foreach (var line in lines)
                {
                    var p = line.Split(' ');
                    try
                    {
                        if (p[0] == "ghost" && p.Length >= 7)
                        {
                            var g = new Ghost { Id = int.Parse(p[1]), Pos = new Vector3(float.Parse(p[2], ci), float.Parse(p[3], ci), float.Parse(p[4], ci)), Src = (Src)int.Parse(p[5]), Born = now - float.Parse(p[6], ci), Moved = now, About = p.Length > 7 ? string.Join(" ", p, 7, p.Length - 7) : "" };
                            if (_ghostRoot == null) { var r = new GameObject("NPCAI.Ghosts") { hideFlags = HideFlags.HideAndDontSave }; UnityEngine.Object.DontDestroyOnLoad(r); _ghostRoot = r.transform; }
                            g.Obj = new GameObject("NPCAI.Ghost#" + g.Id) { hideFlags = HideFlags.HideAndDontSave };
                            g.Obj.transform.SetParent(_ghostRoot, false); g.Obj.transform.position = g.Pos;
                            if (g.About.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0) g.Subject = _player;
                            _ghosts.Add(g); ghostById[g.Id] = g;
                            if (g.Id >= _nextGhostId) _nextGhostId = g.Id + 1;
                        }
                        else if (p[0] == "agent" && p.Length >= 10) pending.Add(p);
                    }
                    catch (Exception e) { NpcaiRuntime.Warn("Senses: bad line in " + path + ": " + e.Message); }
                }
                float until = Time.realtimeSinceStartup + 20f;
                var byName = new Dictionary<string, Agent>();
                int restored = 0;
                while (pending.Count > 0 && Time.realtimeSinceStartup < until)
                {
                    byName.Clear();
                    foreach (var kv in _agents) if (kv.Value.Owner != null) byName[kv.Value.Owner.name.Replace(' ', '_')] = kv.Value;
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        var p = pending[i];
                        Agent a;
                        if (!byName.TryGetValue(p[1], out a)) continue;
                        pending.RemoveAt(i);
                        try
                        {
                            var st = (State)int.Parse(p[2]);
                            if (a.Spider && st != State.Idle) a.AmbushDist = 0f;
                            a.LastSeen = new Vector3(float.Parse(p[7], ci), float.Parse(p[8], ci), float.Parse(p[9], ci));
                            GameObject target = p[3] == "Player" ? _player : (p[3] == "-" ? null : (byName.ContainsKey(p[3]) ? byName[p[3]].Owner : null));
                            int gid = int.Parse(p[4]); Ghost g; ghostById.TryGetValue(gid, out g);
                            if (st == State.Combat)
                            {
                                if (target != null) { a.State = State.Combat; a.Target = target; a.UnseenFor = 0f; a.UnseenSince = -1f; }
                                else { g = GetOrMake(Src.Vision, a.LastSeen, null, "last seen (before the load)", 2f, 9999f, now); a.State = State.Idle; Assign(a, g, Src.Vision, now); }
                            }
                            else if (g != null)
                            {
                                a.State = State.Idle; Assign(a, g, (Src)int.Parse(p[5]), now);
                                if (st == State.Search) { a.State = State.Search; a.SearchUntil = now + float.Parse(p[6], ci); }
                            }
                            restored++;
                        }
                        catch (Exception e) { NpcaiRuntime.Warn("Senses: restore of " + p[1] + " failed: " + e.Message); }
                    }
                    if (pending.Count > 0) yield return new WaitForSecondsRealtime(0.5f);
                    if (gen != _gen) { if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: restore of " + Path.GetFileName(slot) + " cancelled"); _restoring = false; yield break; }   // a new load / reset happened meanwhile
                }
                for (int i = _ghosts.Count - 1; i >= 0; i--) if (_ghosts[i].Holders.Count == 0) KillGhost(i);
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: restored " + restored + " alert NPC(s), " + _ghosts.Count + " ghost(s) from " + Path.GetFileName(slot) + (pending.Count > 0 ? "; " + pending.Count + " NPC(s) not found" : ""));
                _restoring = false; _co = null;
            }
        }
    }
}
