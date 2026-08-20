#nullable enable

using System;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Ui;

/// <summary>
/// The status bar across the top of every gameplay mockup: the mark and the
/// date on the left, the speed controls beside them, the company's numbers in
/// capsules across the middle, and the menu on the right.
///
/// <para>Drawn on the supplied atlas's plates through <see cref="SlateChrome"/>,
/// so it belongs to the same set as the setup screens rather than to the yard's
/// painted wood.</para>
///
/// <para><b>Every capsule is a published number.</b> The mockups show Cash,
/// Reputation, Oil Rate and Gas Rate. Three of those exist:</para>
/// <list type="bullet">
/// <item>cash and the oil price come straight off the read model;</item>
/// <item>a daily rate is the month's volume over thirty, which is exact rather
/// than approximate — the engine's calendar is 30/360, so a month <b>is</b>
/// thirty days;</item>
/// <item><b>reputation is not shown at all.</b> Gap G-04: no published engine
/// metric owns it, and a bar reporting 68 would be reporting nothing. Debt takes
/// its place, which the company does own.</item>
/// </list>
///
/// <para>Gas has no capsule either. The read model publishes one produced volume
/// and the chain's throughputs in mass; splitting a gas rate out of that would be
/// the host doing reservoir engineering, which is the one thing plan 11 §7 says
/// it must never do. The gas plant's own throughput is on the chain, where it is
/// measured.</para>
/// </summary>
public sealed partial class StatusBar : PanelContainer
{
    private Label _date = null!;
    private Label _speed = null!;
    private Label _cash = null!;
    private Label _debt = null!;
    private Label _price = null!;
    private Label _rate = null!;
    private Label _wells = null!;
    private Label _weather = null!;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_left", 14);
        inset.AddThemeConstantOverride("margin_right", 14);
        inset.AddThemeConstantOverride("margin_top", 8);
        inset.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(inset);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        inset.AddChild(row);

        row.AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>(SlateChrome.LogoPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(42, 42),
            MouseFilter = MouseFilterEnum.Ignore,
        });

        _date = Capsule(row, "communications-tower", UiSurface.Role.Neutral, 132);

        BuildSpeed(row);

        _cash = Capsule(row, "crude-oil-storage-tank", UiSurface.Role.Success, 132);
        _debt = Capsule(row, "security-checkpoint", UiSurface.Role.Danger, 118);
        _price = Capsule(row, "metering-station", UiSurface.Role.Warning, 118);
        _rate = Capsule(row, "pumpjack", UiSurface.Role.Warning, 142);
        _wells = Capsule(row, "drilling-rig-derrick", UiSurface.Role.Info, 126);
        _weather = Capsule(row, "cooling-tower", UiSurface.Role.Info, 126);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Button dispatch = SlateChrome.Chunk("JOBS", UiSurface.Role.Neutral, new Vector2(92, 42), fontSize: 13);
        dispatch.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard);
        row.AddChild(dispatch);

        Button menu = SlateChrome.Chunk("MENU", UiSurface.Role.Neutral, new Vector2(96, 42), fontSize: 13);
        menu.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.PauseMenu);
        row.AddChild(menu);
    }

    private void BuildSpeed(Container parent)
    {
        var group = new HBoxContainer();
        group.AddThemeConstantOverride("separation", 4);
        parent.AddChild(group);

        Step(group, "II", SimulationController.Speed.Paused);
        Step(group, "▶", SimulationController.Speed.Slow);
        Step(group, "▶▶", SimulationController.Speed.Normal);
        Step(group, "▶▶▶", SimulationController.Speed.Fast);

        _speed = SlateChrome.Line("x1", 15, KitTheme.Amber);
        _speed.CustomMinimumSize = new Vector2(38, 0);
        _speed.VerticalAlignment = VerticalAlignment.Center;
        group.AddChild(_speed);
    }

    private static void Step(Container parent, string glyph, SimulationController.Speed speed)
    {
        Button button = SlateChrome.Chunk(glyph, UiSurface.Role.Neutral, new Vector2(38, 42), fontSize: 12);
        button.Pressed += () => SimulationController.Instance.SetSpeed(speed);
        parent.AddChild(button);
    }

    /// <summary>One of the mockups' stat capsules: icon, then the reading.</summary>
    private static Label Capsule(Container parent, string icon, UiSurface.Role role, float width)
    {
        var plate = new PanelContainer { CustomMinimumSize = new Vector2(width, 42) };
                // Tighter than the default field inset. A capsule is a chip, not a
        // panel: at the standard padding seven of them plus the speed controls
        // ran past the right edge and took the menu button with them.
        plate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate(10, 6));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(SlateChrome.Icon(icon, 22.0f));

        Label reading = SlateChrome.Line("-", 15, Tint(role));
        reading.VerticalAlignment = VerticalAlignment.Center;
        reading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(reading);

        plate.AddChild(row);
        parent.AddChild(plate);

        return reading;
    }

    private static Color Tint(UiSurface.Role role) => role switch
    {
        UiSurface.Role.Success => KitTheme.Green.Lightened(0.35f),
        UiSurface.Role.Danger => KitTheme.Red.Lightened(0.35f),
        UiSurface.Role.Info => KitTheme.Sky,
        UiSurface.Role.Warning => KitTheme.Amber,
        _ => KitTheme.Ink,
    };

    public void Bind(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        int year = snapshot.Date.Year - EngineHost.Instance.Draft.StartYear + 1;

        _date.Text = $"{Season(snapshot.Date.Month)} Y{year.ToString(CultureInfo.InvariantCulture)}";

        _cash.Text = $"${snapshot.Cash.Cents / 100.0 / 1e6:N1}M";

        _debt.Text = snapshot.Debt.Cents == 0
            ? "no debt"
            : $"-${snapshot.Debt.Cents / 100.0 / 1e6:N1}M";

        _price.Text = $"${snapshot.OilPrice.Cents / 100.0:N0}/t";

        // Thirty, not 30.44: the engine's calendar makes every month exactly
        // thirty days, so this is the rate and not an approximation of it.
        _rate.Text = $"{snapshot.ProducedThisTick.CubicMetres / 30.0:N0} m3/d";

        _wells.Text = snapshot.ActivitiesRunning > 0
            ? $"{snapshot.Wells}w  {snapshot.ActivitiesRunning} busy"
            : $"{snapshot.Wells} wells";

        _weather.Text = $"{snapshot.Weather.Ambient.ToCelsius():N0}C  {Rough(snapshot.Weather.Severity)}";
    }

    public void BindSpeed(SimulationController.Speed speed) => _speed.Text = speed switch
    {
        SimulationController.Speed.Paused => "||",
        SimulationController.Speed.Slow => "x1",
        SimulationController.Speed.Fast => "x4",
        _ => "x2",
    };

    private static string Season(int month) => month switch
    {
        <= 3 => "Spring",
        <= 6 => "Summer",
        <= 9 => "Autumn",
        _ => "Winter",
    };

    private static string Rough(double severity) => severity switch
    {
        < 0.25 => "fair",
        < 0.5 => "fresh",
        < 0.75 => "rough",
        _ => "foul",
    };
}
