using UnityEngine;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Owns actual canine rig registration, samples and reversible visual operations.</summary>
    internal sealed class CaninePresentation : ICaninePresentation,ICanineRestPolicy
    {
        /// <summary>Reports whether this provider currently permits its owned feature.</summary>
        public bool Enabled=>PresentationModule.Running && CharacterSettings.DogAnimationsEnabled.Value && CanineVisuals.BundleLoaded;
        /// <summary>Returns the number of registered canine rigs.</summary>
        public int DogCount=>CanineVisuals.DogCount;
        /// <summary>Returns the number of mapped canine clips.</summary>
        public int ClipCount=>CanineVisuals.ClipCount;
        /// <summary>Reports whether the canine clip bundle loaded usable mappings.</summary>
        public bool BundleLoaded=>CanineVisuals.BundleLoaded;
        /// <summary>Releases every canine rig while retaining loaded clip assets for restart.</summary>
        public void Clear()=>CanineVisuals.OnSceneLoaded();
        /// <summary>Returns the Presentation-owned lying-down policy.</summary>
        public bool Resting { get { return CharacterSettings.DogsLieDown.Value; } }
        /// <summary>Returns the exact registered canine handoff record for this actor.</summary>
        public CanineState Find(GameObject actor) { CanineState state;return actor!=null && CanineVisuals.Dogs.TryGetValue(actor.GetInstanceID(),out state)?state:null; }
        /// <summary>Registers the actual actor rig against its mapped prefab clips.</summary>
        public bool Register(IAnimalActor actor,string prefab,ICanineCompanion companion) { return CanineVisuals.RegisterRig(actor,prefab,companion); }
        /// <summary>Reports whether the current Presentation driver owns this canine rig.</summary>
        public bool Owns(GameObject actor)=>CanineVisuals.Owns(actor);
        /// <summary>Restores and releases one external companion rig.</summary>
        public void Remove(GameObject actor)=>CanineVisuals.RemoveCompanion(actor);
        /// <summary>Advances the companion decision and clip clocks once after its native movement.</summary>
        public void Tick(GameObject actor)=>CanineVisuals.TickCompanion(actor);
        /// <summary>Samples the companion visual after the external animator.</summary>
        public void Late(GameObject actor)=>CanineVisuals.LateCompanion(actor);
        /// <summary>Restores the last native bone sample before another animation pass.</summary>
        public void Restore(GameObject actor)=>CanineVisuals.RestoreCompanion(actor);
        /// <summary>Reports the active committed dash of the supplied companion.</summary>
        public bool Dashing(GameObject actor)=>CanineVisuals.CompanionDashing(actor);
        /// <summary>Checks current rig, actor and distance gates before applying canine decisions.</summary>
        public bool Eligible(CanineState d)=>CanineVisuals.Eligible(d);
        /// <summary>Captures native visual state before the Presentation driver takes over.</summary>
        public void Prepare(CanineState d)=>CanineVisuals.Prepare(d);
        /// <summary>Restores native canine state through its current Presentation owner.</summary>
        public void Release(CanineState d)=>CanineVisuals.Release(d);
        /// <summary>Requests a pose change with the existing clip cross-fade state.</summary>
        public void Switch(CanineState d,CaninePose pose,string clip)=>CanineVisuals.Switch(d,pose,clip);
        /// <summary>Restores the last native bone sample before another animation pass.</summary>
        public void Restore(CanineState d)=>CanineVisuals.Restore(d);
        /// <summary>Restores native drag and movement ownership after a jumping bite.</summary>
        public void ReleaseJump(CanineState d)=>CanineVisuals.ReleaseJump(d);
        /// <summary>Restores velocity, drag and movement ownership after a committed dash.</summary>
        public void ReleaseDash(CanineState d)=>CanineVisuals.ReleaseDash(d);
        /// <summary>Checks native or companion feet against the existing ground layers.</summary>
        public bool Grounded(CanineState d)=>CanineVisuals.Grounded(d);
    }
}
