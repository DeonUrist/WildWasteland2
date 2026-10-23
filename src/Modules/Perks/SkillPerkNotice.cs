using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;
using WildWasteland.Core.Events;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Perks
{
    internal sealed class SkillPerkNotice
    {
        private readonly Queue<string> _pending = new Queue<string>();
        private GameObject _root;
        private RectTransform _card;
        private CanvasGroup _group;
        private RawImage _icon;
        private GlyphText _label;
        private AudioSource _audio;
        private AudioClip _sound;
        private bool _skillUp;
        private float _age = 4;
        internal void Init(IModule owner)
        {
            ModEvents.Subscribe<PlayerSkillPerkUnlocked>(owner, Unlocked);
            Scheduler.EveryFrame(owner, "Perks.Unlock notice", Tick, Phase.LateUpdateLast);
        }
        private void Unlocked(ref PlayerSkillPerkUnlocked e) { _pending.Enqueue(e.Id); }
        internal void SkillUp() { _skillUp = true; }
        private void Build()
        {
            _root = new GameObject("Perks.Unlock", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            _card = new GameObject("Earned perk", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>(); _card.SetParent(_root.transform,false);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(.5f,.5f); _card.sizeDelta = new Vector2(420,100);
            _group = _card.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            var icon = new GameObject("Icon",typeof(RectTransform),typeof(RawImage)); icon.transform.SetParent(_card,false);
            var rt = (RectTransform)icon.transform; rt.sizeDelta = new Vector2(64,64); rt.anchoredPosition = new Vector2(0,20);
            _icon = icon.GetComponent<RawImage>(); _icon.raycastTarget = false;
            var text = new GameObject("Perk name",typeof(RectTransform),typeof(GlyphText)); text.transform.SetParent(_card,false);
            rt = (RectTransform)text.transform; rt.sizeDelta = new Vector2(600,40); rt.anchoredPosition = new Vector2(0,-35);
            _label = text.GetComponent<GlyphText>(); _label.Size = 23; _label.Align = TextAnchor.MiddleCenter; _label.color = new Color(.89f,.75f,.42f);
            _audio = _root.AddComponent<AudioSource>(); _audio.spatialBlend = 0; _audio.playOnAwake = false;
            _sound = Core.Audio.TryLoadWav(System.IO.Path.Combine(Assets.ModuleDir("Perks"), "Sounds", "lvlup.wav"), "Skill up");
            if (_sound == null) Log.Warn(null, "[Perks] Could not load Sounds/lvlup.wav.");
        }
        private void Tick()
        {
            if (!GameRefs.InPlay || EditorSession.Busy) { if (_root != null) _root.SetActive(false); return; }
            bool playSound = _skillUp;
            if (_skillUp && _root == null) Build();
            if (_age >= 3 && _pending.Count > 0)
            {
                if (_root == null) Build();
                string id = _pending.Dequeue(); var perks = ModApi.Get<IPlayerPerks>();
                if (perks == null) return;
                for (int i = 0; i < perks.PerkCount; i++) { var p = perks.Perk(i); if (p.Id != id) continue; _icon.texture = p.Icon; _label.Set(p.Name.ToUpperInvariant()); break; }
                _age = 0; playSound = true;
            }
            if (_root == null) return;
            if (playSound)
            {
                _skillUp = false; _root.SetActive(true);
                if (_sound != null && !_audio.isPlaying) _audio.PlayOneShot(_sound);
            }
            _root.SetActive(_age < 3 || _audio.isPlaying);
            _card.gameObject.SetActive(_age < 3);
            if (_age >= 3) return;
            _age += Time.deltaTime;
            _card.anchoredPosition = new Vector2(0,110 + _age * 34);
            _group.alpha = Mathf.Clamp01(_age * 6) * Mathf.Clamp01((3 - _age) / 1.2f);
        }
        internal void Clear()
        { _pending.Clear(); _skillUp = false; _age = 4; if (_root != null) Object.Destroy(_root); if (_sound != null) Object.Destroy(_sound); _root = null; _sound = null; }
    }
}
