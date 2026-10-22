using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Modules.PlayerBody;

namespace WildWasteland.Modules.Presentation
{
    /// <summary>
    /// ICharacterPresentation provider (README 6.1): the id-based face of the single ModAPI character driver (ModCharacter + CharPlan).
    /// Human and female NPC rigs are attached through <see cref="ModAPI.Attach"/>; the player's body (Body + Loco, still its own driver
    /// until the unification step) answers the held-prop / muzzle / root queries for <see cref="Entities.Player"/>. Animals stay with the
    /// quadruped driver (DogAnimations, driven by NPCAI's registry), so Attach refuses Dog/Animal/Player kinds. Drivers may also send
    /// <see cref="CharacterAction"/> events instead of calling <see cref="Action"/>.
    /// </summary>
    internal sealed class CharacterPresentation : ICharacterPresentation
    {
        /// <summary>One attached character: its driver handle, kind, last inputs and the visual offset applied so far.</summary>
        sealed class Entry
        {
            internal ModAPI.Character Driver;
            internal CharacterKind Kind;
            internal CharacterInputs Inputs;
            internal Vector3 Offset, Applied;
        }

        readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();
        readonly List<long> _dead = new List<long>();

        /// <summary>Clips/bundles loaded (the player's Mixamo set is present).</summary>
        public bool Ready { get { return ModAPI.Ready; } }

        /// <summary>Number of characters attached through this service.</summary>
        public int Count { get { return _entries.Count; } }

        /// <summary>Starts driving a humanoid Animator (Human/Female kinds); false for other kinds, other owners or unsupported rigs.</summary>
        public bool Attach(EntityId id, Animator animator, CharacterKind kind)
        {
            if (!id.IsValid || animator == null || (kind != CharacterKind.Human && kind != CharacterKind.Female)) return false;
            Entry e;
            if (_entries.TryGetValue(id.Value, out e) && e.Driver != null && e.Driver.Valid) return e.Driver.Animator == animator;
            var c = ModAPI.Attach(animator);
            if (c == null) return false;
            _entries[id.Value] = new Entry { Driver = c, Kind = kind };
            return true;
        }

        /// <summary>Stops driving the character and restores its animator.</summary>
        public void Detach(EntityId id)
        {
            Entry e;
            if (!_entries.TryGetValue(id.Value, out e)) return;
            _entries.Remove(id.Value);
            RemoveOffset(e);
            if (e.Driver != null) e.Driver.Dispose();
        }

        /// <summary>True while the id is driven by the pipeline.</summary>
        public bool IsAttached(EntityId id) { return Get(id) != null; }

        /// <summary>Kind the character was attached as (the player reports Player while its body exists).</summary>
        public bool TryGetKind(EntityId id, out CharacterKind kind)
        {
            var e = Get(id);
            if (e != null) { kind = e.Kind; return true; }
            kind = CharacterKind.Player;
            return id == Entities.Player && PlayerBodyAlive();
        }

        /// <summary>Sets this tick's inputs (velocity, aim pitch, stance flags); read by the next animation update.</summary>
        public void SetInputs(EntityId id, ref CharacterInputs inputs)
        {
            var e = Get(id);
            if (e == null) return;
            e.Inputs = inputs;
            var c = e.Driver;
            c.ManualVelocity = true; c.Velocity = inputs.Velocity;
            c.AimPitch = inputs.AimPitch; c.Crouched = inputs.Crouched; c.Aiming = inputs.Aiming; c.Firing = inputs.Firing; c.Airborne = inputs.Airborne;
            c.TurnInPlace = inputs.TurnInPlace; c.WalkToStop = inputs.WalkToStop;
            if (inputs.Seated != c.Suspended) c.Suspended = inputs.Seated;
        }

        /// <summary>Last inputs given to the character.</summary>
        public bool TryGetInputs(EntityId id, out CharacterInputs inputs)
        {
            var e = Get(id);
            inputs = e != null ? e.Inputs : default(CharacterInputs);
            return e != null;
        }

        /// <summary>Plays a one-shot action (shoot, reload, pump, jump, kick, throws, strike/punch); Die suspends the graph.</summary>
        public void Action(EntityId id, CharacterActionKind action, float seconds, int rounds)
        {
            var e = Get(id);
            if (e == null) return;
            var c = e.Driver;
            switch (action)
            {
                case CharacterActionKind.Shoot: if (seconds > 0f) c.Shoot(seconds); else c.Shoot(); break;
                case CharacterActionKind.Reload: c.Reload(seconds > 0f ? seconds : 1f, rounds); break;
                case CharacterActionKind.CancelReload: c.CancelReload(); break;
                case CharacterActionKind.Pump: c.Pump(); break;
                case CharacterActionKind.Jump: c.Jump(); break;
                case CharacterActionKind.Kick: c.Kick(); break;
                case CharacterActionKind.Throw: c.Throw(true); break;
                case CharacterActionKind.ThrowLeft: c.Throw(false); break;
                case CharacterActionKind.Strike:
                case CharacterActionKind.Punch: if (seconds > 0f) c.Strike(seconds); else c.Strike(); break;
                case CharacterActionKind.Die: c.Suspended = true; break;
            }
        }

