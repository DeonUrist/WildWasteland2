using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    // Existing NPC notifications cover native attacks and Guns hits, including armor-only hits.
    internal sealed class DamageDirection : IDisposable
    {
        private sealed class Arrow
        {
            internal Transform Attacker;
            internal Vector3 Offset;
            internal float HitTime;
            internal RectTransform Root;
            internal CanvasGroup Group;
            internal bool Active;
            internal bool Pale;
        }
        private readonly List<Arrow> _arrows = new List<Arrow>();
        private readonly Transform _parent;
        private int _active;
        private readonly Arrow[] _paranoia = new Arrow[128];
        private readonly List<GameObject> _enemies = new List<GameObject>(32);
        private float _nextParanoia, _urbanUntil;
        private int _paranoiaBuilt;
        /// <summary>Constructs the attacker direction arrows and captures its owning resources.</summary>
        internal DamageDirection(Transform parent)
        {
            var go = new GameObject("Damage direction canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false); _parent = go.transform;
            var rt = (RectTransform)_parent;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var canvas = go.GetComponent<Canvas>(); canvas.overrideSorting = false;
            var source = parent.GetComponentInParent<Canvas>(); if (source != null) canvas.additionalShaderChannels = source.additionalShaderChannels;
            for (int i = 0; i < 4; i++) _arrows.Add(Create());
            ModEvents.Subscribe<Core.Events.PlayerWeaponKill>(HudModule.Instance, OnSkillKill);
            ModEvents.Subscribe<Core.Events.AttackObserved>(HudModule.Instance,OnAttackObserved);
        }

        /// <summary>Resolves hurt for the attacker direction arrows.</summary>
        /// <summary>Resolves tactical contact ids while the observed objects are still alive.</summary>
        private void OnAttackObserved(ref Core.Events.AttackObserved evt) { Hurt(Entities.Find(evt.Victim),Entities.Find(evt.Attacker)); }
        private void Hurt(GameObject victim, GameObject attacker)
        {
            if (!HudModule.DamageDirection.Value || !GameRefs.InGame || !_parent.gameObject.activeInHierarchy) return;
            var player = GameRefs.Player;
            if (player == null || victim == null || attacker == null || !victim.transform.IsChildOf(player.transform) || attacker.transform.IsChildOf(player.transform)) return;
            var target = attacker.transform;
            // Keep an NPC's head/weapon hits under the same identity without merging occupants of a vehicle.
            for (var t = target; t != null; t = t.parent)
                if (FsmUtil.Find(t.gameObject, "Health") != null) { target = t; break; }
            Arrow arrow = null;
            foreach (var a in _arrows) if (!a.Pale && a.Active && a.Attacker == target) { arrow = a; break; }
            if (arrow == null) foreach (var a in _arrows) if (!a.Pale && !a.Active) { arrow = a; break; }
            if (arrow == null) { arrow = Create(); _arrows.Add(arrow); }
            arrow.Attacker = target; arrow.Offset = target.position - player.transform.position;
            arrow.HitTime = Time.unscaledTime; arrow.Group.alpha = 1f;
            if (!arrow.Active) { arrow.Active = true; _active++; arrow.Root.gameObject.SetActive(true); }
        }

        /// <summary>Creates the attacker direction arrows.</summary>
        private Arrow Create(bool pale = false)
        {
            var go = new GameObject("Damage direction", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = new Vector2(24, 16);
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0;
            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject("Arrow", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(rt, false);
                var b = (RectTransform)bar.transform;
                b.sizeDelta = new Vector2(12, 3); b.anchoredPosition = new Vector2(i == 0 ? -4 : 4, 0);
                b.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
                var image = bar.GetComponent<Image>(); image.raycastTarget = false; image.color = pale ? new Color(.9f, .92f, .88f, .65f) : new Color(.85f, .16f, .12f, 1);
            }
            go.SetActive(false); return new Arrow { Root = rt, Group = group, Pale = pale };
        }

        private void OnSkillKill(ref Core.Events.PlayerWeaponKill kill) { if (SkillPerks.Rifle(kill.Kind) && SkillPerks.Has("UrbanWarrior")) _urbanUntil = Time.time + 5; }
        private void Paranoia(bool visible)
        {
            bool enabled = visible && HudModule.DamageDirection.Value && GameRefs.InGame && ((ModApi.Get<IPlayerPerkEffects>()?.Paranoia ?? false) || Time.time < _urbanUntil);
            if (!enabled)
            { for (int i = 0; i < _paranoiaBuilt; i++) if (_paranoia[i].Active) Hide(_paranoia[i]); return; }
            if (_paranoiaBuilt < _paranoia.Length)
            { var arrow = Create(true); _paranoia[_paranoiaBuilt++] = arrow; _arrows.Add(arrow); }
            if (Time.unscaledTime < _nextParanoia) return;
            _nextParanoia = Time.unscaledTime + .25f;
            _enemies.Clear(); var player = GameRefs.Player;
            if (player != null) ModApi.Get<INpcEnemies>()?.NearPlayer(player.transform.position, Time.time < _urbanUntil ? 200 : 30, _enemies);
            for (int i = 0; i < _paranoiaBuilt; i++)
            {
                var arrow = _paranoia[i];
                if (i >= _enemies.Count || _enemies[i] == null) { if (arrow.Active) Hide(arrow); continue; }
                arrow.Attacker = _enemies[i].transform; arrow.Group.alpha = 1f;
                foreach (var image in arrow.Root.GetComponentsInChildren<Image>()) image.color = Time.time < _urbanUntil ? new Color(1,.85f,.1f,1) : new Color(.9f,.92f,.88f,.65f);
                if (!arrow.Active) { arrow.Active = true; _active++; arrow.Root.gameObject.SetActive(true); }
            }
        }

        /// <summary>Updates the attacker direction arrows.</summary>
        internal void Tick(bool visible)
        {
            Paranoia(visible);
            if (_active == 0) return;
            var camera = GameRefs.MainCamera; var player = GameRefs.Player;
            bool show = visible && HudModule.DamageDirection.Value && GameRefs.InGame && camera != null && player != null;
            float now = Time.unscaledTime, heading = camera != null ? camera.transform.eulerAngles.y : 0f;
            float scale = HudModule.Scale.Value; var size = Vector3.one * (scale * 1.15f);
            var playerPosition = player != null ? player.transform.position : Vector3.zero;
            foreach (var a in _arrows)
            {
                if (!a.Active) continue;
                float age = now - a.HitTime;
                if (!show || !a.Pale && age >= 2f || a.Pale && a.Attacker == null) { Hide(a); continue; }
                if (a.Attacker != null) a.Offset = a.Attacker.position - playerPosition;
                var direction = new Vector3(a.Offset.x, 0, a.Offset.z);
                if (direction.sqrMagnitude < .0001f) { Hide(a); continue; }
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - heading;
                float angle = yaw * Mathf.Deg2Rad;
                var position = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (90f * scale);
                var rotation = Quaternion.Euler(0, 0, -yaw);
                if (a.Root.anchoredPosition != position) a.Root.anchoredPosition = position;
                if (a.Root.localRotation != rotation) a.Root.localRotation = rotation;
                if (a.Root.localScale != size) a.Root.localScale = size;
            }
        }

        /// <summary>Hides the attacker direction arrows.</summary>
        private void Hide(Arrow a) { a.Active = false; _active--; a.Attacker = null; a.Root.gameObject.SetActive(false); }
        /// <summary>Destroys owned attacker direction arrows resources and releases scene bindings.</summary>
        public void Dispose() { ModEvents.Unsubscribe<Core.Events.AttackObserved>(HudModule.Instance,OnAttackObserved); _arrows.Clear(); _active = 0; }
    }
}
