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
        internal static string[] Travelers(string faction)=>TrafficFactions.Normalize(faction)=="Coyotes"?CoyoteTravelers:ScrapyardTravelers;
        internal static bool Empty(string person)=>string.IsNullOrWhiteSpace(person)||person=="None";
        internal static string Of(string person)=>Coyotes.Contains((person??"").Trim(),StringComparer.OrdinalIgnoreCase)?"Coyotes":"Scrapyard";
        internal static bool Allowed(string person,string faction)=>Empty(person)||Of(person)==TrafficFactions.Normalize(faction);
        internal static IEnumerable<string> People(CarTemplate t)=>new[]{t.Driver,t.Passenger}.Concat(t.Turrets).Concat(t.Trailer!=null?People(t.Trailer):Enumerable.Empty<string>());
        internal static bool Compatible(CarTemplate t,string faction)=>t!=null&&TrafficFactions.Normalize(t.Faction)==TrafficFactions.Normalize(faction)&&People(t).All(p=>Allowed(p,faction));
        internal static string Infer(TemplateFile t)=>new[]{t.driver,t.passenger,t.rider,t.rider2,t.rider3,t.rider4}.Concat(t.trailer!=null?new[]{t.trailer.driver,t.trailer.passenger,t.trailer.rider,t.trailer.rider2,t.trailer.rider3,t.trailer.rider4}:new string[0]).Any(p=>!Empty(p)&&Of(p)=="Coyotes")?"Coyotes":"Scrapyard";
    }
}
