using System;
using System.Collections.Generic;
using System.Linq;
using WildWasteland.Core;
namespace WildWasteland.Modules.Tools
{
    /// <summary>Detached authoring workspace; runtime-owned mutations are explicit validated commands.</summary>
    internal static class PatrolEditor
    {
        /// <summary>Returns the currently registered Patrols authoring owner.</summary>
        internal static IPatrolAuthoring Source { get { return ModApi.Get<IPatrolAuthoring>(); } }
        internal static PatrolEditorData Data;
        internal static readonly List<PatrolTemplateInfo> Park = new List<PatrolTemplateInfo>();
        /// <summary>Refreshes the real catalog and replaces detached template metadata.</summary>
        internal static void Refresh(bool force) { Source.Refresh(force); Source.Templates(Park); }
        /// <summary>Refreshes the real catalog and replaces detached template metadata.</summary>
        internal static void Load() { Data=PatrolEditorData.FromJson(Source.ReadData()); Refresh(false); }
        /// <summary>Submits the entire edited workspace to its validating runtime owner.</summary>
        internal static void Save() { Source.SaveData(Data.ToJson()); }
        /// <summary>Loads a fresh detached authoring workspace and current template list.</summary>
        internal static void Reset(bool recycle) { Source.Reset(recycle);Load(); }
        /// <summary>Resolves current detached template metadata by case-insensitive name.</summary>
        internal static PatrolTemplateInfo Find(string name) { return Park.FirstOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)); }
        /// <summary>Finds the selected cargo profile in the detached car or truck collection.</summary>
        internal static LootProfile Loot(bool truck,string id) { return (truck?Data.TruckLoot:Data.CarLoot).FirstOrDefault(p=>string.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase)); }
        /// <summary>Loads a fresh detached authoring workspace and current template list.</summary>
        internal static void Delete(PatrolTemplateInfo template) { Source.Delete(template.Name);Load(); }
        /// <summary>Refreshes the real catalog and replaces detached template metadata.</summary>
        internal static void Favorite(PatrolTemplateInfo template) { Source.Favorite(template.Name);Refresh(true); }
    }
    /// <summary>UI projection of actual owner-provided merchant categories.</summary>
    internal static class EditorMerchants
    {
        /// <summary>Reads supported categories from the merchant runtime owner.</summary>
        internal static string[] Types { get { return PatrolEditor.Source.MerchantTypes; } }
        /// <summary>Checks whether a category belongs to the authoritative merchant catalog.</summary>
        internal static bool Known(string type) { return Types.Contains(type); }
        /// <summary>Gets the runtime-defined display label for a merchant category.</summary>
        internal static string Label(string type) { return PatrolEditor.Source.MerchantLabel(type); }
        /// <summary>Normalizes authored merchant categories through their runtime owner.</summary>
        internal static string Selected(IEnumerable<string> types) { return PatrolEditor.Source.MerchantSelected(types); }
        /// <summary>Normalizes authored merchant categories through their runtime owner.</summary>
        internal static string[] Selection(IEnumerable<string> types) { return new[]{Selected(types)}; }
    }
    /// <summary>Owner-provided cargo item catalog for the ledger search.</summary>
    internal static class EditorItems
    {
        private static readonly List<LootAuthoringItem> Rows=new List<LootAuthoringItem>();
        /// <summary>Refreshes the reusable item list from the actual Loot authoring catalog.</summary>
        internal static List<LootAuthoringItem> All() { var api=ModApi.Get<ILootAuthoringCatalog>();Rows.Clear();if(api!=null)api.Items(Rows);return Rows; }
        /// <summary>Uses the Loot item display name or a readable identifier fallback.</summary>
        internal static string Name(string id) { return ModApi.Get<ILootAuthoringCatalog>()?.Name(id)??id.Replace('_',' '); }
    }
}
