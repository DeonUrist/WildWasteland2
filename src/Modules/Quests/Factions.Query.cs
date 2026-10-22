using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quests factions, Core query part (Wave A, from Quests/Factions.cs; B01 feeds Core.Factions with it): which faction an NPC/camp belongs to (factions.txt, Unity tag, home camp), and whether two factions are friendly or at war.</summary>
    internal static partial class Factions
    {
        public static string[] HumanFactions = new string[0];   // tags that are factions of people (Scrapyard, Coyotes)

        /// <summary>Tests normalized tags against the authored human faction catalog.</summary>
        static bool IsHuman(string tag)
        {
            string n = Util.Norm(tag);
            if (n.Length == 0) return false;
            for (int i = 0; i < HumanFactions.Length; i++) if (HumanFactions[i] == n) return true;
            return false;
        }

        // like OfObject, but for something that is known to be a person (a hireling): no need for its name to be in the people list
        /// <summary>Resolves a known person by authored name, native faction tag or nearest home POI.</summary>
        public static Faction OfPerson(string tag, string name, Vector3 world)
        {
            for (int i = 0; i < file.Count; i++) if (file[i].HasEnemy(name)) return file[i];
            if (IsHuman(tag)) return Get(tag);
            Poi home = HomeOf(world);
            return home != null ? OwnerOfPoi(home) : null;
        }

        /// <summary>Recognizes authored NPC names and configured person-name words.</summary>
        static bool IsPerson(string name)
        {
            string n = Util.Norm(name);
            for (int i = 0; i < PeopleWords.Length; i++) if (PeopleWords[i].Length > 0 && n.Contains(PeopleWords[i])) return true;
            for (int i = 0; i < file.Count; i++) if (file[i].HasEnemy(name)) return true;
            return false;
        }

        // The POI (camp, cave, building, wreck) the thing at `world` lives at, or null.
        /// <summary>Selects the nearest known POI within the configured home radius.</summary>
        public static Poi HomeOf(Vector3 world)
        {
            List<Poi> near = Game.FindPois(world, 0f, HomeRadius, "");
            Poi best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < near.Count; i++)
            {
                float d = Game.HDist(near[i].Pos, world);
                if (d < bd) { bd = d; best = near[i]; }
            }
            return best;
        }

        // Who owns a point of interest: factions.txt first, otherwise the game's own EnemySpawn_<Faction> marker.
        /// <summary>Resolves POI ownership from saved overrides, authored faction lists and native spawn faction.</summary>
        public static Faction OwnerOfPoi(Poi p)
        {
            string taken;
            if (Overrides.Count > 0 && Overrides.TryGetValue(KeyOf(p), out taken)) return Get(taken);
            for (int i = 0; i < file.Count; i++) if (file[i].HasPoi(p.Name)) return file[i];
            if (p.Faction.Length > 0 && IsHuman(p.Faction)) return Get(p.Faction);
            return null;
        }

        // Faction of an NPC or enemy: factions.txt by name, then its Unity tag (the game's own faction), then, for
        // people without a faction tag, the camp it lives at. null = no faction (animals, mutants, wanderers).
        /// <summary>Resolves NPC faction from authored identity, native tag and, for recognized people, their home POI.</summary>
        public static Faction OfObject(string tag, string name, Vector3 world)
        {
            for (int i = 0; i < file.Count; i++) if (file[i].HasEnemy(name)) return file[i];     // npcs and enemies listed in factions.txt
            if (IsHuman(tag)) return Get(tag);
            if (IsPerson(name))
            {
                Poi home = HomeOf(world);
                if (home != null) return OwnerOfPoi(home);
            }
            return null;
        }

        /// <summary>Tests whether one authored faction explicitly lists another as an ally.</summary>
        static bool Listed(Faction f, Faction other)
        {
            for (int i = 0; i < f.Allies.Count; i++) if (f.Allies[i] == other.Key) return true;
            return false;
        }

        // same faction, or either one lists the other as an ally; no faction counts as friendly to everyone
        /// <summary>Treats missing factions, equal factions and either-direction authored alliances as friendly.</summary>
        public static bool Friendly(Faction a, Faction b)
        {
            if (a == null || b == null) return true;
            return a.Key == b.Key || Listed(a, b) || Listed(b, a);
        }

        /// <summary>Tests an explicit either-direction war declaration between distinct known factions.</summary>
        public static bool AtWar(Faction a, Faction b)
        {
            if (a == null || b == null || a.Key == b.Key) return false;
            return a.War.Contains(b.Key) || b.War.Contains(a.Key);
        }
    }
}
