using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Old cross-module service locator, now a thin [Obsolete] facade over <see cref="ModApi"/> (README 4.1): every service property
    /// forwards to <c>ModApi.Get&lt;T&gt;()</c>, and assigning it registers the value with ModApi for the module currently in its
    /// lifecycle call (so it is also removed automatically on shutdown). The old GameObject-typed events keep working until Wave C;
    /// handlers left behind by a module that shuts down are removed by <see cref="RemoveHandlersOf"/>. Contracts moved to
    /// src/Core/ModApi/Contracts/*.cs. Deleted in Wave C.
    /// </summary>
    public static class Services
    {
        /// <summary>Hooks ModApi.Changed so <see cref="PoiChanged"/> also fires when POI registers through ModApi directly.</summary>
        static Services() { ModApi.Changed += OnApiChanged; }

        /// <summary>Animation framework (PlayerBody / Presentation). Null when the provider is off.</summary>
        [Obsolete("refactor: use ModApi.Get<IAnimationApi>() / ICharacterPresentation")]
        public static IAnimationApi Animation { get { return ModApi.Get<IAnimationApi>(); } set { Set(value); } }

        /// <summary>(NPC) "Setup POI pathfinding" developer mode is on: Towns plans no new towns while it is (saved towns are kept).</summary>
        [Obsolete("refactor: use INavApi.WorldGenerationSuspended")]
        public static volatile bool PoiNavSetup;

        /// <summary>(PlayerBody ladders) the ladder trigger the player climbs right now, else null. NPCs follow / wait for him there.</summary>
        [Obsolete("refactor: use ITraversal.TryPlayerLadder")]
        public static Collider PlayerLadder;

        /// <summary>(PlayerBody ladders) while PlayerLadder is set: the horizontal direction from the rung line to the player (his climbing side).</summary>
        [Obsolete("refactor: use ITraversal.TryPlayerLadder")]
        public static Vector3 PlayerLadderSide;

        /// <summary>(PlayerBody ladders) sliding down a ladder: the held pose (fraction of the climb cycle) and the top speed, m/s. NPCs slide the same.</summary>
        [Obsolete("refactor: use ITraversal.TryPlayerLadder")]
        public static float LadderSlidePhase = 0f, LadderSlideSpeed = 6f;

        /// <summary>(NPC POI navigation) feet of the NPCs on ladders right now (refreshed every physics step): the player cannot climb through them.</summary>
        [Obsolete("refactor: use ITraversal.SetLadderOccupant / LadderOccupants")]
        public static readonly List<Vector3> LadderClimbers = new List<Vector3>();

        /// <summary>NPC AI (NPC / NPCAI). Null when the provider is off.</summary>
        [Obsolete("refactor: use ModApi.Get<INpcApi>()")]
        public static INpcApi Npc { get { return ModApi.Get<INpcApi>(); } set { Set(value); } }

        /// <summary>NPC dressing by identity (NPC Women / Presentation).</summary>
        [Obsolete("refactor: use ModApi.Get<ICharacterAppearance>()")]
        public static INpcAppearance NpcAppearance { get { return ModApi.Get<INpcAppearance>(); } set { Set(value); } }

        /// <summary>Character model/texture paths (NPC Women / Presentation).</summary>
        [Obsolete("refactor: use ModApi.Get<ICharacterAppearance>()")]
        public static ICharacterAssets CharacterAssets { get { return ModApi.Get<ICharacterAssets>(); } set { Set(value); } }

        /// <summary>(Patrols) vehicle combat near the player, read by the autosave deferral.</summary>

        /// <summary>(Guns) reports combat activity to the autosave deferral.</summary>
        [Obsolete("refactor: publish the CombatActivity event")]
        internal static void ReportCombat() { var activity = new Events.CombatActivity { Actor = EntityId.None, Kind = Events.CombatActivityKind.Attack }; ModEvents.Publish(ref activity); }

        /// <summary>Guns (Guns / Gunplay). Null when the provider is off.</summary>
        [Obsolete("refactor: use ModApi.Get<IGunsApi>() / IWeaponCatalog / IWeaponStats")]
        public static IGunsApi Guns { get { return ModApi.Get<IGunsApi>(); } set { Set(value); } }

        /// <summary>Hit feedback (HUD). Null when the provider is off.</summary>
        [Obsolete("refactor: use ModApi.Get<IHitFeedback>()")]
        public static IHitFeedback Hud { get { return ModApi.Get<IHitFeedback>(); } set { Set(value); } }

        /// <summary>Vehicle status line (CleanHUD / HUD).</summary>
        [Obsolete("refactor: use ModApi.Get<IVehicleStatusDisplay>()")]
        public static IVehicleStatusDisplay VehicleStatus { get { return ModApi.Get<IVehicleStatusDisplay>(); } set { Set(value); } }

        /// <summary>Engine ignition (EngineSimCompat / Compatibility).</summary>
        [Obsolete("refactor: use ModApi.Get<IVehicleIgnition>()")]
        public static IVehicleIgnition VehicleIgnition { get { return ModApi.Get<IVehicleIgnition>(); } set { Set(value); } }

        /// <summary>Compass markers (CleanHUD / HUD).</summary>
        [Obsolete("refactor: use ModApi.Get<ICompassMarkers>()")]
        public static ICompassMarkers Compass { get { return ModApi.Get<ICompassMarkers>(); } set { Set(value); } }

        /// <summary>Map markers (CleanHUD / HUD).</summary>
        [Obsolete("refactor: use ModApi.Get<IMapMarkers>()")]
        public static IMapMarkers Map { get { return ModApi.Get<IMapMarkers>(); } set { Set(value); } }

        /// <summary>Weather regions (Weather / Survival).</summary>
        [Obsolete("refactor: use ModApi.Get<IWeatherRegions>()")]
        public static IWeatherRegions Weather { get { return ModApi.Get<IWeatherRegions>(); } set { Set(value); } }

        /// <summary>Weather render detail policy (Performance / Core Optimization).</summary>
        [Obsolete("refactor: use ModApi.Get<IWeatherVisualPolicy>()")]
        public static IWeatherVisualPolicy WeatherVisualPolicy { get { return ModApi.Get<IWeatherVisualPolicy>(); } set { Set(value); } }

        /// <summary>Crew seating for Quests followers (Patrols).</summary>
        [Obsolete("refactor: use ModApi.Get<IFollowerVehicles>()")]
        public static IFollowerVehicles FollowerVehicles { get { return ModApi.Get<IFollowerVehicles>(); } set { Set(value); } }

        /// <summary>Quest state (Quests).</summary>
        [Obsolete("refactor: use ModApi.Get<IQuestApi>()")]
        public static IQuestApi Quests { get { return ModApi.Get<IQuestApi>(); } set { Set(value); } }

        /// <summary>Quest widgets (CleanHUD / HUD).</summary>
        [Obsolete("refactor: use ModApi.Get<IQuestPresentation>()")]
        public static IQuestPresentation QuestUi { get { return ModApi.Get<IQuestPresentation>(); } set { Set(value); } }

        /// <summary>Camp state (Patrols; POI IWorldLocations in Wave B).</summary>
        [Obsolete("refactor: use ModApi.Get<ICampStatus>() / IWorldLocations")]
        public static ICampStatus Camps { get { return ModApi.Get<ICampStatus>(); } set { Set(value); } }

        /// <summary>Raised when the POI provider appears or disappears (argument = new provider or null).</summary>
        [Obsolete("refactor: use ModApi.Changed")]
        public static event Action<IPoiApi> PoiChanged;

        /// <summary>Custom POI registry (POI).</summary>
        [Obsolete("refactor: use ModApi.Get<IPoiApi>()")]
        public static IPoiApi Poi { get { return ModApi.Get<IPoiApi>(); } set { Set(value); } }

        /// <summary>Patrols raised: an occupant left a raider car (ex Apocapatrol.Patrol.BailOut). (car, previous passenger object, new on-foot NPC)</summary>
        [Obsolete("refactor: subscribe to the CrewBailedOut event (ModEvents)")]
        public static event Action<GameObject, GameObject, GameObject> CrewBailedOut;

        /// <summary>NPC hurt (victim, attacker), raised by NPC/Guns.</summary>
        [Obsolete("refactor: subscribe to the Hurt event (ModEvents)")]
        public static event Action<GameObject, GameObject> NpcHurt;

        /// <summary>Vehicle part hit (part, attacker), raised by Guns.</summary>
        [Obsolete("refactor: subscribe to the VehiclePartHit event (ModEvents)")]
        public static event Action<GameObject, GameObject> VehiclePartHit;

        /// <summary>Patrols raised: a raider car exploded (ex Apocapatrol.Explode.Blast).</summary>
        [Obsolete("refactor: subscribe to the VehicleExploded event (ModEvents)")]
        public static event Action<GameObject> VehicleExploded;

        /// <summary>Raises <see cref="VehiclePartHit"/>; handler exceptions are logged.</summary>
        internal static void RaiseVehiclePartHit(GameObject part, GameObject attacker)
        { var h=VehiclePartHit;if(h==null)return;try{h(part,attacker);}catch(Exception e){Log.Error(null, "VehiclePartHit handler",e);} }

        /// <summary>Raises <see cref="NpcHurt"/>; handler exceptions are logged.</summary>
        internal static void RaiseNpcHurt(GameObject victim, GameObject attacker)
        { var h=NpcHurt;if(h==null)return;try{h(victim,attacker);}catch(Exception e){Log.Error(null, "NpcHurt handler",e);} }

        /// <summary>Raises <see cref="CrewBailedOut"/>; handler exceptions are logged.</summary>
        [Obsolete("refactor: publish the CrewBailedOut event (ModEvents)")]
        public static void RaiseCrewBailedOut(GameObject car, GameObject pax, GameObject npc)
        {
            var h = CrewBailedOut; if (h == null) return;
            try { h(car, pax, npc); } catch (Exception e) { Log.Error(null, "CrewBailedOut handler", e); }
        }

        /// <summary>Raises <see cref="VehicleExploded"/>; handler exceptions are logged.</summary>
        [Obsolete("refactor: publish the VehicleExploded event (ModEvents)")]
        public static void RaiseVehicleExploded(GameObject car)
        {
            var h = VehicleExploded; if (h == null) return;
            try { h(car); } catch (Exception e) { Log.Error(null, "VehicleExploded handler", e); }
        }

        /// <summary>Clears every old event (no owner tracking).</summary>
        internal static void ClearEvents()
        {
            CrewBailedOut = null; VehicleExploded = null; NpcHurt = null; VehiclePartHit = null; PoiChanged = null;
        }

        /// <summary>Removes old-event handlers declared in <paramref name="m"/>'s namespace (or a sub-namespace); called by
        /// ModuleHost.Cleanup so a stopped module never keeps handlers (README 9: ClearEvents was never called).</summary>
        internal static void RemoveHandlersOf(IModule m)
        {
            if (m == null) return;
            string ns = m.GetType().Namespace;
            if (string.IsNullOrEmpty(ns) || ns == "WildWasteland.Core") return;
            CrewBailedOut = Without(CrewBailedOut, ns);
            NpcHurt = Without(NpcHurt, ns);
            VehiclePartHit = Without(VehiclePartHit, ns);
            VehicleExploded = Without(VehicleExploded, ns);
            PoiChanged = Without(PoiChanged, ns);
        }

        /// <summary>Copy of <paramref name="d"/> without invocation entries whose method is declared in namespace <paramref name="ns"/>.</summary>
        static D Without<D>(D d, string ns) where D : class
        {
            var del = d as Delegate;
            if (del == null) return d;
            Delegate kept = null;
            foreach (var x in del.GetInvocationList())
            {
                var t = x.Method.DeclaringType;
                string xns = t != null ? t.Namespace : null;
                if (xns != null && (xns == ns || xns.StartsWith(ns + ".", StringComparison.Ordinal))) continue;
                kept = Delegate.Combine(kept, x);
            }
            return kept as D;
        }

        /// <summary>Old field assignment: registers <paramref name="value"/> with ModApi for the module in its lifecycle call
        /// (<see cref="ModuleHost.Current"/>); null withdraws the current provider.</summary>
        static void Set<T>(T value) where T : class
        {
            if (value == null) ModApi.Provide<T>(ModApi.OwnerOf<T>(), null);
            else ModApi.Provide(ModuleHost.Current, value);
        }

        /// <summary>Forwards IPoiApi provider changes to the old <see cref="PoiChanged"/> event.</summary>
        static void OnApiChanged(Type contract)
        {
            if (contract != typeof(IPoiApi)) return;
            var changed = PoiChanged;
            if (changed == null) return;
            var poi = ModApi.Get<IPoiApi>();
            foreach (Action<IPoiApi> d in changed.GetInvocationList())
            {
                try { d(poi); } catch (Exception e) { Log.Error(null, "POI provider notification", e); }
            }
        }
    }
}
