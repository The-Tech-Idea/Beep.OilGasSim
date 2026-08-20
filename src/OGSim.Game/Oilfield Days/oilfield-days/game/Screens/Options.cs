#nullable enable

using System;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// Options — the mockup's fifth menu entry, holding the settings that are the
/// client's to hold.
///
/// <para>GAME-SDD-002 §2 puts display, audio and input on the client side of the
/// line and simulation settings on the engine's. So everything here is a window,
/// a volume or a key; nothing here changes a rate, a price or a probability.
/// Difficulty is not on this screen because difficulty is the reality profile,
/// which is chosen when a world is created and fixed for the run — moving it
/// mid-game would change the models under a running simulation.</para>
/// </summary>
public sealed partial class Options : Control
{
    private static readonly (string Label, int Width, int Height)[] Windows =
    {
        ("1280 x 720", 1280, 720),
        ("1600 x 900", 1600, 900),
        ("1920 x 1080", 1920, 1080),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = KitTheme.Void };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        Container inset = SlateChrome.Frame(new Vector2(760, 620), "OPTIONS", UiSurface.Role.Warning);
        Control panel = SlateChrome.PanelOf(inset);
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.Position = new Vector2(-380, -310);
        AddChild(panel);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 12);
        inset.AddChild(page);

        VBoxContainer display = SlateChrome.Group("DISPLAY", page, 700, UiSurface.Role.Info);

        display.AddChild(SlateChrome.Caption("Window size"));

        string[] sizes = new string[Windows.Length];

        for (int i = 0; i < Windows.Length; i++)
            sizes[i] = Windows[i].Label;

        OptionButton window = SlateChrome.Choice(sizes, 1, 640);
        window.ItemSelected += index => Resize((int)index);
        display.AddChild(window);

        CheckBox full = SlateChrome.Tick("Full screen", DisplayServer.WindowGetMode() is
            DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen);

        full.Toggled += on => DisplayServer.WindowSetMode(
            on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

        display.AddChild(full);

        VBoxContainer audio = SlateChrome.Group("AUDIO", page, 700, UiSurface.Role.Info);
        audio.AddChild(Volume("Master"));

        VBoxContainer keys = SlateChrome.Group("CONTROLS", page, 700, UiSurface.Role.Info);
        keys.AddChild(SlateChrome.Row2("Drive", "W A S D"));
        keys.AddChild(SlateChrome.Row2("Advance one month", "Space"));
        keys.AddChild(SlateChrome.Row2("Pause", "P"));
        keys.AddChild(SlateChrome.Row2("Close a board", "Esc"));

        Button back = SlateChrome.Chunk("BACK", UiSurface.Role.Danger, new Vector2(220, 50));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
        page.AddChild(back);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.Go(SceneRouter.MainMenu);
            GetViewport().SetInputAsHandled();
        }
    }

    private static void Resize(int choice)
    {
        (string _, int width, int height) = Windows[Mathf.Clamp(choice, 0, Windows.Length - 1)];

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
    }

    /// <summary>
    /// A bus volume, moved in decibels because that is what a mixer is in.
    /// </summary>
    private static Control Volume(string busName)
    {
        int bus = AudioServer.GetBusIndex(busName);

        var column = new VBoxContainer();
        Label caption = SlateChrome.Caption(busName);
        column.AddChild(caption);

        var slider = new HSlider
        {
            MinValue = -40.0,
            MaxValue = 0.0,
            Step = 1.0,
            Value = bus < 0 ? 0.0 : AudioServer.GetBusVolumeDb(bus),
            CustomMinimumSize = new Vector2(640, 24),
        };

        slider.ValueChanged += db =>
        {
            caption.Text = $"{busName}   {db.ToString("F0", CultureInfo.InvariantCulture)} dB";

            if (bus >= 0)
                AudioServer.SetBusVolumeDb(bus, (float)db);
        };

        column.AddChild(slider);

        if (bus < 0)
            column.AddChild(SlateChrome.Caption("no audio bus by that name is configured yet"));

        return column;
    }
}
