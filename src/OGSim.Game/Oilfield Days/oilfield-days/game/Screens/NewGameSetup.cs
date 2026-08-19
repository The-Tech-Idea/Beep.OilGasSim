#nullable enable

using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// New-game setup (plan 08 §6.2): the knobs <c>EngineSettings</c> and
/// <c>WorldParameters</c> actually take.
///
/// <para>Every field here is one the engine asks for and refuses to default —
/// "EngineSettings has no defaults by design" (plan 00 §2). The seed is on the
/// screen because plan 11 §5 makes a challenge a competition on a shared one:
/// two players who type the same number get the same basin.</para>
/// </summary>
public sealed partial class NewGameSetup : Control
{
    private static readonly string[] Profiles = { "arcade", "standard", "simulation" };
    /// <summary>
    /// How big a basin the generator is asked for.
    /// </summary>
    /// <remarks>
    /// Nothing under 24 km: the generator draws structures from the area it is
    /// given, and a 16 km basin came back with none at all — a world with
    /// nothing to explore is not a smaller game, it is no game.
    /// </remarks>
    private static readonly int[] BasinSizes = { 24, 32, 40 };

    private LineEdit _seed = null!;
    private Label _status = null!;
    private int _profile;
    private int _size = 1;
    private OptionButton _profilePicker = null!;
    private OptionButton _sizePicker = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ground = new ColorRect { Color = new Color(0.09f, 0.12f, 0.10f) };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        KitPanel panel = ScreenChrome.Panel("NEW BASIN", new Vector2(620, 520), LayoutPreset.Center, new Vector2(-310, -260));
        AddChild(panel);

        var column = new VBoxContainer
        {
            Position = new Vector2(24, 52),
            CustomMinimumSize = new Vector2(572, 0),
        };

        column.AddThemeConstantOverride("separation", 14);
        panel.AddChild(column);

        column.AddChild(ScreenChrome.Text("World seed", 16, ScreenChrome.Faded));
        _seed = new LineEdit
        {
            Text = "3",
            CustomMinimumSize = new Vector2(572, 42),
        };

        _seed.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_seed);

        column.AddChild(ScreenChrome.Text("Reality profile — which models the engine composes", 16, ScreenChrome.Faded));
        _profilePicker = Picker(column, Profiles, 0);

        column.AddChild(ScreenChrome.Text("Basin size — kilometres across", 16, ScreenChrome.Faded));
        _sizePicker = Picker(column, ["24 km — a handful of structures", "32 km — a dozen or more", "40 km — a career"], 0);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        _status = ScreenChrome.Text(
            "The basin is generated from the seed: its structures, their charge, and what you are told about them.",
            15,
            ScreenChrome.Faded);

        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(572, 0);
        column.AddChild(_status);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        Button start = ScreenChrome.Action("Drill in", ScreenChrome.Good, new Vector2(280, 52));
        start.Pressed += Start;
        row.AddChild(start);

        Button back = ScreenChrome.Action("Back", ScreenChrome.Bad, new Vector2(280, 52));
        back.Pressed += () => SceneRouter.Instance.Go(SceneRouter.MainMenu);
        row.AddChild(back);

        column.AddChild(row);
    }

    private void Start()
    {
        if (!ulong.TryParse(_seed.Text.Trim(), out ulong seed))
        {
            _status.Text = "A seed is a whole number. Anything else and two players cannot compare runs.";
            _status.AddThemeColorOverride("font_color", ScreenChrome.Bad);
            return;
        }

        _profile = _profilePicker.Selected;
        _size = _sizePicker.Selected;

        if (!EngineHost.Instance.NewGame(seed, Profiles[_profile], BasinSizes[_size]))
        {
            _status.Text = "The engine refused to start:\n" + string.Join("\n", EngineHost.Instance.StartupProblems);
            _status.AddThemeColorOverride("font_color", ScreenChrome.Bad);
            return;
        }

        SceneRouter.Instance.Go(SceneRouter.Gameplay);
    }

    private static OptionButton Picker(Container parent, string[] items, int selected)
    {
        var picker = new OptionButton { CustomMinimumSize = new Vector2(572, 42) };

        foreach (string item in items)
            picker.AddItem(item);

        picker.Selected = selected;
        picker.AddThemeFontSizeOverride("font_size", 18);
        parent.AddChild(picker);

        return picker;
    }
}
