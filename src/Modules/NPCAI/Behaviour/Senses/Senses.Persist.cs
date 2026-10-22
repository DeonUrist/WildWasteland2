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
    /// <summary>Senses part: the embedded senses record: written on save, restored after a load once the saved NPCs are back.</summary>
    internal static partial class Senses
    {
        /// <summary>(WW) Core.SaveBus.StateChanged: the SaveLoadGame state changes the old Persist.Tick polled for itself (same reactions, same order)</summary>
        internal static void OnSaveLoadState(string previous, string current)
        {
            if (!NpcaiRuntime.Running || current == null) return;
            Persist.OnState(current);
        }

        // ---------- persistence ----------
        // Stored inside the native world save; NPC names identify restored agents after loading.
        internal static class Persist
        {
            private static string _loadSlot; private static bool _restoring, _loadPending, _loadFailed;
            private static Coroutine _co;
            private static int _gen;        // bumped by every reset / clear / new load: a running Restore sees it and stops
            /// <summary>True while a load's senses restore is matching saved NPCs.</summary>
            internal static bool Restoring { get { return _restoring; } }

            /// <summary>Scene load: forgets the pending load and cancels a running restore.</summary>
            internal static void ResetForScene() { _loadSlot = null; _restoring = false; _loadPending = false; _loadFailed = false; _gen++; }
            /// <summary>Module stop: cancels a running restore coroutine.</summary>
            internal static void Shutdown() { _gen++; _restoring = false; _loadPending = false; if (_co != null) { Core.Scheduler.StopCoroutine(_co); _co = null; } }

            /// <summary>SaveLoadGame state change: LoadGame arms the restore, setSeed clears, the first isPlay after a load starts the restore.</summary>
            internal static void OnState(string state)
            {
                try
                {
                    if (state == "LoadGame") { _loadSlot = Slot(); _loadPending = true; _gen++; _restoring = false; }   // a new load cancels a restore in progress
                    if (state == "setSeed" || state == "Start") { Clear(); _loadFailed = false; Nests.NewWorld(); }
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

            private static readonly Core.SaveKey Record = Core.SaveBus.Key("WildWasteland.NPCAI.Senses.v1");

            /// <summary>New world: every agent's alert state and every ghost are dropped.</summary>
            private static void Clear()
            {
                _gen++;
                foreach (var kv in _agents) { var a = kv.Value; a.State = State.Idle; a.Target = null; a.Ghost = null; a.GhostPrio = Src.None; a.Heard.Clear(); a.Done.Clear(); }
                for (int i = _ghosts.Count - 1; i >= 0; i--) KillGhost(i);
            }

            /// <summary>Captures ghosts and alert agents into the active world save transaction.</summary>
            internal static void Save(string slot)
            {
                if (_loadFailed || _loadPending || _restoring) { Core.SaveBus.ReportFailure(NpcaiRuntime.Module, "NPC senses are unreadable or still restoring; refusing to replace the saved record"); return; }
                var sb = new StringBuilder();
                var ci = CultureInfo.InvariantCulture;
                float now = Time.time;
                sb.Append("v1\n");
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
                Record.Write(NpcaiRuntime.Module, slot, sb.ToString());
                if (WildWasteland.Core.Log.L2) NpcaiRuntime.Deep("Senses: saved " + _ghosts.Count + " ghost(s), " + n + " alert NPC(s) for " + Path.GetFileName(slot));
            }

            /// <summary>Reports damaged records and keeps a later save from replacing them with partial state.</summary>
            private static string[] ReadLines(string slot)
            {
                try
                {
                    _loadFailed = false;
                    string value = Record.Read(slot);
                    if (value == null) return null;
                    string[] lines = value.Split('\n');
                    if (lines.Length == 0 || lines[0] != "v1") throw new InvalidDataException("Unsupported embedded NPC senses record");
                    ValidateLines(lines);
                    _loadFailed = false;
                    return lines;
                }
                catch (Exception e)
                {
                    _loadFailed = true;
                    Core.SaveBus.ReportFailure(NpcaiRuntime.Module, "NPC senses load failed: " + e.Message);
                    NpcaiRuntime.Error("Senses persistence: " + e);
                    return null;
                }
            }

            /// <summary>Validates every row before touching live agents, including agents that are not yet spawned.</summary>
            private static void ValidateLines(string[] lines)
            {
                var ghosts = new HashSet<int>();
                var agents = new HashSet<string>(StringComparer.Ordinal);
                var references = new List<int>();
                var ci = CultureInfo.InvariantCulture;
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0) continue;
                    var p = lines[i].Split(' ');
                    if (p[0] == "ghost" && p.Length >= 7)
                    {
                        int id = int.Parse(p[1], ci), source = int.Parse(p[5], ci);
                        if (id < 0 || id == int.MaxValue || !ghosts.Add(id) || source < (int)Src.None || source > (int)Src.Vision) throw new InvalidDataException("Invalid ghost identity/source");
                        Finite(p[2]); Finite(p[3]); Finite(p[4]);
                        if (Finite(p[6]) < 0f) throw new InvalidDataException("Invalid ghost age");
                    }
                    else if (p[0] == "agent" && p.Length == 10)
                    {
                        int state = int.Parse(p[2], ci), ghost = int.Parse(p[4], ci), source = int.Parse(p[5], ci);
                        if (p[1].Length == 0 || p[3].Length == 0 || !agents.Add(p[1]) || state < (int)State.Combat || state > (int)State.Hide || ghost < -1 || source < (int)Src.None || source > (int)Src.Vision) throw new InvalidDataException("Invalid agent identity/state");
                        if (Finite(p[6]) < 0f) throw new InvalidDataException("Invalid agent search duration");
                        Finite(p[7]); Finite(p[8]); Finite(p[9]);
                        if (ghost >= 0) references.Add(ghost);
                    }
                    else throw new InvalidDataException("Malformed NPC senses row " + i);
                }
                foreach (int id in references) if (!ghosts.Contains(id)) throw new InvalidDataException("Missing NPC ghost " + id);
            }

            /// <summary>Parses finite invariant coordinates and durations; Unity cannot restore NaN/infinity positions.</summary>
            private static float Finite(string value)
            {
                float parsed = float.Parse(value, CultureInfo.InvariantCulture);
                if (float.IsNaN(parsed) || float.IsInfinity(parsed)) throw new InvalidDataException("Non-finite NPC senses value");
                return parsed;
            }

            /// <summary>Restores the embedded senses record after a load: recreates the ghosts, then matches saved agents by name for up to 20 s as the NPCs register.</summary>
            private static IEnumerator Restore(string slot)
            {
                _restoring = true;
                string[] lines = ReadLines(slot);
                if (lines == null) { _restoring = false; yield break; }
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
                    catch (Exception e) { _loadFailed = true; Core.SaveBus.ReportFailure(NpcaiRuntime.Module, "Invalid embedded NPC senses record: " + e.Message); NpcaiRuntime.Warn("Senses: bad embedded record line: " + e.Message); }
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
                        catch (Exception e) { _loadFailed = true; Core.SaveBus.ReportFailure(NpcaiRuntime.Module, "NPC senses restore failed: " + e.Message); NpcaiRuntime.Warn("Senses: restore of " + p[1] + " failed: " + e.Message); }
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