        /// <summary>CharacterAction event handler: the same as <see cref="Action"/> (subscribed by PresentationModule).</summary>
        internal void OnCharacterAction(ref CharacterAction evt) { Action(evt.Character, evt.Action, evt.Seconds, evt.Rounds); }

        /// <summary>The held weapon changed: the model goes to the right hand at the player's weapon poses (null = unarmed).</summary>
        public void WeaponChanged(EntityId id, Transform model, string weaponKey)
        {
            var e = Get(id);
            if (e != null) e.Driver.SetWeapon(model, weaponKey);
        }

        /// <summary>Suspends/resumes the character graph (the Animator's own controller shows while suspended).</summary>
        public void Suspend(EntityId id, bool suspended)
        {
            var e = Get(id);
            if (e != null) e.Driver.Suspended = suspended;
        }

        /// <summary>Muzzle of the held weapon: the player's third-person prop muzzle, or an NPC's weapon model forward axis.</summary>
        public bool TryGetMuzzle(EntityId id, out Vector3 position, out Vector3 direction)
        {
            position = direction = Vector3.zero;
            if (id == Entities.Player)
            {
                var body = BodyRunner.PlayerBody;
                if (!PlayerBodyAlive() || !body.TryWeaponMuzzle(out position)) return false;
                var prop = body.HeldProp;
                direction = prop != null ? prop.forward : Game.PlayerCamera != null ? Game.PlayerCamera.forward : Vector3.forward;
                return true;
            }
            var e = Get(id);
            var w = e != null ? e.Driver.HeldWeapon : null;
            if (w == null) return false;
            position = w.position; direction = w.forward;
            return true;
        }

        /// <summary>Held prop: the player's third-person weapon/tool model, or the weapon model an NPC character holds.</summary>
        public bool TryGetHeldProp(EntityId id, out Transform prop)
        {
            if (id == Entities.Player) { prop = PlayerBodyAlive() ? BodyRunner.PlayerBody.HeldProp : null; return prop != null; }
            var e = Get(id);
            prop = e != null ? e.Driver.HeldWeapon : null;
            return prop != null;
        }

        /// <summary>Character root: the player's body root, or the attached Animator's transform.</summary>
        public bool TryGetRoot(EntityId id, out Transform root)
        {
            if (id == Entities.Player) { root = PlayerBodyAlive() && BodyRunner.PlayerBody.Root != null ? BodyRunner.PlayerBody.Root.transform : null; return root != null; }
            var e = Get(id);
            root = e != null && e.Driver.Animator != null ? e.Driver.Animator.transform : null;
            return root != null;
        }

        /// <summary>Visual root offset (scene space) applied after the animation each LateUpdate, as a delta so other writers keep theirs.</summary>
        public void SetVisualOffset(EntityId id, Vector3 offset)
        {
            var e = Get(id);
            if (e != null) e.Offset = offset;
        }

        /// <summary>Animation update tier (0 = every frame); see ModAPI.Character.Tier.</summary>
        public int UpdateTier(EntityId id)
        {
            var e = Get(id);
            return e != null ? e.Driver.Tier : 0;
        }

        /// <summary>Job "Presentation.VisualOffsets" (LateUpdate, after the characters): applies changed visual offsets, drops dead entries.</summary>
        internal void LateTick()
        {
            if (_entries.Count == 0) return;
            _dead.Clear();
            foreach (var kv in _entries)
            {
                var e = kv.Value;
                if (e.Driver == null || !e.Driver.Valid || e.Driver.Animator == null) { _dead.Add(kv.Key); continue; }
                if (e.Offset == e.Applied) continue;
                e.Driver.Animator.transform.position += e.Offset - e.Applied;
                e.Applied = e.Offset;
            }
            for (int i = 0; i < _dead.Count; i++) _entries.Remove(_dead[i]);
        }

        /// <summary>Drops every entry (scene load / shutdown: the ModAPI characters are disposed separately).</summary>
        internal void Clear()
        {
            foreach (var e in _entries.Values) RemoveOffset(e);
            _entries.Clear();
        }

        /// <summary>Removes a still-applied visual offset from the character's transform.</summary>
        static void RemoveOffset(Entry e)
        {
            if (e.Applied == Vector3.zero || e.Driver == null || e.Driver.Animator == null) return;
            e.Driver.Animator.transform.position -= e.Applied; e.Applied = Vector3.zero;
        }

        /// <summary>The live entry of an id, or null (an entry whose driver died is dropped).</summary>
        Entry Get(EntityId id)
        {
            Entry e;
            if (!_entries.TryGetValue(id.Value, out e)) return null;
            if (e.Driver != null && e.Driver.Valid) return e;
            _entries.Remove(id.Value);
            return null;
        }

        /// <summary>True while the player's body exists and is alive.</summary>
        static bool PlayerBodyAlive() { var b = BodyRunner.PlayerBody; return PresentationModule.Running && b != null && b.Alive; }
    }
}
