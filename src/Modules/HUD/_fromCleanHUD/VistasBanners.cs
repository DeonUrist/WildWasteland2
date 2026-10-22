using WildWasteland.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.HUD
{
    internal static class VistasBanners
    {
        private static GameObject _root;
        private static CanvasGroup _group;
        private static GlyphText _name, _detail;
        private static RectTransform _panel;
        private static string _pendingName, _pendingDetail;
        private static float _until;
        static IPlaceBanners _source;
        internal static void Init(HudModule owner)
        {
            ModApi.Changed += ProviderChanged;
            BindSource();
            Scheduler.EveryFrame(owner, "Vistas place banners", Tick, Phase.LateUpdateLast);
        }
        private static bool Enabled() { return HudModule.Running && HudModule.CleanHudEnabled.Value; }
        private static void Entered(string name, string detail)
        {
            if (!Enabled()) return;
            _pendingName = name; _pendingDetail = detail; _until = Time.unscaledTime + 5f;
        }
        private static void Build()
        {
            _root = new GameObject("CleanHUD.Place", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            _group = _root.GetComponent<CanvasGroup>(); _group.interactable = _group.blocksRaycasts = false;
            var panel = DialogueWidgets.Rect(_root.transform, "place", 0, (_source != null ? _source.Height : .15f) * 1080f, 960, 110); _panel = panel;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1); panel.pivot = new Vector2(.5f, 1);
            var line = DialogueWidgets.Rect(panel, "accent", 180, 94, 600, 3).gameObject.AddAccent<Image>(); line.color = DialogueWidgets.Gold;
            _name = DialogueWidgets.Text(panel, "name", 0, 0, 960, 58, 42, DialogueWidgets.Gold, ""); _name.Align = TextAnchor.MiddleCenter;
            _detail = DialogueWidgets.Text(panel, "biome", 0, 58, 960, 32, 24, Color.white, ""); _detail.Align = TextAnchor.MiddleCenter;
            var shadow = _name.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(1, -2);
        }
        private static void Tick()
        {
            bool shown = Enabled() && _until > Time.unscaledTime && GameRefs.InGame && !SaveBus.Busy && !Core.UI.ToolWindow.IsOpen;
            if (shown && _root == null) Build();
            if (_root == null) return;
            if (_root.activeSelf != shown) _root.SetActive(shown);
            if (!shown) return;
            _panel.anchoredPosition = new Vector2(0, -(_source != null ? _source.Height : .15f) * 1080f);
            _name.Set(_pendingName); _detail.Set(_pendingDetail);
            _group.alpha = Mathf.Min(Mathf.Clamp01((_until - Time.unscaledTime) / .75f), Mathf.Clamp01((Time.unscaledTime - _until + 5f) / .3f));
        }
        internal static void Reset() { _until = 0; if (_root != null) UnityEngine.Object.Destroy(_root); _root = null; }
        /// <summary>Unsubscribes event sources before the HUD releases its display.</summary>
        internal static void Shutdown() { ModApi.Changed -= ProviderChanged; if (_source != null) _source.Entered -= Entered; _source = null; Reset(); }
        /// <summary>Rebinds when the optional generator bridge changes.</summary>
        static void ProviderChanged(System.Type type) { if (type == typeof(IPlaceBanners)) BindSource(); }
        /// <summary>Uses the currently enabled banner source and releases the previous one.</summary>
        static void BindSource() { if (_source != null) _source.Entered -= Entered; _source = ModApi.Get<IPlaceBanners>(); if (_source != null) _source.Entered += Entered; }
    }
}
