using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
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
        }
        private readonly List<Arrow> _arrows = new List<Arrow>();
        private readonly Transform _parent;
        private int _active;
        internal DamageDirection(Transform parent)
        {
            var go = new GameObject("Damage direction canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false); _parent = go.transform;
            var rt = (RectTransform)_parent;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var canvas = go.GetComponent<Canvas>(); canvas.overrideSorting = false;
            var source = parent.GetComponentInParent<Canvas>(); if (source != null) canvas.additionalShaderChannels = source.additionalShaderChannels;
            for (int i = 0; i < 4; i++) _arrows.Add(Create());
            Services.NpcHurt += Hurt;
        }

        private void Hurt(GameObject victim, GameObject attacker)
        {
            if (!CleanHudModule.DamageDirection.Value || !GameRefs.InGame || !_parent.gameObject.activeInHierarchy) return;
            var player = GameRefs.Player;
            if (player == null || victim == null || attacker == null || !victim.transform.IsChildOf(player.transform) || attacker.transform.IsChildOf(player.transform)) return;
            var target = attacker.transform;
            // Keep an NPC's head/weapon hits under the same identity without merging occupants of a vehicle.
            for (var t = target; t != null; t = t.parent)
                if (FsmUtil.Find(t.gameObject, "Health") != null) { target = t; break; }
            Arrow arrow = null;
            foreach (var a in _arrows) if (a.Active && a.Attacker == target) { arrow = a; break; }
            if (arrow == null) foreach (var a in _arrows) if (!a.Active) { arrow = a; break; }
            if (arrow == null) { arrow = Create(); _arrows.Add(arrow); }
            arrow.Attacker = target; arrow.Offset = target.position - player.transform.position;
            arrow.HitTime = Time.unscaledTime; arrow.Group.alpha = 1f;
            if (!arrow.Active) { arrow.Active = true; _active++; arrow.Root.gameObject.SetActive(true); }
        }

        private Arrow Create()
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
                var image = bar.GetComponent<Image>(); image.raycastTarget = false; image.color = new Color(.85f, .16f, .12f, 1);
            }
            go.SetActive(false); return new Arrow { Root = rt, Group = group };
        }

        internal void Tick(bool visible)
        {
            if (_active == 0) return;
            var camera = GameRefs.MainCamera; var player = GameRefs.Player;
            bool show = visible && CleanHudModule.DamageDirection.Value && GameRefs.InGame && camera != null && player != null;
            float now = Time.unscaledTime, heading = camera != null ? camera.transform.eulerAngles.y : 0f;
            float scale = CleanHudModule.Scale.Value; var size = Vector3.one * (scale * 1.15f);
            var playerPosition = player != null ? player.transform.position : Vector3.zero;
            foreach (var a in _arrows)
            {
                if (!a.Active) continue;
                float age = now - a.HitTime;
                if (!show || age >= 2f) { Hide(a); continue; }
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

        private void Hide(Arrow a) { a.Active = false; _active--; a.Attacker = null; a.Root.gameObject.SetActive(false); }
        public void Dispose() { Services.NpcHurt -= Hurt; _arrows.Clear(); _active = 0; }
    }
}
