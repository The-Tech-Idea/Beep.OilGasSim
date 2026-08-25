using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Drop-in skinned label for template scenes.
    ///
    /// It stays a Godot Label so existing scene paths, bindings, alignment, wrapping, and layout
    /// still work, but it takes its font scale and ink from the active Beep game skin instead of
    /// inheriting plain editor-style label defaults.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLabel : Label
    {
        [Export]
        public bool AutoRole
        {
            get => _autoRole;
            set { if (_autoRole == value) return; _autoRole = value; ApplyKitText(); }
        }
        private bool _autoRole = true;

        [Export]
        public UiSurface.TextRole Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; ApplyKitText(); }
        }
        private UiSurface.TextRole _role = UiSurface.TextRole.Body;

        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; ApplyKitText(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        private string _genre = "";
        private bool _applying;
        private bool _exiting;

        public override void _Ready()
        {
            base._Ready();
            _exiting = false;
            _genre = KitChrome.GenreOf(this);
            ApplyKitText();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                if (_applying || _exiting || !IsInsideTree()) return;
                _genre = KitChrome.GenreOf(this);
                ApplyKitText();
            }
        }

        public override void _ExitTree()
        {
            _exiting = true;
            base._ExitTree();
        }

        private void ApplyKitText()
        {
            if (_applying || _exiting || !IsInsideTree()) return;

            UiSurface.TextRole role = AutoRole ? InferRole() : Role;
            int fs = Mathf.Max(1, UiSurface.FontSize(this, role));
            Color ink = Accent == UiSurface.Role.Neutral ? UiSurface.Text(this) : UiSurface.Semantic(this, Accent);
            if (ink.A < 0.02f) ink = new Color(0.96f, 0.94f, 0.88f);

            _applying = true;
            try
            {
                ApplyOverrideChanges(fs, ink, role);
            }
            finally
            {
                _applying = false;
            }
        }

        private void ApplyOverrideChanges(int fs, Color ink, UiSurface.TextRole role)
        {
            Font? font = KitChrome.Font(this, _genre);
            if (font != null) KitChrome.SetFontOverrideIfChanged(this, "font", font);
            KitChrome.SetFontSizeOverrideIfChanged(this, "font_size", fs);
            KitChrome.SetColorOverrideIfChanged(this, "font_color", ink);
            KitChrome.SetColorOverrideIfChanged(this, "font_shadow_color", new Color(0, 0, 0, 0.68f));
            KitChrome.SetConstantOverrideIfChanged(this, "shadow_offset_x", Mathf.Max(1, fs / 18));
            KitChrome.SetConstantOverrideIfChanged(this, "shadow_offset_y", Mathf.Max(1, fs / 18));
            KitChrome.SetColorOverrideIfChanged(this, "font_outline_color", new Color(0, 0, 0, 0.72f));
            KitChrome.SetConstantOverrideIfChanged(this, "outline_size", role == UiSurface.TextRole.Title ? 3 : 2);
        }

        private UiSurface.TextRole InferRole()
        {
            string name = Name.ToString().ToLowerInvariant();
            string variation = ThemeTypeVariation.ToString().ToLowerInvariant();
            string key = name + " " + variation;

            if (key.Contains("title") || key.Contains("banner") || key.Contains("pause"))
                return UiSurface.TextRole.Title;
            if (key.Contains("heading") || key.Contains("subtitle") || key.Contains("name"))
                return UiSurface.TextRole.Subtitle;
            if (key.Contains("value") || key.Contains("count") || key.Contains("gold") || key.Contains("score"))
                return UiSurface.TextRole.Value;
            if (key.Contains("caption") || key.Contains("hint") || key.Contains("label") || key.Contains("unit"))
                return UiSurface.TextRole.Caption;
            return Role;
        }
    }
}
