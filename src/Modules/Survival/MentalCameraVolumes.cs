using System;
using System.Linq.Expressions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Survival
{
    /// <summary>Scopes native global post-process volumes to the affected player's camera, restoring them after each render.</summary>
    internal sealed class MentalCameraVolumes
    {
        private sealed class Weight
        {
            internal Component Volume;
            internal Func<float> Read;
            internal Action<float> Write;
            internal float Previous;
            internal bool Applied;
        }
        private readonly Insanity _state;
        private readonly IModule _owner;
        private Weight _sanity, _alcohol;
        private bool _subscribed;
        internal MentalCameraVolumes(Insanity state, IModule owner) { _state = state; _owner = owner; }
        internal void Bind(Component volume, bool alcohol)
        {
            var current = alcohol ? _alcohol : _sanity;
            if (volume == null || current != null && current.Volume == volume) return;
            try
            {
                var field = volume.GetType().GetField("weight");
                if (field == null || field.FieldType != typeof(float)) return;
                var value = Expression.Parameter(typeof(float), "weight");
                var access = Expression.Field(Expression.Constant(volume), field);
                var binding = new Weight { Volume = volume, Read = Expression.Lambda<Func<float>>(access).Compile(),
                    Write = Expression.Lambda<Action<float>>(Expression.Assign(access, value), value).Compile() };
                if (alcohol) _alcohol = binding; else _sanity = binding;
                if (!_subscribed) { Camera.onPreCull += BeforeCull; Camera.onPostRender += AfterRender; _subscribed = true; }
            }
            catch (Exception error) { Log.Error(_owner, "Mental break camera volume binding failed", error); }
        }
        private void BeforeCull(Camera camera)
        {
            Restore(_sanity); Restore(_alcohol);
            MentalBreakVisuals visuals;
            if (!_state.TryGetVisuals(Entities.Player, out visuals)) return;
            bool local = camera == GameRefs.MainCamera;
            Apply(_sanity, local ? visuals.InsanityWeight : 0);
            if (visuals.AlcoholDistortion) Apply(_alcohol, local ? 1 : 0);
        }
        private static void Apply(Weight binding, float value)
        { if (binding == null || binding.Volume == null) return; binding.Previous = binding.Read(); binding.Write(value); binding.Applied = true; }
        private static void Restore(Weight binding)
        { if (binding == null || !binding.Applied) return; if (binding.Volume != null) binding.Write(binding.Previous); binding.Applied = false; }
        private void AfterRender(Camera camera) { Restore(_sanity); Restore(_alcohol); }
        internal void End()
        {
            if (_subscribed) { Camera.onPreCull -= BeforeCull; Camera.onPostRender -= AfterRender; _subscribed = false; }
            Restore(_sanity); Restore(_alcohol); _sanity = _alcohol = null;
        }
    }
}
