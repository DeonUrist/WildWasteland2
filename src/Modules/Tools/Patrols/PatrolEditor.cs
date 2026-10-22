using System;
using System.Collections.Generic;
using System.Linq;
using WildWasteland.Core;
namespace WildWasteland.Modules.Tools
{
    /// <summary>Detached authoring workspace; runtime-owned mutations are explicit validated commands.</summary>
    internal static class PatrolEditor
    {
        internal static IPatrolAuthoring Source { get { return ModApi.Get<IPatrolAuthoring>(); } }
        internal static PatrolEditorData Data;
        internal static readonly List<PatrolTemplateInfo> Park = new List<PatrolTemplateInfo>();
        internal static void Refresh(bool force) { Source.Refresh(force); Source.Templates(Park); }
        internal static void Load() { Data=PatrolEditorData.FromJson(Source.ReadData()); Refresh(false); }
        internal static void Save() { Source.SaveData(Data.ToJson()); }
        internal static void Reset(bool recycle) { Source.Reset(recycle);Load(); }
        internal static PatrolTemplateInfo Find(string name) { return Park.FirstOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)); }
        internal static LootProfile Loot(bool truck,string id) { return (truck?Data.TruckLoot:Data.CarLoot).FirstOrDefault(p=>string.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase)); }
        internal static void Delete(PatrolTemplateInfo template) { Source.Delete(template.Name);Load(); }
        internal static void Favorite(PatrolTemplateInfo template) { Source.Favorite(template.Name);Refresh(true); }
    }
    /// <summary>UI projection of actual owner-provided merchant categories.</summary>
    internal static class EditorMerchants
    {
        internal static string[] Types { get { return PatrolEditor.Source.MerchantTypes; } }
        internal static bool Known(string type) { return Types.Contains(type); }
        internal static string Label(string type) { return PatrolEditor.Source.MerchantLabel(type); }
        internal static string Selected(IEnumerable<string> types) { return PatrolEditor.Source.MerchantSelected(types); }
        internal static string[] Selection(IEnumerable<string> types) { return new[]{Selected(types)}; }
    }
    /// <summary>Owner-provided cargo item catalog for the ledger search.</summary>
    internal static class EditorItems
    {
        private static readonly List<LootAuthoringItem> Rows=new List<LootAuthoringItem>();
        internal static List<LootAuthoringItem> All() { var api=ModApi.Get<ILootAuthoringCatalog>();Rows.Clear();if(api!=null)api.Items(Rows);return Rows; }
        internal static string Name(string id) { return ModApi.Get<ILootAuthoringCatalog>()?.Name(id)??id.Replace('_',' '); }
    }
}
