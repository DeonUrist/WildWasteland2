using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.NPC
{
    // where the files live: the editor / baker writes config\WildWasteland\NPC\PoiNav\<type>.txt; a shipped pack may sit in
    // plugins\WildWasteland\NPC\PoiNav\ (read when no config file exists)
    internal static class PoiNavStore
    {
        internal static string ConfigDir { get { return Path.Combine(Assets.ModuleConfigDir("NPC"), "PoiNav"); } }
        internal static string ShippedDir { get { return Path.Combine(Assets.ModuleDir("NPC"), "PoiNav"); } }
        static string FileName(string poi)
        {
            var sb = new StringBuilder();
            foreach (char c in poi) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString() + ".txt";
        }
        internal static string ConfigPath(string poi) { return Path.Combine(ConfigDir, FileName(poi)); }

        private static readonly Dictionary<string, PoiNavData> _cache = new Dictionary<string, PoiNavData>(StringComparer.Ordinal);
        private sealed class Loading
        {
            internal Task<PoiNavData> Work;
            internal readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            internal readonly List<string> Errors = new List<string>();
        }
        private static readonly Dictionary<string, Loading> _loading = new Dictionary<string, Loading>(StringComparer.Ordinal);
        private static readonly object _readLock = new object();
        internal static void Forget() { foreach (var job in _loading.Values) job.Cancel.Cancel(); _loading.Clear(); _cache.Clear(); }
        internal static void Forget(string poi) { Loading job; if (_loading.TryGetValue(poi, out job)) { job.Cancel.Cancel(); _loading.Remove(poi); } _cache.Remove(poi); }

        internal static PoiNavData GetAsync(string poi)
        {
            if (string.IsNullOrEmpty(poi)) return null;
            PoiNavData data; if (_cache.TryGetValue(poi, out data)) return data;
            Loading job;
            if (!_loading.TryGetValue(poi, out job))
            {
                var paths = new[] { ConfigPath(poi), Path.Combine(ShippedDir, FileName(poi)) };
                job = new Loading(); var load = job;
                job.Work = Task.Run(() =>
                {
                    lock (_readLock) foreach (var path in paths)
                    {
                        if (load.Cancel.IsCancellationRequested) return null;
                        try { if (File.Exists(path)) { var parsed = PoiNavData.Read(File.ReadAllText(path)); parsed.Poi = poi; return parsed; } }
                        catch (Exception e) { load.Errors.Add("POI navigation file " + path + " ignored: " + e.Message); }
                    }
                    return (PoiNavData)null;
                });
                _loading.Add(poi, job); return null;
            }
            if (!job.Work.IsCompleted) return null;
            _loading.Remove(poi);
            if (job.Work.IsFaulted) { Log.Warn(NavRuntime.Owner, "POI navigation load failed: " + poi + ": " + job.Work.Exception.GetBaseException().Message); _cache[poi] = null; return null; }
            foreach (var error in job.Errors) Log.Warn(NavRuntime.Owner, error);
            data = job.Work.Result; _cache[poi] = data; return data;
        }

        /// <summary>The data for a POI type, or null (cached, also the miss).</summary>
        internal static PoiNavData Get(string poi)
        {
            if (string.IsNullOrEmpty(poi)) return null;
            PoiNavData d;
            if (_cache.TryGetValue(poi, out d)) return d;
            d = null;
            foreach (var path in new[] { ConfigPath(poi), Path.Combine(ShippedDir, FileName(poi)) })
            {
                try { if (File.Exists(path)) { d = PoiNavData.Read(File.ReadAllText(path)); d.Poi = poi; break; } }
                catch (Exception e) { Log.Warn(NavRuntime.Owner, "POI navigation file " + path + " ignored: " + e.Message); }
            }
            _cache[poi] = d;
            Loading pending; if (_loading.TryGetValue(poi, out pending)) { pending.Cancel.Cancel(); _loading.Remove(poi); }
            return d;
        }

        internal static void Save(PoiNavData d)
        {
            Directory.CreateDirectory(ConfigDir);
            string path = ConfigPath(d.Poi), tmp = path + ".tmp";
            string text = d.Write();
            PoiNavData.Read(text);   // never publish a file this reader rejects
            File.WriteAllText(tmp, text);
            AtomicFile.Replace(tmp, path, path + ".bak");   // (D17) previous file kept as .bak, then a same-directory replace (no Copy over an existing file)
            d.Recompute();
            _cache[d.Poi] = d;
            Loading pending; if (_loading.TryGetValue(d.Poi, out pending)) { pending.Cancel.Cancel(); _loading.Remove(d.Poi); }
        }

        /// <summary>"Camp_7(Clone)", "Building_3 (Towns 1A2B/4)", "Camp_7 (WW POI nav setup)" -> the POI type.</summary>
        internal static string TypeOf(string rootName)
        {
            if (string.IsNullOrEmpty(rootName)) return "";
            int cut = rootName.Length;
            int p = rootName.IndexOf('('); if (p >= 0 && p < cut) cut = p;
            int s = rootName.IndexOf(' '); if (s >= 0 && s < cut) cut = s;
            return rootName.Substring(0, cut);
        }
    }
}
