#nullable enable

using System;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// Options - the mockup's fifth menu entry, holding the settings that are the
/// client's to hold.
///
/// <para>GAME-SDD-002 §2 puts display, audio and input on the client side of the
/// line and simulation settings on the engine's. So everything here is a window,
/// a volume or a key; nothing here changes a rate, a price or a probability.
/// Difficulty is not on this screen because difficulty is the reality profile,
/// which is chosen when a world is created and fixed for the run - moving it
/// mid-game would change the models under a running simulation.</para>
/// </summary>
[Tool]
public sealed partial class Options : Control
{
    private static readonly (string Label, int Width, int Height)[] Windows =
    {
        ("1280 x 720", 1280, 720),
        ("1600 x 900", 1600, 900),
        ("1920 x 1080", 1920, 1080),
    };

    private Label _volumeCaption = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        StyleGround();
        BindPanel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Godot.Engine.IsEditorHint())
            return;

        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.Go(SceneRouter.MainMenu);
            GetViewport().SetInputAsHandled();
        }
    }

    private void StyleGround()
    {
        var ground = RequireNamed<ColorRect>("Ground");
        ground.Color = KitTheme.Void;
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void BindPanel()
    {
        var panel = RequireNamed<PanelContainer>("OptionsPanel");
        panel.CustomMinimumSize = new Vector2(760, 620);
        CenterPanel(panel, 760, 620);
        panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate());

        var page = RequireNamed<VBoxContainer>(panel, "Page");
        page.AddThemeConstantOverride("separation", 12);

        ConfigureTitle(page);
        ConfigureDisplay(page);
        ConfigureAudio(page);
        ConfigureControls(page);
        ConfigureBack(page);
    }

    private static void ConfigureTitle(Node page)
    {
        Label title = RequireNamed<Label>(page, "Title");
        title.Text = "OPTIONS";
        SlateChrome.PromoteHeader(title, UiSurface.Role.Warning, centered: true);

    }

    private void ConfigureDisplay(Node page)
    {
        var group = RequireGroup(page, "DisplayGroup", "DISPLAY");

        Label windowLabel = RequireNamed<Label>(group, "WindowCaption");
        ApplyCaption(windowLabel, "Window size");

        OptionButton window = RequireNamed<OptionButton>(group, "WindowSize");
        window.Clear();

        foreach ((string label, _, _) in Windows)
            window.AddItem(label);

        window.Selected = 1;
        window.CustomMinimumSize = new Vector2(640, 46);
        window.AddThemeFontSizeOverride("font_size", 17);
        window.AddThemeColorOverride("font_color", KitTheme.Ink);
        window.AddThemeColorOverride("font_hover_color", KitTheme.Amber);
        window.AddThemeStyleboxOverride("normal", SlateChrome.FieldPlate());
        window.AddThemeStyleboxOverride("hover", SlateChrome.FieldPlate());
        window.AddThemeStyleboxOverride("pressed", SlateChrome.FieldPlate());
        window.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);

        if (!Godot.Engine.IsEditorHint())
            window.ItemSelected += index => Resize((int)index);

        CheckBox full = RequireNamed<CheckBox>(group, "Fullscreen");
        full.Text = "Full screen";
        full.ButtonPressed = !Godot.Engine.IsEditorHint() && DisplayServer.WindowGetMode() is
            DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        full.AddThemeFontSizeOverride("font_size", 15);
        full.AddThemeColorOverride("font_color", KitTheme.Muted);

        if (!Godot.Engine.IsEditorHint())
        {
            full.Toggled += on => DisplayServer.WindowSetMode(
                on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        }
    }

    private void ConfigureAudio(Node page)
    {
        var group = RequireGroup(page, "AudioGroup", "AUDIO");

        _volumeCaption = RequireNamed<Label>(group, "MasterCaption");
        ApplyCaption(_volumeCaption, "Master");

        int bus = AudioServer.GetBusIndex("Master");
        var slider = RequireNamed<HSlider>(group, "MasterVolume");
        slider.MinValue = -40.0;
        slider.MaxValue = 0.0;
        slider.Step = 1.0;
        slider.Value = bus < 0 ? 0.0 : AudioServer.GetBusVolumeDb(bus);
        slider.CustomMinimumSize = new Vector2(640, 24);
        UpdateVolumeCaption(slider.Value);

        if (!Godot.Engine.IsEditorHint())
        {
            slider.ValueChanged += db =>
            {
                UpdateVolumeCaption(db);

                if (bus >= 0)
                    AudioServer.SetBusVolumeDb(bus, (float)db);
            };
        }

        Label missing = RequireNamed<Label>(group, "MissingAudioBus");
        ApplyCaption(missing, bus < 0 ? "no audio bus by that name is configured yet" : string.Empty);
        missing.Visible = bus < 0;
    }

    private static void ConfigureControls(Node page)
    {
        var group = RequireGroup(page, "ControlsGroup", "CONTROLS");

        Row(group, "DriveRow", "Drive", "W A S D");
        Row(group, "AdvanceRow", "Advance one month", "Space");
        Row(group, "PauseRow", "Pause", "P");
        Row(group, "CloseRow", "Close a board", "Esc");
    }

    private static VBoxContainer RequireGroup(Node page, string name, string title)
    {
        var panel = RequireNamed<PanelContainer>(page, name);
        panel.CustomMinimumSize = new Vector2(700, 0);
        panel.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        var column = RequireNamed<VBoxContainer>(panel, "Content");
        column.AddThemeConstantOverride("separation", 4);

        Label caption = RequireNamed<Label>(column, "Header");
        caption.Text = title;
        SlateChrome.PromoteHeader(caption, UiSurface.Role.Warning);

        return column;
    }

    private static void Row(Node parent, string rowName, string label, string value)
    {
        var row = RequireNamed<HBoxContainer>(parent, rowName);
        row.CustomMinimumSize = new Vector2(0, 26);
        row.AddThemeConstantOverride("separation", 8);

        Label name = RequireNamed<Label>(row, "Name");
        name.Text = label;
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_color", KitTheme.Muted);

        Label read = RequireNamed<Label>(row, "Value");
        read.Text = value;
        read.HorizontalAlignment = HorizontalAlignment.Right;
        read.VerticalAlignment = VerticalAlignment.Center;
        read.AddThemeFontSizeOverride("font_size", 15);
        read.AddThemeColorOverride("font_color", KitTheme.Amber);
    }

    private void ConfigureBack(Node page)
    {
        Button back = RequireNamed<Button>(page, "BackButton");
        SlateChrome.ApplyChunk(back, "BACK", UiSurface.Role.Danger, new Vector2(220, 50));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        if (!Godot.Engine.IsEditorHint())
            back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
    }

    private static void Resize(int choice)
    {
        (string _, int width, int height) = Windows[Mathf.Clamp(choice, 0, Windows.Length - 1)];

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
    }

    private static void CenterPanel(Control panel, float width, float height)
    {
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -width / 2.0f;
        panel.OffsetTop = -height / 2.0f;
        panel.OffsetRight = width / 2.0f;
        panel.OffsetBottom = height / 2.0f;
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
    }

    private void UpdateVolumeCaption(double db) =>
        _volumeCaption.Text = $"Master   {db.ToString("F0", CultureInfo.InvariantCulture)} dB";

    private static void ApplyCaption(Label label, string text)
    {
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);
    }

    private T? FindNamed<T>(string name) where T : Node => FindNamed<T>(this, name);

    private T RequireNamed<T>(string name) where T : Node =>
        FindNamed<T>(name) ?? throw new InvalidOperationException(
			$"{nameof(Options)} requires a design-time {typeof(T).Name} named '{name}'.");

    private static T RequireNamed<T>(Node root, string name) where T : Node =>
        FindNamed<T>(root, name) ?? throw new InvalidOperationException(
			$"{nameof(Options)} requires a design-time {typeof(T).Name} named '{name}' under {root.GetPath()}.");

    private static T? FindNamed<T>(Node root, string name) where T : Node
    {
        if (root is T self && root.Name == name)
            return self;

        foreach (Node child in root.GetChildren())
        {
            T? found = FindNamed<T>(child, name);

            if (found is not null)
                return found;
        }

        return null;
    }
}
