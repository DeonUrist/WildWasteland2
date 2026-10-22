using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Modules.Presentation;

namespace WildWasteland.Modules.Presentation
{
    internal static class CharacterModels
    {
        static readonly string[] Names = { "Flexa", "Flexa", "Merchant", "Bodyguard", "Sprokka", "Pistoleer", "Mechanic", "Organic_Mechanic", "Flexa", "Sprokka" };
        /// <summary>Number of character choices.</summary>
        internal static int ChoiceCount { get { return Names.Length; } }
        /// <summary>True for choices with their own skin (Female, Male, Gungirl, Shoota).</summary>
        internal static bool CustomSkin(int choice) { return choice < 2 || choice == 8 || choice == 9; }
        static readonly Dictionary<string, Animator> Sources = new Dictionary<string, Animator>();
        static float _nextFind;
        /// <summary>True when the chosen character uses a native NPC rig (Merchant, Bodyguard, Sprokka, Organic Mechanic, Gungirl, Shoota).</summary>
        internal static bool NativeSelection { get { return (int)CharacterSettings.Character.Value >= 2; } }
        /// <summary>The native rig of the chosen character, or null.</summary>
        internal static Animator SelectedSource() { return NativeSelection ? Source((int)CharacterSettings.Character.Value) : null; }
        /// <summary>The native NPC Animator of a choice (found once among the prefab assets).</summary>
        internal static Animator Source(int choice)
        {
            if (choice < 0 || choice >= Names.Length) return null;
            Animator source; string name = Names[choice];
            if (Sources.TryGetValue(name, out source) && source != null) return source;
            if (Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 5f;
                foreach (var animator in Resources.FindObjectsOfTypeAll<Animator>())
                    if (animator != null && !animator.gameObject.scene.IsValid() && animator.transform.Find("mixamorig:Hips") != null)
                    {
                        string root = animator.transform.root.name;
                        if (Array.IndexOf(Names, root) >= 0) Sources[root] = animator;
                    }
                Sources.TryGetValue(name, out source);
            }
            return source;
        }
    }
}
