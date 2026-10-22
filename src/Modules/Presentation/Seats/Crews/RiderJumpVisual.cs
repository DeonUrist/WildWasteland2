using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Presentation-owned temporary jump pose; traffic retains velocity, landing and awareness decisions.</summary>
    internal sealed class RiderJumpVisual : IDisposable
    {
        private IAnimatedCharacter _body;
        private PlayableGraph _graph;
        private bool _disposed;
        /// <summary>Uses the common character driver, preserving the native clip fallback when that driver cannot attach.</summary>
        internal RiderJumpVisual(GameObject actor)
        {
            var animator=actor.GetComponentInChildren<Animator>();
            if(animator==null || !animator.isHuman)return;
            try
            {
                _body=PlayerAnims.Attach(animator) as IAnimatedCharacter;
                if(_body!=null) { PlayerAnims.Jump(_body);return; }
                var clip=ModApi.Get<IAnimationApi>()?.GetClip("Jump");if(clip==null)return;
                _graph=PlayableGraph.Create("WildWasteland.RiderJump");_graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output=AnimationPlayableOutput.Create(_graph,"jump",animator);
                output.SetSourcePlayable(AnimationClipPlayable.Create(_graph,clip));_graph.Play();
            }
            catch(Exception error) { Dispose();Core.Log.Warn(PresentationModule.Instance,"Rider jump animation: "+error.Message); }
        }
        /// <summary>Releases the actual graph or common driver and removes its tracked lease.</summary>
        public void Dispose() { if(_disposed)return;_disposed=true;RiderVisuals.Remove(this);if(_graph.IsValid())_graph.Destroy();_body?.Dispose();_body=null; }
    }
}
