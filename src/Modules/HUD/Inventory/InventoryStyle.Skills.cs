using System.Globalization;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.HUD
{
    internal static partial class InventoryStyle
    {
        private static RectTransform _skills;
        private static readonly RectTransform[] PerkSquares = new RectTransform[46];
        private static readonly UnityEngine.UI.RawImage[] PerkIcons = new UnityEngine.UI.RawImage[46];
        private static readonly UnityEngine.UI.Image[] PerkBorders = new UnityEngine.UI.Image[46];
        private static readonly Texture2D[] PerkGraySources = new Texture2D[46], PerkGrayIcons = new Texture2D[46];
        private static readonly string[] PerkHints = new string[46];
        private static readonly string[] PerkDescriptions = new string[46];
        private static GlyphText _skillHint, _noPerks;
        private static readonly System.Collections.Generic.List<GlyphText> HintLines = new System.Collections.Generic.List<GlyphText>();
        private static readonly RectTransform[] SkillRows = new RectTransform[12];
        private static readonly GlyphText[] SkillValues = new GlyphText[12];
        private static readonly GlyphText[] SkillNames = new GlyphText[12];
        private static readonly GlyphText[] SkillXp = new GlyphText[12];
        private static readonly int[] ShownSkills = new int[12], ShownXp = new int[12];
        private static readonly string[] SkillHints = new string[12];
        private static IPlayerPerks _skillsSource;
        private static string _shownSkillHint;
        private static bool _skillsTab;

        private static void BuildSkills()
        {
            ActionButton(_view, "INVENTORY", 156, 139, 238, 40, ShowInventory);
            ActionButton(_view, "SKILLS", 414, 139, 180, 40, ShowSkills);
            _skills = Rect(_view, "skills", 0, 0, 1920, 1080);
            Label(_skills, "SKILL", 214, 204, 340, 30, 22, DialogueWidgets.Gold);
            Label(_skills, "VALUE", 570, 204, 130, 30, 22, DialogueWidgets.Gold, TextAnchor.MiddleRight);
            Label(_skills, "XP", 724, 204, 196, 30, 22, DialogueWidgets.Gold, TextAnchor.MiddleRight);
            var source = ModApi.Get<IPlayerPerks>(); _skillsSource = source;
            for (int i = 0; i < SkillRows.Length; i++)
            {
                var skill = source != null && i < source.SkillCount ? source.Skill(i) : new PlayerSkill();
                SkillRows[i] = Rect(_skills, "skill " + i, 214, 236 + i * 24, 710, 24);
                SkillNames[i] = Label(SkillRows[i], (skill.Name ?? "").ToUpperInvariant(), 0, 0, 340, 24, 20, White);
                SkillValues[i] = Label(SkillRows[i], "0 / 100", 356, 0, 130, 24, 20, White, TextAnchor.MiddleRight);
                SkillXp[i] = Label(SkillRows[i], "0 / 100", 510, 0, 196, 24, 20, White, TextAnchor.MiddleRight);
                SkillHints[i] = skill.Hint; ShownSkills[i] = ShownXp[i] = -1;
            }
            Label(_skills, "SKILL PERKS", 214, 534, 710, 30, 22, DialogueWidgets.Gold);
            Label(_skills, "INSANITY PERKS", 214, 744, 710, 26, 20, new Color(.85f, .16f, .12f, 1));
            for (int i = 0; i < PerkSquares.Length; i++)
            {
                PerkSquares[i] = Rect(_skills, i == 0 ? "Cannibal perk" : "perk " + i, 214 + (i % 12) * 58, i < 36 ? 568 + (i / 12) * 58 : 776, 54, 54);
                var border = PerkSquares[i].gameObject.AddAccent<UnityEngine.UI.Image>(); border.color = DialogueWidgets.Gold;
                PerkBorders[i] = border;
                var back = Rect(PerkSquares[i], "background", 2, 2, 50, 50).gameObject.AddComponent<UnityEngine.UI.Image>(); back.color = new Color(.06f, .06f, .05f);
                PerkIcons[i] = Rect(PerkSquares[i], "icon", 4, 4, 46, 46).gameObject.AddComponent<UnityEngine.UI.RawImage>(); PerkIcons[i].raycastTarget = false;
                if (i < 36) Label(PerkSquares[i], ((i % 4 + 1) * 25).ToString(CultureInfo.InvariantCulture), 25, 36, 25, 15, 12, new Color(.65f, .65f, .65f), TextAnchor.MiddleRight);
                var perk = source != null ? source.Perk(i) : new PlayerPerk();
                PerkHints[i] = (perk.Name ?? "").ToUpperInvariant() + "\n\n" + perk.Hint;
                PerkDescriptions[i] = perk.Hint;
            }
            _noPerks = Label(_skills, "PERKS MODULE IS DISABLED", 214, 590, 700, 32, 20, new Color(.55f, .55f, .5f));
            var hintPanel = Panel(_skills, "description", 980, 204, 740, 626, "");
            _skillHint = Label(hintPanel, "", 32, 28, 676, 568, 18, White, TextAnchor.UpperLeft);
            _shownSkillHint = null; SelectTab(false);
        }
        private static void ShowInventory() { SelectTab(false); }
        private static void ShowSkills() { SelectTab(true); }
        private static void SelectTab(bool skills)
        {
            HideDrop(); _pressed = null; _skillsTab = skills;
            if (_inventory != null) _inventory.gameObject.SetActive(!skills);
            if (_skills != null) _skills.gameObject.SetActive(skills);
            if (_hint != null) _hint.gameObject.SetActive(!skills);
        }
        private static void RefreshSkills()
        {
            if (_skills == null || !_skillsTab) return;
            var source = ModApi.Get<IPlayerPerks>();
            if (source != _skillsSource)
            {
                _skillsSource = source;
                for (int i = 0; i < SkillHints.Length; i++)
                { var skill = source != null ? source.Skill(i) : new PlayerSkill(); SkillHints[i] = skill.Hint; SkillNames[i].Set((skill.Name ?? "").ToUpperInvariant()); ShownSkills[i] = ShownXp[i] = -1; }
                for (int i = 0; i < PerkHints.Length; i++)
                { var perk = source != null ? source.Perk(i) : new PlayerPerk(); PerkHints[i] = (perk.Name ?? "").ToUpperInvariant() + "\n\n" + perk.Hint; }
            }
            string hint = source == null ? "PERKS module is disabled." : "Hover over a skill or perk to see its effect.";
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            for (int i = 0; i < SkillRows.Length; i++)
            {
                var skill = source != null ? source.Skill(i) : new PlayerSkill();
                int value = skill.Value; SkillHints[i] = skill.Hint;
                if (ShownSkills[i] != value) { ShownSkills[i] = value; SkillValues[i].Set(value.ToString(CultureInfo.InvariantCulture) + " / 100"); }
                int xp = value == 100 ? 100 : Mathf.FloorToInt(skill.Xp);
                if (ShownXp[i] != xp) { ShownXp[i] = xp; SkillXp[i].Set(value == 100 ? "MAX" : xp.ToString(CultureInfo.InvariantCulture) + " / 100"); }
                if (Application.isFocused && RectTransformUtility.RectangleContainsScreenPoint(SkillRows[i], Input.mousePosition, camera)) hint = SkillHints[i] ?? hint;
            }
            int shown = 0;
            for (int i = 0; i < PerkSquares.Length; i++)
            {
                var perk = source != null ? source.Perk(i) : new PlayerPerk();
                bool visible = source != null && i < source.PerkCount;
                if (PerkSquares[i].gameObject.activeSelf != visible) PerkSquares[i].gameObject.SetActive(visible);
                if (!visible) continue;
                string description = (perk.Unlocked ? "UNLOCKED\n\n" : "LOCKED\n\n") + perk.Hint;
                if (PerkDescriptions[i] != description) { PerkDescriptions[i] = description; PerkHints[i] = (perk.Name ?? "").ToUpperInvariant() + "\n\n" + description; }
                var borderColor = !perk.Unlocked ? new Color(.38f, .38f, .38f, 1) : perk.Insanity ? new Color(.85f, .16f, .12f, 1) : DialogueWidgets.Gold;
                if (PerkBorders[i].color != borderColor) PerkBorders[i].color = borderColor;
                var position = new Vector2(214 + (i % 12) * 58, -(i < 36 ? 568 + (i / 12) * 58 : 776)); shown++;
                if (PerkSquares[i].anchoredPosition != position) PerkSquares[i].anchoredPosition = position;
                var icon = perk.Unlocked ? perk.Icon : GrayPerkIcon(i, perk.Icon);
                if (PerkIcons[i].texture != icon) PerkIcons[i].texture = icon;
                var iconColor = perk.Unlocked ? Color.white : new Color(.65f, .65f, .65f, .8f);
                if (PerkIcons[i].color != iconColor) PerkIcons[i].color = iconColor;
                PerkIcons[i].enabled = perk.Icon != null;
                if (Application.isFocused && RectTransformUtility.RectangleContainsScreenPoint(PerkSquares[i], Input.mousePosition, camera)) hint = PerkHints[i];
            }
            if (_noPerks.gameObject.activeSelf != (shown == 0)) _noPerks.gameObject.SetActive(shown == 0);
            if (_shownSkillHint != hint)
            {
                _shownSkillHint = hint;
                float size = 18;
                var lines = GlyphFont.Wrap(hint, 676, size);
                if (lines.Count * 23 > 568) { size = 16; lines = GlyphFont.Wrap(hint, 676, size); }
                float lineHeight = size + 5;
                if (_skillHint.Size != size) { _skillHint.Size = size; _skillHint.SetVerticesDirty(); }
                _skillHint.Set(lines.Count > 0 ? lines[0] : "");
                for (int i = 1; i < lines.Count; i++)
                {
                    if (HintLines.Count < i) HintLines.Add(Label(_skillHint.transform, "", 0, i * lineHeight, 676, lineHeight, size, White, TextAnchor.UpperLeft));
                    if (HintLines[i - 1].Size != size) { HintLines[i - 1].Size = size; HintLines[i - 1].SetVerticesDirty(); }
                    ((RectTransform)HintLines[i - 1].transform).anchoredPosition = new Vector2(0, -i * lineHeight);
                    HintLines[i - 1].Set(lines[i]); HintLines[i - 1].gameObject.SetActive(true);
                }
                for (int i = System.Math.Max(0, lines.Count - 1); i < HintLines.Count; i++) HintLines[i].gameObject.SetActive(false);
            }
        }
        private static Texture2D GrayPerkIcon(int index, Texture2D source)
        {
            if (PerkGraySources[index] == source) return PerkGrayIcons[index];
            if (PerkGrayIcons[index] != null) UnityEngine.Object.Destroy(PerkGrayIcons[index]);
            PerkGraySources[index] = source; PerkGrayIcons[index] = null;
            if (source == null) return null;
            var previous = RenderTexture.active;
            var buffer = RenderTexture.GetTemporary(46, 46, 0, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                Graphics.Blit(source, buffer); RenderTexture.active = buffer;
                texture = new Texture2D(46, 46, TextureFormat.RGBA32, false) { name = "Locked perk", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                texture.ReadPixels(new Rect(0, 0, 46, 46), 0, 0, false);
                var pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) { var p = pixels[i]; byte gray = (byte)((p.r * 299 + p.g * 587 + p.b * 114) / 1000); pixels[i] = new Color32(gray, gray, gray, p.a); }
                texture.SetPixels32(pixels); texture.Apply(false, true); PerkGrayIcons[index] = texture;
            }
            catch (System.Exception e) { if (texture != null) UnityEngine.Object.Destroy(texture); Core.Log.Warn(null, "[HUD] Locked perk icon: " + e.Message); }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); }
            return PerkGrayIcons[index];
        }
        private static void ClearSkills()
        {
            for (int i = 0; i < PerkGrayIcons.Length; i++) { if (PerkGrayIcons[i] != null) UnityEngine.Object.Destroy(PerkGrayIcons[i]); PerkGrayIcons[i] = PerkGraySources[i] = null; }
            HintLines.Clear(); _skills = null; _skillHint = _noPerks = null; _skillsSource = null; _shownSkillHint = null; _skillsTab = false;
        }
    }
}
