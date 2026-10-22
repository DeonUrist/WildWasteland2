using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    // Reads the game's own HUD/FSM state and hides the vanilla HUD graphics this mod replaces (alpha only; no object or FSM is touched).
    internal sealed class Bindings
    {
        internal Canvas Canvas;
        internal Transform Survival, Loading, DesignUI, Codex, Saving;
        internal Text UIText;
        internal Text[] CodexTexts;
        internal Text Health, Armor, ArmorMax, AmmoInWeapon, AmmoReserve, NativeFontSource, Thirst, Hunger, Fatigue, Sanity;
        internal FsmInt Grenades, Money;
        internal Transform[] SlotHolders = new Transform[3];
        internal PlayMakerFSM Weapons, Bandage, FirstAid;
        internal Transform PlayerCamera;
        private readonly Dictionary<Graphic, float> _hidden = new Dictionary<Graphic, float>();
        private bool _hiddenCaptured, _culled;
        private readonly Dictionary<Graphic, bool> _cull = new Dictionary<Graphic, bool>();   // original cullTransparentMesh
        private int _pocketUiRevision = -1;
        internal Transform HudRoot;
        internal readonly ApocapocketInventory Pocket = new ApocapocketInventory();
        internal int SlotCount { get { return Pocket.Active ? Pocket.Count : 3; } }
        internal bool Ordinary(int slot) { return Pocket.Active && slot >= 0 && slot < 6 && Pocket.Ordinary[slot]; }
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly PlayMakerFSM[] _slotFsms = new PlayMakerFSM[3];
        private readonly FsmGameObject[] _slotVars = new FsmGameObject[3];
        private readonly FsmBool[] _selected = new FsmBool[3];

        // Replaced by CleanHUD. Radiation (label + its background) stays native.
        private static readonly string[] HiddenRoots = {
            "Design_UI", "SurvivalCanvas", "WeaponSlots_UI", "QuickItemSlots_UI", "ArmorLable", "AmmoLable", "NutsLable", "Compass",
            "WeaponSlot_1", "WeaponSlot_2", "WeaponSlot_3", "ArmorSlotImage", "WeaponSlotImage", "ItemSlotImage",
            "WeaponUse",   // the "unequip (0) / drop (p) / grenade (g)" hint text
            "UIText", "CodexEntry", "Saving game text UI" };   // shown in the message log instead

        internal static Bindings Find()
        {
            var root = SceneCache.FindPath("Canvas");
            var canvas = root != null ? root.GetComponent<Canvas>() : null;
            if (canvas != null)
            {
                if (canvas.name != "Canvas" || !canvas.isRootCanvas || canvas.transform.Find("WeaponSlots_UI") == null) return null;
                var b = new Bindings { Canvas = canvas };
                b.Health = b.TextAt("SurvivalCanvas/HealthLable/HealthText");
                b.Armor = b.TextAt("ArmorLable/ArmorText");
                b.ArmorMax = b.TextAt("ArmorLable/ArmorText/ArmorMaxText");
                if (b.Health == null || b.Armor == null) return null;
                b.AmmoInWeapon = b.TextAt("AmmoLable/AmmoInWeaponText");
                b.AmmoReserve = b.TextAt("AmmoLable/AmmoInWeaponText/AmmoText");
                b.NativeFontSource = b.AmmoInWeapon ?? b.Health;
                b.Thirst = b.TextAt("SurvivalCanvas/ThirstLable/ThirstText");
                b.Hunger = b.TextAt("SurvivalCanvas/HungerLable/HungerText");
                b.Fatigue = b.TextAt("SurvivalCanvas/FatigueLable/FatigueText");
                b.Sanity = b.TextAt("SurvivalCanvas/SanityLable/SanityText");
                b.Survival = canvas.transform.Find("SurvivalCanvas");
                b.Loading = canvas.transform.Find("LoadingScreen");
                b.DesignUI = canvas.transform.Find("Design_UI");
                b.UIText = b.TextAt("UIText");
                b.Codex = canvas.transform.Find("CodexEntry");
                b.Saving = canvas.transform.Find("Saving game text UI");
                b.CodexTexts = b.Codex != null ? b.Codex.GetComponentsInChildren<Text>(true) : new Text[0];
                b.Refresh();
                return b;
            }
            return null;
        }

        private Text TextAt(string path) { var t = Canvas.transform.Find(path); return t != null ? t.GetComponent<Text>() : null; }

        internal void Refresh()
        {
            if (Canvas == null) return;
            if (Weapons == null) Weapons = SceneCache.Fsm("Weapons", "Weapons");
            if (Bandage == null) Bandage = SceneCache.Fsm("Bandage_Item", "SlotEmptyFull");
            if (FirstAid == null) FirstAid = SceneCache.Fsm("FirstAid_Item", "SlotEmptyFull");
            if (PlayerCamera == null) { var f = SceneCache.Fsm("PlayerCamera", "GrabItem"); if (f != null) PlayerCamera = f.transform; }
            if (Grenades == null) { var f = SceneCache.Fsm("__GameManager__", "Ammo"); if (f != null) Grenades = f.FsmVariables.FindFsmInt("ammo_grenade"); }
            if (Grenades == null) Grenades = FsmVariables.GlobalVariables.FindFsmInt("ammo_grenade");
            if (Money == null) Money = FsmVariables.GlobalVariables.FindFsmInt("Money");
            if (Weapons != null) for (int i = 0; i < 3; i++)
            {
                if (_selected[i] == null) _selected[i] = Weapons.FsmVariables.FindFsmBool(i == 0 ? "weapon1_Bool" : i == 1 ? "weapon2_Bool" : "weapon3_Bool");
                if (SlotHolders[i] != null) continue;
                SlotHolders[i] = Weapons.transform.Find(i == 0 ? "Slot 1" : i == 1 ? "Slot 2" : "Slot 3");
                if (SlotHolders[i] == null) continue;
                _slotFsms[i] = Fsm(SlotHolders[i].gameObject, "SlotEmptyFull");
                if (_slotFsms[i] != null) _slotVars[i] = _slotFsms[i].FsmVariables.FindFsmGameObject("item");
            }
            if (_hiddenCaptured && _pocketUiRevision == ApocapocketInventory.UiRevision) return;
            _hiddenCaptured = true;
            _pocketUiRevision = ApocapocketInventory.UiRevision;
            foreach (var path in HiddenRoots)
            {
                var t = Canvas.transform.Find(path); if (t == null) continue;
                _graphics.Clear(); t.GetComponentsInChildren<Graphic>(true, _graphics);
                foreach (var g in _graphics)
                {
                    if (PathOf(g.transform).IndexOf("radiation", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (!_hidden.ContainsKey(g)) _hidden.Add(g, g.canvasRenderer.GetAlpha());
                }
            }
            ApocapocketInventory.CaptureUi(this, HudRoot);
            _culled = false;
        }

        internal void CaptureExtra(Graphic g) { float a; if (!_hidden.TryGetValue(g, out a) || a == 0f) { a = g.canvasRenderer.GetAlpha(); _hidden[g] = a == 0f ? 1f : a; _culled = false; } }

        private static string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

        internal void Hide()
        {
            // Hidden graphics are also culled (not drawn at all) while their alpha is 0; Restore puts the original flag back.
            if (!_culled)
            {
                _culled = true;
                foreach (var pair in _hidden) if (pair.Key != null)
                { var r = pair.Key.canvasRenderer; if (!_cull.ContainsKey(pair.Key)) _cull.Add(pair.Key, r.cullTransparentMesh); r.cullTransparentMesh = true; }
            }
            foreach (var pair in _hidden) if (pair.Key != null && pair.Key.canvasRenderer.GetAlpha() != 0f) pair.Key.canvasRenderer.SetAlpha(0f);
        }
        internal void RefreshHiddenIfNeeded() { if (!_hiddenCaptured || _pocketUiRevision != ApocapocketInventory.UiRevision) Refresh(); }
        internal void Restore()
        {
            foreach (var pair in _hidden) if (pair.Key != null) pair.Key.canvasRenderer.SetAlpha(pair.Value);
            foreach (var pair in _cull) if (pair.Key != null) pair.Key.canvasRenderer.cullTransparentMesh = pair.Value;
            _culled = false;
        }

        internal bool Visible
        {
            get
            {
                return Canvas != null && Canvas.enabled && Canvas.gameObject.activeInHierarchy
                    && Survival != null && Survival.gameObject.activeInHierarchy
                    && (Loading == null || !Loading.gameObject.activeInHierarchy);
            }
        }

        internal GameObject Item(int slot)
        {
            if (Pocket.Active) return slot >= 0 && slot < 6 ? Pocket.Items[slot] : null;
            if (slot < 0 || slot >= 3 || SlotHolders[slot] == null) return null;
            var f = _slotFsms[slot];
            if (f != null && f.ActiveStateName == "empty") return null;
            if (f != null)
            {
                var v = _slotVars[slot];
                if (v != null && v.Value != null && v.Value.transform.parent == SlotHolders[slot]) return v.Value;
            }
            return SlotHolders[slot].childCount > 0 ? SlotHolders[slot].GetChild(0).gameObject : null;
        }

        // Drawn slot 0..2, or -1 when holstered.
        internal int Selected
        {
            get
            {
                if (Pocket.Active) return Pocket.Selected;
                if (Weapons == null) return -1;
                var s = Weapons.ActiveStateName;
                if (s == "Slot 1") return 0;
                if (s == "Slot 2") return 1;
                if (s == "Slot 3") return 2;
                if (s == "off 2") return -1;
                for (int i = 0; i < 3; i++) { var v = _selected[i]; if (v != null && v.Value) return i; }
                return -1;
            }
        }

        internal static PlayMakerFSM Fsm(GameObject go, string name) { return WildWasteland.Core.FsmUtil.Find(go, name); }

        private sealed class ChargeBinding { internal Transform Child; internal FsmInt Quantity; }
        private static readonly Dictionary<PlayMakerFSM, ChargeBinding> ChargeCache = new Dictionary<PlayMakerFSM, ChargeBinding>();
        internal static void Clear() { ChargeCache.Clear(); TextCache.Clear(); }
        internal static int Charges(PlayMakerFSM slot)
        {
            if (slot == null || slot.ActiveStateName == "empty" || slot.transform.childCount == 0) return 0;
            ChargeBinding binding;
            if (!ChargeCache.TryGetValue(slot, out binding)) { binding = new ChargeBinding(); ChargeCache.Add(slot, binding); }
            var child = slot.transform.GetChild(0);
            if (binding.Child != child)
            {
                binding.Child = child;
                var f = Fsm(child.gameObject, "Quantity");
                binding.Quantity = f != null ? f.FsmVariables.FindFsmInt("Quantity") : null;
            }
            return binding.Quantity != null ? Mathf.Max(0, binding.Quantity.Value) : 1;
        }
        internal static string ItemName(GameObject item)
        {
            var f = Fsm(item, "ItemName");
            var s = f != null ? f.FsmVariables.FindFsmString("ItemName") : null;
            return s != null ? s.Value : null;
        }

        private sealed class TextValue { internal string NumberSource, DigitSource, Digits; internal float Number; }
        private static readonly Dictionary<Text, TextValue> TextCache = new Dictionary<Text, TextValue>();
        private static TextValue Value(Text t)
        {
            TextValue value;
            if (!TextCache.TryGetValue(t, out value)) { value = new TextValue(); TextCache.Add(t, value); }
            return value;
        }
        internal static string Digits(Text t)
        {
            if (t == null) return "";
            var value = Value(t); var s = t.text ?? "";
            if (value.DigitSource == s) return value.Digits;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s) if (c >= '0' && c <= '9') sb.Append(c);
            value.DigitSource = s; value.Digits = sb.ToString(); return value.Digits;
        }
        internal static float Number(Text t)
        {
            if (t == null) return 0f;
            var value = Value(t); var s = t.text ?? "";
            if (value.NumberSource == s) return value.Number;
            int i = 0;
            while (i < s.Length && !(s[i] >= '0' && s[i] <= '9') && s[i] != '-') i++;
            int j = i; while (j < s.Length && ((s[j] >= '0' && s[j] <= '9') || s[j] == '.' || s[j] == ',' || (j == i && s[j] == '-'))) j++;
            float v;
            value.Number = j > i && float.TryParse(s.Substring(i, j - i).Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
            value.NumberSource = s; return value.Number;
        }
    }
}
