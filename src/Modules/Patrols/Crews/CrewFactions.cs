using System;
using System.Collections.Generic;
using System.Linq;
namespace WildWasteland.Modules.Patrols
{
    internal static class CrewFactions
    {
        internal static readonly string[] Coyotes={"Bodyguard","Bolter","Gunnar","Lugger","Mechanic","Merchant","Organic_Mechanic","Pistoleer","Pup"};
        // foot-patrol travelers: every fighting NPC type of the faction as offered by the patrol editor (Coyote shop keepers excluded)
        internal static readonly string[] ScrapyardTravelers={"Boltjaw","Flexa","Gungirl","Shoota","Lugnut","Scraffa","Scrud","Spanna","Sprokka"};
        internal static readonly string[] CoyoteTravelers={"Bodyguard","Bolter","Gunnar","Lugger","Pistoleer","Pup"};
        /// <summary>Selects authored traveller identities for the normalized faction.</summary>
        internal static string[] Travelers(string faction)=>TrafficFactions.Normalize(faction)=="Coyotes"?CoyoteTravelers:ScrapyardTravelers;
        /// <summary>Checks whether the authored crew slot represents no occupant.</summary>
        internal static bool Empty(string person)=>string.IsNullOrWhiteSpace(person)||person=="None";
        /// <summary>Resolves the supplied crew identity to its accepted faction.</summary>
        internal static string Of(string person)=>Coyotes.Contains((person??"").Trim(),StringComparer.OrdinalIgnoreCase)?"Coyotes":"Scrapyard";
        /// <summary>Checks the authored identity against its requested faction.</summary>
        internal static bool Allowed(string person,string faction)=>Empty(person)||Of(person)==TrafficFactions.Normalize(faction);
        /// <summary>Enumerates authored driver, passenger and turret identities.</summary>
        internal static IEnumerable<string> People(CarTemplate t)=>new[]{t.Driver,t.Passenger}.Concat(t.Turrets).Concat(t.Trailer!=null?People(t.Trailer):Enumerable.Empty<string>());
        /// <summary>Checks that the authored template and its occupants share the accepted faction.</summary>
        internal static bool Compatible(CarTemplate t,string faction)=>t!=null&&TrafficFactions.Normalize(t.Faction)==TrafficFactions.Normalize(faction)&&People(t).All(p=>Allowed(p,faction));
        /// <summary>Infers the template faction from its authored occupant identities.</summary>
        internal static string Infer(TemplateFile t)=>new[]{t.driver,t.passenger,t.rider,t.rider2,t.rider3,t.rider4}.Concat(t.trailer!=null?new[]{t.trailer.driver,t.trailer.passenger,t.trailer.rider,t.trailer.rider2,t.trailer.rider3,t.trailer.rider4}:new string[0]).Any(p=>!Empty(p)&&Of(p)=="Coyotes")?"Coyotes":"Scrapyard";
    }
}
