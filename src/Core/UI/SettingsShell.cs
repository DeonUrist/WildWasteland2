using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace WildWasteland.Core.UI
{
    /// <summary>
    /// Contribution registry for the shared F3 settings pages (core-modapi.md 7, README 4.1 "Settings shell"). Each module adds rows
    /// bound to its own ConfigEntries from <see cref="IModule.Settings"/>; Core lays them out, so CorePages loses its module references
    /// in Wave B (B01). In W0 CorePages still draws its hand-built rows; <see cref="Draw"/> is the contribution path next to them.
    /// Contributions of a module are dropped when it is re-collected or removed. Main thread only.
    /// </summary>
    public sealed class SettingsShell
    {
        /// <summary>Shared F3 pages a row can go to.</summary>
        public enum Page { Player, World, Hud, System, Debug, Developer }

        /// <summary>One contributed row (entry row or custom-drawn row).</summary>
        public sealed class Contribution
        {
            /// <summary>Contributing module (null = Core).</summary>
            public IModule Owner;
            /// <summary>Target page.</summary>
            public Page Page;
            /// <summary>Group heading within the page.</summary>
            public string Group;
            /// <summary>Bound entry (null for custom rows).</summary>
            public ConfigEntryBase Entry;
            /// <summary>Row label (null = entry key).</summary>
            public string Label;
            /// <summary>Custom draw callback (null for entry rows).</summary>
            public Action<ToolLayout> Draw;
            /// <summary>Sort order within the group (lower first; ties keep registration order).</summary>
            public int Order;
            /// <summary>Registration sequence (stable sort tie-breaker).</summary>
            internal int Sequence;
        }

        /// <summary>The one shell instance Core passes to every module.</summary>
        public static readonly SettingsShell Shared = new SettingsShell();

        readonly List<Contribution> _rows = new List<Contribution>();
        readonly List<KeyValuePair<Page, ConfigEntryBase>> _resettable = new List<KeyValuePair<Page, ConfigEntryBase>>();
        readonly Dictionary<IModule, List<KeyValuePair<Page, ConfigEntryBase>>> _resettableOwners = new Dictionary<IModule, List<KeyValuePair<Page, ConfigEntryBase>>>();
        int _sequence;
        int _revision;

        /// <summary>Increments on every change (pages rebuild cached layouts).</summary>
        public int Revision { get { return _revision; } }

        /// <summary>Adds a bool/choice/slider row bound to a module ConfigEntry, in a named group, with sort order.</summary>
        public void Row(IModule owner, Page page, string group, ConfigEntryBase entry, string label, int order = 0)
        {
            if (entry == null) return;
            _rows.Add(new Contribution { Owner = owner, Page = page, Group = group ?? "", Entry = entry, Label = label, Order = order, Sequence = _sequence++ });
            _revision++;
        }

        /// <summary>Adds a custom-drawn row (previews like crosshair/body selection).</summary>
        public void Custom(IModule owner, Page page, string group, Action<ToolLayout> draw, int order = 0)
        {
            if (draw == null) return;
            _rows.Add(new Contribution { Owner = owner, Page = page, Group = group ?? "", Draw = draw, Order = order, Sequence = _sequence++ });
            _revision++;
        }

        /// <summary>Declares an entry that the page's RESET restores to its default (owner modules decide which of their entries reset).</summary>
        public void Resettable(IModule owner, Page page, ConfigEntryBase entry)
        {
            if (entry == null) return;
            var kv = new KeyValuePair<Page, ConfigEntryBase>(page, entry);
            _resettable.Add(kv);
            var key = owner ?? NoOwner.Instance;
            List<KeyValuePair<Page, ConfigEntryBase>> list;
            if (!_resettableOwners.TryGetValue(key, out list)) { list = new List<KeyValuePair<Page, ConfigEntryBase>>(); _resettableOwners[key] = list; }
            list.Add(kv);
            _revision++;
        }

        /// <summary>Fills <paramref name="into"/> (cleared first) with the rows of a page, sorted by group (first appearance), order, registration.</summary>
        public void Rows(Page page, List<Contribution> into)
        {
            into.Clear();
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Page == page) into.Add(_rows[i]);
            var groups = new List<string>();
            for (int i = 0; i < into.Count; i++) if (!groups.Contains(into[i].Group)) groups.Add(into[i].Group);
            into.Sort((a, b) =>
            {
                int g = groups.IndexOf(a.Group).CompareTo(groups.IndexOf(b.Group));
                if (g != 0) return g;
                int o = a.Order.CompareTo(b.Order);
                return o != 0 ? o : a.Sequence.CompareTo(b.Sequence);
            });
        }

        /// <summary>Fills <paramref name="into"/> (cleared first) with the entries RESET restores on a page.</summary>
        public void ResettableEntries(Page page, List<ConfigEntryBase> into)
        {
            into.Clear();
            for (int i = 0; i < _resettable.Count; i++) if (_resettable[i].Key == page) into.Add(_resettable[i].Value);
        }

        readonly List<Contribution> _drawBuffer = new List<Contribution>();

        /// <summary>Draws the contributed rows of a page: a heading per group, then each row (bool entries as toggles with the
        /// contributed label, other entries with the generic entry row, custom rows through their callback). Exceptions of a custom
        /// row are reported against its module.</summary>
        public void Draw(Page page, ToolLayout l)
        {
            Rows(page, _drawBuffer);
            string group = null;
            for (int i = 0; i < _drawBuffer.Count; i++)
            {
                var c = _drawBuffer[i];
                if (c.Group != group) { group = c.Group; if (group.Length > 0) l.Heading(group); }
                try
                {
                    if (c.Draw != null) c.Draw(l);
                    else
                    {
                        var b = c.Entry as ConfigEntry<bool>;
                        if (b != null) l.Toggle(b, c.Label);
                        else l.Entry(c.Entry);
                    }
                }
                catch (Exception e) { ModuleHost.ReportHookException(c.Owner, "settings row " + (c.Label ?? group), e); }
            }
        }

        /// <summary>Removes every contribution of <paramref name="owner"/> (before re-collecting it).</summary>
        internal void RemoveAll(IModule owner)
        {
            int before = _rows.Count;
            _rows.RemoveAll(c => c.Owner == owner);
            List<KeyValuePair<Page, ConfigEntryBase>> list;
            var key = owner ?? NoOwner.Instance;
            if (_resettableOwners.TryGetValue(key, out list))
            {
                for (int i = 0; i < list.Count; i++) _resettable.Remove(list[i]);
                _resettableOwners.Remove(key);
            }
            if (before != _rows.Count || list != null) _revision++;
        }

        /// <summary>Dictionary key standing in for Core (null owner).</summary>
        sealed class NoOwner : ModuleBase
        {
            internal static readonly NoOwner Instance = new NoOwner();
            /// <summary>Placeholder id.</summary>
            public override string Id { get { return "Core"; } }
            /// <summary>Placeholder name.</summary>
            public override string DisplayName { get { return "Core"; } }
            /// <summary>Never bound.</summary>
            public override void BindConfig(ModuleConfig cfg) { }
            /// <summary>Never started.</summary>
            public override void Init() { }
        }
    }
}
