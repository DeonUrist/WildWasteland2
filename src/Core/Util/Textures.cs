using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// How <see cref="Textures"/> creates and configures a texture. Zero/null fields mean "Unity default" so each of the 18
    /// old LoadImage sites can be expressed exactly: Format 0 = RGBA32; Wrap Repeat (Unity default); Filter null = leave
    /// (Bilinear); Aniso 0 = leave (1); Flags None. Readable=false passes markNonReadable=true to LoadImage (frees the CPU copy).
    /// </summary>
    public struct TextureSettings : IEquatable<TextureSettings>
    {
        /// <summary>Texture format of the 2x2 placeholder (0 = RGBA32). LoadImage replaces it for PNG/JPG anyway (RGB24/ARGB32).</summary>
        public TextureFormat Format;
        /// <summary>Create the texture with a mip chain.</summary>
        public bool Mipmaps;
        /// <summary>Linear (true) or sRGB (false) sampling.</summary>
        public bool Linear;
        /// <summary>Keep the CPU copy (GetPixels / SetData later); false = markNonReadable.</summary>
        public bool Readable;
        /// <summary>Wrap mode (Repeat = Unity default).</summary>
        public TextureWrapMode Wrap;
        /// <summary>Filter mode; null leaves Unity's default (Bilinear).</summary>
        public FilterMode? Filter;
        /// <summary>Anisotropic level; 0 leaves Unity's default.</summary>
        public int Aniso;
        /// <summary>Hide flags applied after a successful load.</summary>
        public HideFlags Flags;

        /// <summary>UI/sprite texture: RGBA32, no mips, Clamp, Bilinear, non-readable, DontUnloadUnusedAsset (CleanHUD Menu/LoadingScreen/MapIcons).</summary>
        public static TextureSettings Ui { get { return new TextureSettings { Wrap = TextureWrapMode.Clamp, Filter = FilterMode.Bilinear, Flags = HideFlags.DontUnloadUnusedAsset }; } }

        /// <summary>Model albedo: RGBA32 with mips, Repeat, Trilinear, aniso 4, non-readable, DontUnloadUnusedAsset (PlayerBody Model, Gungirl, Patrols Paint).</summary>
        public static TextureSettings Model { get { return new TextureSettings { Mipmaps = true, Wrap = TextureWrapMode.Repeat, Filter = FilterMode.Trilinear, Aniso = 4, Flags = HideFlags.DontUnloadUnusedAsset }; } }

        /// <summary>Field-wise equality (used as part of the cache key).</summary>
        public bool Equals(TextureSettings o)
        {
            return Format == o.Format && Mipmaps == o.Mipmaps && Linear == o.Linear && Readable == o.Readable && Wrap == o.Wrap
                && Filter == o.Filter && Aniso == o.Aniso && Flags == o.Flags;
        }

        /// <summary>Boxed equality (avoid; the cache uses the typed overload).</summary>
        public override bool Equals(object obj) { return obj is TextureSettings && Equals((TextureSettings)obj); }

        /// <summary>Hash of all fields.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Format;
                h = h * 31 + (Mipmaps ? 1 : 0); h = h * 31 + (Linear ? 1 : 0); h = h * 31 + (Readable ? 1 : 0);
                h = h * 31 + (int)Wrap; h = h * 31 + (Filter.HasValue ? (int)Filter.Value + 1 : 0); h = h * 31 + Aniso; h = h * 31 + (int)Flags;
                return h;
            }
        }
    }

    /// <summary>
    /// The one PNG/JPG loader (replaces 18 Texture2D.LoadImage sites). Loads from a file path, a packaged resource name
    /// (DiskResources, e.g. "CleanHUD.panel_x.png") or bytes; results are cached per (source, settings) with a negative cache
    /// for missing sources, optionally re-read when the file's write time changes (LoadingScreen, CharacterPortraits), and
    /// owned by a module: <see cref="Release(IModule)"/> destroys that module's textures (to be called by ModuleHost cleanup).
    /// Lookups of an already cached texture do not allocate (safe in OnGUI).
    /// </summary>
    public static class Textures
    {
        /// <summary>Cache key: source string (path or resource name, ordinal) + settings.</summary>
        private struct Key : IEquatable<Key>
        {
            public string Source; public bool Resource; public TextureSettings Settings;
            /// <summary>Ordinal source, kind and settings equality.</summary>
            public bool Equals(Key o) { return Resource == o.Resource && string.Equals(Source, o.Source, StringComparison.Ordinal) && Settings.Equals(o.Settings); }
            /// <summary>Boxed equality.</summary>
            public override bool Equals(object obj) { return obj is Key && Equals((Key)obj); }
            /// <summary>Combined hash.</summary>
            public override int GetHashCode() { unchecked { return (Source != null ? StringComparer.Ordinal.GetHashCode(Source) : 0) * 397 ^ Settings.GetHashCode() ^ (Resource ? 1 : 0); } }
        }

        /// <summary>One cached texture (null Texture = known missing/undecodable).</summary>
        private sealed class Entry
        {
            public Texture2D Texture; public IModule Owner; public DateTime Stamp;
        }

        static readonly Dictionary<Key, Entry> _cache = new Dictionary<Key, Entry>();
        static readonly List<Key> _drop = new List<Key>();

        /// <summary>Number of cached entries (Debug census).</summary>
        public static int Count { get { return _cache.Count; } }

        /// <summary>
        /// Creates a texture from PNG/JPG bytes with the given settings and name (not cached; the caller owns it). Returns null
        /// (and destroys the placeholder) when the bytes cannot be decoded.
        /// </summary>
        public static Texture2D FromBytes(byte[] bytes, TextureSettings s, string name = null)
        {
            if (bytes == null) return null;
            var t = new Texture2D(2, 2, s.Format == 0 ? TextureFormat.RGBA32 : s.Format, s.Mipmaps, s.Linear);
            if (name != null) t.name = name;
            if (!ImageConversion.LoadImage(t, bytes, !s.Readable)) { UnityEngine.Object.Destroy(t); return null; }
            t.wrapMode = s.Wrap;
            if (s.Filter.HasValue) t.filterMode = s.Filter.Value;
            if (s.Aniso > 0) t.anisoLevel = s.Aniso;
            t.hideFlags = s.Flags;
            return t;
        }

        /// <summary>Creates an uncached texture from a file; null when the file is missing or undecodable (failures logged at verbose level).</summary>
        public static Texture2D FromFile(string path, TextureSettings s, string name = null)
        {
            try { return File.Exists(path) ? FromBytes(File.ReadAllBytes(path), s, name ?? Path.GetFileNameWithoutExtension(path)) : null; }
            catch (Exception e) { if (Log.Verbose) Log.Info("[Core] Texture " + path + ": " + e.Message); return null; }
        }

        /// <summary>
        /// Cached load from a file path. owner may be null (Core-owned, destroyed only by <see cref="ReleaseAll"/>).
        /// reloadIfChanged re-reads the file when its write time changed and destroys the previous texture (callers holding the old
        /// reference must re-fetch). Returns null for a missing/undecodable file (remembered until the file changes or
        /// <see cref="Forget"/>).
        /// </summary>
        public static Texture2D Load(IModule owner, string path, TextureSettings s, bool reloadIfChanged = false, string name = null)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var key = new Key { Source = path, Resource = false, Settings = s };
            Entry e;
            bool have = _cache.TryGetValue(key, out e);
            if (have && !reloadIfChanged && (e.Texture != null || ReferenceEquals(e.Texture, null))) return e.Texture;
            DateTime stamp = DateTime.MinValue;
            try { if (File.Exists(path)) stamp = File.GetLastWriteTimeUtc(path); } catch (Exception) { }
            if (have && e.Stamp == stamp && (e.Texture != null || ReferenceEquals(e.Texture, null))) return e.Texture;
            var t = stamp == DateTime.MinValue ? null : FromFile(path, s, name);
            if (have && e.Texture != null && e.Texture != t) UnityEngine.Object.Destroy(e.Texture);
            if (!have) { e = new Entry(); _cache[key] = e; }
            e.Texture = t; e.Owner = owner; e.Stamp = stamp;
            return t;
        }

        /// <summary>Convenience overload of the brief: (path, linear, mipmaps, readable) with Unity defaults otherwise.</summary>
        public static Texture2D Load(IModule owner, string path, bool linear, bool mipmaps, bool readable)
        {
            return Load(owner, path, new TextureSettings { Linear = linear, Mipmaps = mipmaps, Readable = readable });
        }

        /// <summary>
        /// Cached load from a packaged resource (DiskResources name, e.g. "CleanHUD.icon.png"). Missing resources are
        /// remembered (CleanHUD Assets.Missing behaviour).
        /// </summary>
        public static Texture2D LoadResource(IModule owner, string resource, TextureSettings s, string name = null)
        {
            if (string.IsNullOrEmpty(resource)) return null;
            var key = new Key { Source = resource, Resource = true, Settings = s };
            Entry e;
            if (_cache.TryGetValue(key, out e) && (e.Texture != null || ReferenceEquals(e.Texture, null))) return e.Texture;
            Texture2D t = null;
            try
            {
                using (var stream = DiskResources.Open(resource))
                {
                    if (stream != null)
                    {
                        var buf = new byte[stream.Length];
                        int read = 0; while (read < buf.Length) { int r = stream.Read(buf, read, buf.Length - read); if (r <= 0) break; read += r; }
                        t = FromBytes(buf, s, name ?? resource);
                    }
                }
            }
            catch (Exception ex) { if (Log.Verbose) Log.Info("[Core] Texture resource " + resource + ": " + ex.Message); }
            if (e == null) { e = new Entry(); _cache[key] = e; }
            else if (e.Texture != null && e.Texture != t) UnityEngine.Object.Destroy(e.Texture);
            e.Texture = t; e.Owner = owner;
            return t;
        }

        /// <summary>Drops every cache entry for source (any settings) and destroys their textures, so the next Load re-reads it.</summary>
        public static void Forget(string source)
        {
            _drop.Clear();
            foreach (var pair in _cache) if (string.Equals(pair.Key.Source, source, StringComparison.Ordinal)) _drop.Add(pair.Key);
            for (int i = 0; i < _drop.Count; i++) { var e = _cache[_drop[i]]; if (e.Texture != null) UnityEngine.Object.Destroy(e.Texture); _cache.Remove(_drop[i]); }
            _drop.Clear();
        }

        /// <summary>Destroys and uncaches every texture loaded by owner (module shutdown).</summary>
        public static void Release(IModule owner)
        {
            if (owner == null) return;
            _drop.Clear();
            foreach (var pair in _cache) if (pair.Value.Owner == owner) _drop.Add(pair.Key);
            for (int i = 0; i < _drop.Count; i++) { var e = _cache[_drop[i]]; if (e.Texture != null) UnityEngine.Object.Destroy(e.Texture); _cache.Remove(_drop[i]); }
            _drop.Clear();
        }

        /// <summary>Destroys and uncaches everything (plugin teardown).</summary>
        public static void ReleaseAll()
        {
            foreach (var pair in _cache) if (pair.Value.Texture != null) UnityEngine.Object.Destroy(pair.Value.Texture);
            _cache.Clear();
        }
    }
}
