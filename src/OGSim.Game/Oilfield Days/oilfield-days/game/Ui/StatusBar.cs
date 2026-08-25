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
[Tool]
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

        MarginContainer inset = RequireNamed<MarginContainer>(this, "Inset");
        inset.AddThemeConstantOverride("margin_left", 14);
        inset.AddThemeConstantOverride("margin_right", 14);
        inset.AddThemeConstantOverride("margin_top", 8);
        inset.AddThemeConstantOverride("margin_bottom", 8);

        HBoxContainer row = RequireNamed<HBoxContainer>(inset, "StatusRow");
        row.AddThemeConstantOverride("separation", 6);

        TextureRect logo = RequireNamed<TextureRect>(row, "Logo");
        logo.Texture = GD.Load<Texture2D>(SlateChrome.LogoPath);
        logo.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        logo.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        logo.CustomMinimumSize = new Vector2(42, 42);
        logo.MouseFilter = MouseFilterEnum.Ignore;

        _date = Capsule(row, "DateCapsule", "Date", "communications-tower", UiSurface.Role.Neutral, 132);

        BuildSpeed(row);

        _cash = Capsule(row, "CashCapsule", "Cash", "crude-oil-storage-tank", UiSurface.Role.Success, 132);
        _debt = Capsule(row, "DebtCapsule", "Debt", "security-checkpoint", UiSurface.Role.Danger, 118);
        _price = Capsule(row, "PriceCapsule", "Price", "metering-station", UiSurface.Role.Warning, 118);
        _rate = Capsule(row, "RateCapsule", "Rate", "pumpjack", UiSurface.Role.Warning, 142);
        _wells = Capsule(row, "WellsCapsule", "Wells", "drilling-rig-derrick", UiSurface.Role.Info, 126);
        _weather = Capsule(row, "WeatherCapsule", "Weather", "cooling-tower", UiSurface.Role.Info, 126);

        Control spacer = RequireNamed<Control>(row, "Spacer");
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Button dispatch = Button(row, "DispatchButton", "JOBS", new Vector2(92, 42), 13);

        Button menu = Button(row, "MenuButton", "MENU", new Vector2(96, 42), 13);
        
        if (Godot.Engine.IsEditorHint())
            return;

        dispatch.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard);
        menu.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.PauseMenu);
    }

    private void BuildSpeed(Container parent)
    {
        HBoxContainer group = RequireNamed<HBoxContainer>(parent, "SpeedGroup");
        group.AddThemeConstantOverride("separation", 4);

        Step(group, "PauseButton", "II", SimulationController.Speed.Paused);
        Step(group, "SlowButton", ">", SimulationController.Speed.Slow);
        Step(group, "NormalButton", ">>", SimulationController.Speed.Normal);
        Step(group, "FastButton", ">>>", SimulationController.Speed.Fast);

        _speed = RequireNamed<Label>(group, "Speed");
        _speed.Name = "Speed";

        _speed.CustomMinimumSize = new Vector2(38, 0);
        _speed.VerticalAlignment = VerticalAlignment.Center;
        _speed.AddThemeFontSizeOverride("font_size", 15);
        _speed.AddThemeColorOverride("font_color", KitTheme.Amber);
    }

    private static void Step(Container parent, string name, string glyph, SimulationController.Speed speed)
    {
        Button button = Button(parent, name, glyph, new Vector2(38, 42), 12);

        if (!Godot.Engine.IsEditorHint())
            button.Pressed += () => SimulationController.Instance.SetSpeed(speed);
    }

    /// <summary>One of the mockups' stat capsules: icon, then the reading.</summary>
    private static Label Capsule(Container parent, string name, string labelName, string icon, UiSurface.Role role, float width)
    {
        PanelContainer plate = RequireNamed<PanelContainer>(parent, name);
        plate.CustomMinimumSize = new Vector2(width, 42);
                // Tighter than the default field inset. A capsule is a chip, not a
        // panel: at the standard padding seven of them plus the speed controls
        // ran past the right edge and took the menu button with them.
        plate.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate(14, 8));

        HBoxContainer row = RequireNamed<HBoxContainer>(plate, "Row");
        row.AddThemeConstantOverride("separation", 6);

        TextureRect image = RequireNamed<TextureRect>(row, "Icon");
        image.Name = "Icon";
        image.Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.png");
        image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        image.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        image.CustomMinimumSize = new Vector2(22, 22);
        image.MouseFilter = MouseFilterEnum.Ignore;

        Label reading = RequireNamed<Label>(row, labelName);
        reading.Name = labelName;
        reading.VerticalAlignment = VerticalAlignment.Center;
        reading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        reading.AddThemeFontSizeOverride("font_size", 15);
        reading.AddThemeColorOverride("font_color", Tint(role));

        return reading;
    }

    private static Button Button(Container parent, string name, string text, Vector2 size, int fontSize)
    {
        Button button = RequireNamed<Button>(parent, name);
        button.Name = name;
        SlateChrome.ApplyChunk(button, text, UiSurface.Role.Neutral, size, fontSize);

        return button;
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

        _cash.Text = $"{snapshot.Cash.Cents / 100.0 / 1e6:N1}M stores";

        _debt.Text = snapshot.Debt.Cents == 0
            ? "clear"
            : $"{snapshot.Debt.Cents / 100.0 / 1e6:N1}M owed";

        _price.Text = $"market {snapshot.OilPrice.Cents / 100.0:N0}";

        // Thirty, not 30.44: the engine's calendar makes every month exactly
        // thirty days, so this is the rate and not an approximation of it.
        _rate.Text = $"{snapshot.ProducedThisTick.CubicMetres / 30.0:N0} oil/day";

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

    private static T? FindNamed<T>(Node at, string name) where T : Node
    {
        if (at is T typed && at.Name == name)
            return typed;

        foreach (Node child in at.GetChildren())
        {
            T? found = FindNamed<T>(child, name);

            if (found is not null)
                return found;
        }

        return null;
    }

    private static T RequireNamed<T>(Node at, string name) where T : Node =>
        FindNamed<T>(at, name) ?? throw new InvalidOperationException(
            $"{nameof(StatusBar)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
