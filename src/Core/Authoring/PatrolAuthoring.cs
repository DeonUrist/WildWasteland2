using System.Collections.Generic;
namespace WildWasteland.Core
{
    /// <summary>Detached template metadata used by authoring menus; no mutable simulation objects are exposed.</summary>
    public sealed class PatrolTemplateInfo
    {
        public string Name, Body, Driver, Passenger, Faction, LootPreset, SourcePath, MissingPrefab, KindLabel;
        public bool Available, IsDefault, Favorite, IsTruck, IsMotorcycle;
        public float ExplodeChance;
        public string[] Turrets;
        public bool[] TurretStanding;
        public PatrolTemplateInfo Trailer;
    }
    /// <summary>Editable crew/cargo metadata for one existing authored template.</summary>
    public sealed class PatrolTemplateEdit
    {
        public string Name, Driver, Passenger, Loot, Faction;
        public float ExplodeChance;
        public string[] Turrets, TrailerTurrets;
        public bool[] Standing, TrailerStanding;
    }
    /// <summary>Patrol-owned authoring metadata, validated persistence and explicit debug spawn actions.</summary>
    public interface IPatrolAuthoring
    {
        bool InPlay { get; }
        string VehicleFaction(UnityEngine.GameObject vehicle);
        string ReadData();
        void SaveData(string json);
        void Reset(bool recycleCustom);
        void Templates(List<PatrolTemplateInfo> into);
        void Refresh(bool force);
        string Summary { get; }
        string DataDirectory { get; }
        string PlayerDirectory { get; }
        void Delete(string name);
        void Favorite(string name);
        void Edit(PatrolTemplateEdit edit);
        void RemoveLootPreset(string id);
        string SaveRecorded(string name, string json);
        void SpawnTemplate(string name, int role, string groupId, string[] cargoOptions);
        void SpawnGroup(string id, int role);
        string NormalizeFaction(string faction);
        bool CrewAllowed(string name, string faction);
        bool Compatible(string name, string faction);
        string[] CoyoteCrew { get; }
        string[] TurretChoices { get; }
        int TurretSlots(string body, bool truck, bool trailer);
        string TurretDisplay(string stored);
        string TurretStored(string shown);
        string[] MerchantTypes { get; }
        string MerchantLabel(string type);
        string MerchantSelected(IEnumerable<string> types);
    }
    /// <summary>One pickable item available for editable cargo tables.</summary>
    public sealed class LootAuthoringItem { public string Id, Name; }
    /// <summary>Loot-owned on-demand catalog of native and mod pickable items.</summary>
    public interface ILootAuthoringCatalog { void Items(List<LootAuthoringItem> into); string Name(string id); }
}
