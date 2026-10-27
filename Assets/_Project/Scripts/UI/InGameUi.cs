using System.Collections.Generic;
using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Shared IMGUI presentation. Does not own inventory or issue gameplay requests.</summary>
    internal static class InGameUi
    {
        internal const int LabelFontSize = 16;
        internal const int SmallFontSize = 14;
        private static InGameUiArt _art;
        private static bool _loaded;
        private static readonly Dictionary<string, GUIStyle> Masks = new Dictionary<string, GUIStyle>();
        private static GUIStyle _label, _small, _count, _button;
        private static string _tooltip;
        private static readonly string[] EquipKeys = { "Head", "Body", "Legs", "Feet" };
        private readonly struct PromptRequest
        {
            public readonly string Text, Glyph;
            public readonly float Progress;
            public PromptRequest(string text, string glyph, float progress)
            { Text = text; Glyph = glyph; Progress = progress; }
        }
        private static readonly List<PromptRequest> Prompts = new List<PromptRequest>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _art = null;
            _loaded = false;
            Masks.Clear();
            _label = _small = _count = _button = null;
            _tooltip = null;
            Prompts.Clear();
        }

        public static InGameUiArt Art
        {
            get
            {
                if (!_loaded)
                {
                    _art = Resources.Load<InGameUiArt>("InGameUiArt");
                    _loaded = true;
                    if (_art == null) Debug.LogError("InGameUiArt resource is missing.");
                }
                return _art;
            }
        }

        private static void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = LabelFontSize, richText = true, wordWrap = true, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _label.normal.textColor = UiPalette.TextSteam;
            _small = new GUIStyle(_label) { fontSize = SmallFontSize };
            _count = new GUIStyle(_label) { fontSize = SmallFontSize, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
        }

        public static void Label(Rect rect, string text, bool small = false)
        {
            EnsureStyles();
            GUI.Label(rect, text, small ? _small : _label);
        }

        public static void Image(Rect rect, string key, Color? tint = null)
        {
            Sprite sprite = Art != null ? Art.Find(key) : null;
            if (sprite == null) return;
            Color previous = GUI.color;
            Color color = tint ?? Color.white;
            color.a *= previous.a;
            GUI.color = color;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        public static void Mask(Rect rect, string key, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            if (!Masks.TryGetValue(key, out GUIStyle style))
            {
                Sprite sprite = Art != null ? Art.Find(key) : null;
                if (sprite == null) return;
                Vector4 b = sprite.border;
                style = new GUIStyle { border = new RectOffset((int)b.x, (int)b.z, (int)b.w, (int)b.y) };
                style.normal.background = sprite.texture;
                Masks.Add(key, style);
            }
            Color previous = GUI.color;
            Color background = GUI.backgroundColor;
            color.a *= previous.a;
            GUI.color = color;
            GUI.backgroundColor = Color.white;
            style.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previous;
            GUI.backgroundColor = background;
        }

        public static void Panel(Rect rect, string title)
        {
            Mask(rect, "U_PanelBody", UiPalette.PanelBackdrop);
            Mask(new Rect(rect.x, rect.y, rect.width, 30), "U_PanelHeader", UiPalette.PanelLine);
            Label(new Rect(rect.x + 16, rect.y + 2, rect.width - 32, 26), title);
            Mask(new Rect(rect.x + 12, rect.y + 30, rect.width - 24, 2), "U_Divider", UiPalette.FocusBrass);
        }

        public static void Slot(Rect rect, HotbarSlotView slot, string tooltip, int key = 0,
            bool selected = false, bool rejected = false, int tier = 1, int equipment = -1, bool hover = true)
        {
            Mask(rect, "U_SlotBase", UiPalette.PanelBackdrop);
            Mask(rect, "U_SlotOutline", UiPalette.IronGray);
            float iconSize = rect.width >= 72 ? 56 : 48;
            var icon = new Rect(rect.center.x - iconSize / 2, rect.center.y - iconSize / 2, iconSize, iconSize);
            if (!slot.IsEmpty) Image(icon, InGameUiArt.ItemKey(slot));
            else if (equipment >= 0 && equipment < EquipKeys.Length)
                Image(new Rect(rect.center.x - 16, rect.center.y - 16, 32, 32), "G_Equip_" + EquipKeys[equipment], UiPalette.TextMuted);
            if (selected) Mask(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), "U_FocusCorners", UiPalette.FocusBrass);
            if (rejected)
            {
                Mask(rect, "U_SlotOutline", UiPalette.CriticalText);
                float rejectY = slot.ItemType == HotbarItemType.Harpoon ? rect.yMax - 22 : rect.y + 3;
                Image(new Rect(rect.xMax - 22, rejectY, 18, 18), "G_System_Cancel", UiPalette.CriticalText);
            }
            if (key > 0)
            {
                Mask(new Rect(rect.x + 3, rect.y + 3, 18, 18), "U_Keycap", UiPalette.PanelSoot);
                Label(new Rect(rect.x + 8, rect.y + 3, 15, 18), key.ToString(), true);
            }
            string quantity = InGameUiArt.QuantityLabel(slot);
            if (quantity.Length > 0)
            {
                EnsureStyles();
                var badge = new Rect(rect.xMax - 38, rect.yMax - 21, 34, 18);
                Mask(badge, "U_ControlFace", UiPalette.PanelSoot);
                GUI.Label(new Rect(badge.x, badge.y, badge.width - 4, badge.height), quantity, _count);
            }
            if (slot.ItemType == HotbarItemType.Harpoon)
            {
                Mask(new Rect(rect.xMax - 24, rect.y + 2, 22, 22), "U_BadgeDisc", UiPalette.PanelSoot);
                Label(new Rect(rect.xMax - 19, rect.y + 3, 20, 20), Mathf.Max(1, tier).ToString(), true);
            }
            if (equipment >= 0 && !slot.IsEmpty)
                Image(new Rect(rect.xMax - 22, rect.yMax - 22, 18, 18), "G_System_Confirm", UiPalette.SafeText);
            if (hover && rect.Contains(Event.current.mousePosition)) _tooltip = tooltip;
        }

        public static void BeginTooltip() => _tooltip = null;
        public static void DrawTooltip()
        {
            if (string.IsNullOrEmpty(_tooltip)) return;
            EnsureStyles();
            float width = 280;
            float height = Mathf.Max(48, _label.CalcHeight(new GUIContent(_tooltip), width - 24) + 20);
            Vector2 mouse = Event.current.mousePosition;
            var rect = new Rect(Mathf.Clamp(mouse.x + 18, 8, Screen.width - width - 8),
                Mathf.Clamp(mouse.y + 20, 8, Screen.height - height - 8), width, height);
            Mask(rect, "U_PanelBody", UiPalette.PanelSoot);
            Label(new Rect(rect.x + 12, rect.y + 8, width - 24, height - 16), _tooltip);
        }

        public static float Prompt(Rect rect, string text, string glyph = "G_Action_Interact", float progress = -1)
        {
            EnsureStyles();
            float iconSize = PromptIconSize(glyph);
            float textX = iconSize + 14;
            rect.height = PromptHeight(rect.width, rect.height, text, progress, glyph);
            Mask(rect, "U_PanelBody", UiPalette.PanelBackdrop);
            Image(new Rect(rect.x + 6, rect.y + 4, iconSize, iconSize), glyph,
                glyph.StartsWith("I_") ? Color.white : UiPalette.FocusBrass);
            Label(new Rect(rect.x + textX, rect.y + 4, rect.width - textX - 8, rect.height - 8 - (progress >= 0 ? 14 : 0)), text);
            if (progress >= 0) Gauge(new Rect(rect.x + textX, rect.yMax - 12, 160, 8), progress, UiPalette.AlertFill);
            return rect.height;
        }

        private static float PromptIconSize(string glyph) => glyph.StartsWith("I_Structure_") ? 48 : 24;

        private static float PromptHeight(float width, float minimum, string text, float progress, string glyph)
        {
            EnsureStyles();
            float iconSize = PromptIconSize(glyph);
            return Mathf.Max(minimum, Mathf.Max(iconSize + 8, _label.CalcHeight(new GUIContent(text), width - iconSize - 24) + 8))
                + (progress >= 0 ? 14 : 0);
        }

        public static void BeginPrompts() => Prompts.Clear();
        public static void QueuePrompt(string text, string glyph = "G_Action_Interact", float progress = -1) =>
            Prompts.Add(new PromptRequest(text, glyph, progress));

        public static void DrawPrompts()
        {
            const float width = 400, gap = 6;
            float height = 0;
            foreach (PromptRequest prompt in Prompts) height += PromptHeight(width, 56, prompt.Text, prompt.Progress, prompt.Glyph) + gap;
            float y = Mathf.Max(24, Mathf.Min(Screen.height * 0.54f, Screen.height - 144 - height));
            foreach (PromptRequest prompt in Prompts)
                y += Prompt(new Rect((Screen.width - width) * 0.5f, y, width, 56), prompt.Text, prompt.Glyph, prompt.Progress) + gap;
        }

        public static void Gauge(Rect rect, float ratio, Color fill)
        {
            Mask(rect, "U_GaugeTrack", UiPalette.PanelLine);
            if (float.IsNaN(ratio)) return;
            float width = rect.width * Mathf.Clamp01(ratio);
            if (width <= 0) return;
            // Clip the full-size fill, so a nearly empty bar doesn't stretch its rounded ends.
            GUI.BeginGroup(new Rect(rect.x, rect.y, width, rect.height));
            Mask(new Rect(0, 0, rect.width, rect.height), "U_GaugeFill", fill);
            GUI.EndGroup();
        }

        public static void StatusRow(string glyph, string text, float ratio = -1, Color? tint = null, float alpha = 1)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, previous.a * alpha);
            Rect rect = GUILayoutUtility.GetRect(1, ratio >= 0 ? 48 : 28, GUILayout.ExpandWidth(true));
            Mask(rect, "U_PanelBody", UiPalette.PanelBackdrop);
            Image(new Rect(rect.x + 4, rect.y + 2, 24, 24), glyph, tint ?? UiPalette.TextSteam);
            Label(new Rect(rect.x + 36, rect.y + 2, rect.width - 42, 24), text, true);
            if (ratio >= 0) Gauge(new Rect(rect.x + 36, rect.yMax - 19, Mathf.Min(240, rect.width - 44), 16), ratio, tint ?? UiPalette.FocusBrass);
            GUI.color = previous;
        }

        public static bool Button(Rect rect, string text)
        {
            EnsureStyles();
            bool enabled = GUI.enabled;
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Mask(rect, "U_ControlFace", hover ? UiPalette.FocusBrass : UiPalette.PanelLine);
            if (hover) Mask(rect, "U_FocusCorners", UiPalette.FocusBrass);
            return GUI.Button(rect, text, _button);
        }
    }
}
