#nullable enable

using Godot;
using OGSim.Composition;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Ui;

/// <summary>
/// The HUD of the main-scene mockup, laid out where that image puts things.
///
/// <para>Top-left sign: the date as a player reads it, the cash, the field, a
/// bar. Top-right: the challenge timer. Bottom-left: the hotbar. Bottom-centre:
/// the context prompt, in quotes. Bottom-right: what the run is trying to do.
/// Plan 12 §3 lists exactly those, and this is that list.</para>
///
/// <para><b>Every slot carries a real number.</b> The mockup's "Reputation" and
/// "Actions-Left" have no counterpart in the engine yet, and plan 11 §11 forbids
/// inventing one — so those two slots show what the engine does publish, in the
/// same place and the same style. A made-up percentage would look right and be
/// a lie.</para>
/// </summary>
public sealed partial class GameHud : CanvasLayer
{
    /// <summary>The shipped scenario's target and deadline (EngineBuilder.FirstField).</summary>
    private const double TargetDollars = 600_000_000.0;
    private const int DeadlineMonths = 120;

    private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };

    private Label _date = null!;
    private Label _cash = null!;
    private Label _field = null!;
    private Label _progressText = null!;
    private ProgressBar _progress = null!;
    private Label _timer = null!;
    private Label _prompt = null!;
    private PanelContainer _promptPanel = null!;
    private Label _job = null!;
    private Label _speed = null!;
    private VBoxContainer _toasts = null!;
    private HBoxContainer _hotbar = null!;
    private Minimap _minimap = null!;

    public override void _Ready()
    {
        Layer = 10;

        var root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        root.AddChild(BuildStatusSign());
        root.AddChild(BuildTimerSign());
        root.AddChild(BuildHotbar());
        root.AddChild(BuildJobSign());
        root.AddChild(BuildMinimap());
        root.AddChild(BuildPrompt());
        root.AddChild(BuildToastColumn());
    }

    public void Bind(FieldReadModel snapshot)
    {
        // The engine's calendar is 30/360, so a month is a third of a season and
        // a day an exact subdivision — the mockup's "Day / Season / Year" is the
        // same clock, read the way a player reads it.
        string season = Seasons[((snapshot.Date.Month - 1) / 3) % 4];

        _date.Text = $"{season} - Year {snapshot.Date.Year - 1964}";
        _cash.Text = $"${snapshot.Cash.Cents / 100.0 / 1_000_000.0:N1}M";
        _field.Text = $"{snapshot.Wells} well{(snapshot.Wells == 1 ? "" : "s")} - " +
                      $"{snapshot.ProducedThisTick.CubicMetres:N0} m3 - " +
                      $"{snapshot.ActivitiesRunning} running";

        double progress = Mathf.Clamp(snapshot.Cash.Cents / 100.0 / TargetDollars, 0.0, 1.0);
        _progress.Value = progress;
        _progressText.Text = $"{progress * 100.0:F0}% of $600M";

        int left = Mathf.Max(0, DeadlineMonths - snapshot.Tick.Value);
        _timer.Text = $"{left / 12}y {left % 12}m";

        _job.Text = snapshot.Insolvent
            ? "Out of money."
            : snapshot.Bottlenecks.Count > 0
                ? $"Bottleneck: {snapshot.Bottlenecks[0].DisplayId} is holding production back."
                : snapshot.Wells == 0
                    ? "Find oil: survey a structure, or put a hole in one."
                    : "Keep the field flowing.";
    }

    public void BindSpeed(SimulationController.Speed speed) =>
        _speed.Text = speed == SimulationController.Speed.Paused ? "PAUSED" : speed.ToString().ToUpperInvariant();

    /// <summary>Offer, or stop offering, the thing the truck is standing at.</summary>
    public void ShowPrompt(string? text)
    {
        _promptPanel.Visible = text is not null;

        if (text is not null)
            _prompt.Text = "“" + text + "”";
    }

    /// <summary>Fill the hotbar slots with the actions on offer here.</summary>
    public void BindHotbar(string[] actions)
    {
        for (int i = 0; i < _hotbar.GetChildCount(); i++)
        {
            var slot = (PanelContainer)_hotbar.GetChild(i);
            var label = slot.GetChild<Label>(0);
            bool live = i < actions.Length;

            label.Text = live ? $"{i + 1}\n{actions[i]}" : $"{i + 1}\n-";
            label.AddThemeColorOverride("font_color", live ? ScreenChrome.Cream : ScreenChrome.Faded);
            slot.AddThemeStyleboxOverride(
                "panel",
                ScreenChrome.FlatBox(live ? ScreenChrome.WoodRim : ScreenChrome.WoodDark, radius: 6));
        }
    }

    public void Toast(string message, bool bad)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", ScreenChrome.FlatBox(bad ? ScreenChrome.Bad : ScreenChrome.WoodDark));

        Label label = ScreenChrome.Text(message, 17, ScreenChrome.Cream);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(470, 0);
        panel.AddChild(label);
        _toasts.AddChild(panel);

        Tween tween = CreateTween();
        tween.TweenInterval(bad ? 4.5f : 2.8f);
        tween.TweenProperty(panel, "modulate:a", 0.0f, 0.5f);
        tween.TweenCallback(Callable.From(panel.QueueFree));

        while (_toasts.GetChildCount() > 5)
            _toasts.GetChild(0).QueueFree();
    }

    private Control BuildStatusSign()
    {
        PanelContainer sign = ScreenChrome.Sign(
            string.Empty, new Vector2(320, 0), Control.LayoutPreset.TopLeft, new Vector2(18, 18));

        VBoxContainer column = ScreenChrome.ContentOf(sign);
        column.AddThemeConstantOverride("separation", 6);

        _date = ScreenChrome.Text("Spring - Year 1", 22, ScreenChrome.Cream);
        _cash = ScreenChrome.Text("$0.0M", 30, ScreenChrome.Gold);
        _field = ScreenChrome.Text("0 wells", 16, ScreenChrome.Faded);

        column.AddChild(_date);
        column.AddChild(_cash);
        column.AddChild(_field);

        _progress = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = 0.0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(288, 18),
        };

        _progress.AddThemeStyleboxOverride("background", ScreenChrome.FlatBox(ScreenChrome.WoodDark, radius: 9));
        _progress.AddThemeStyleboxOverride("fill", ScreenChrome.FlatBox(ScreenChrome.Good, radius: 9));
        column.AddChild(_progress);

        _progressText = ScreenChrome.Text("0% of $600M", 15, ScreenChrome.Faded);
        column.AddChild(_progressText);

        return sign;
    }

    private Control BuildTimerSign()
    {
        PanelContainer sign = ScreenChrome.Sign(
            "CHALLENGE", new Vector2(210, 0), Control.LayoutPreset.TopRight, new Vector2(-18, 18));

        sign.GrowHorizontal = Control.GrowDirection.Begin;

        VBoxContainer column = ScreenChrome.ContentOf(sign);
        _timer = ScreenChrome.Text("10y 0m", 30, ScreenChrome.Cream, HorizontalAlignment.Center);
        _speed = ScreenChrome.Text("PAUSED", 16, ScreenChrome.Gold, HorizontalAlignment.Center);
        column.AddChild(_timer);
        column.AddChild(_speed);

        return sign;
    }

    private Control BuildHotbar()
    {
        PanelContainer sign = ScreenChrome.Sign(
            string.Empty, Vector2.Zero, Control.LayoutPreset.BottomLeft, new Vector2(18, -18));

        sign.GrowVertical = Control.GrowDirection.Begin;

        _hotbar = new HBoxContainer();
        _hotbar.AddThemeConstantOverride("separation", 8);

        for (int i = 0; i < 5; i++)
        {
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(124, 64) };
            slot.AddThemeStyleboxOverride("panel", ScreenChrome.FlatBox(ScreenChrome.WoodDark, radius: 6));

            Label label = ScreenChrome.Text($"{i + 1}\n-", 14, ScreenChrome.Faded, HorizontalAlignment.Center);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            slot.AddChild(label);
            _hotbar.AddChild(slot);
        }

        ScreenChrome.ContentOf(sign).AddChild(_hotbar);

        return sign;
    }

    private Control BuildJobSign()
    {
        PanelContainer sign = ScreenChrome.Sign(
            "THE RUN", new Vector2(330, 0), Control.LayoutPreset.BottomRight, new Vector2(-18, -18));

        sign.GrowHorizontal = Control.GrowDirection.Begin;
        sign.GrowVertical = Control.GrowDirection.Begin;

        _job = ScreenChrome.Text("Find oil.", 17, ScreenChrome.Cream);
        _job.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _job.CustomMinimumSize = new Vector2(298, 0);
        ScreenChrome.ContentOf(sign).AddChild(_job);

        return sign;
    }

    private Control BuildMinimap()
    {
        PanelContainer sign = ScreenChrome.Sign(
            "THE BASIN", new Vector2(230, 0), Control.LayoutPreset.TopRight, new Vector2(-18, 132));

        sign.GrowHorizontal = Control.GrowDirection.Begin;

        _minimap = new Minimap
        {
            CustomMinimumSize = new Vector2(198, 198),
        };

        ScreenChrome.ContentOf(sign).AddChild(_minimap);

        return sign;
    }

    /// <summary>Redraw the basin overview. Called with the world, which owns the ground.</summary>
    public void BindMinimap(World.BasinWorld world, FieldReadModel snapshot, Vector2 truck) =>
        _minimap.Bind(world.Terrain, snapshot, truck, world.Tiles);

    private Control BuildPrompt()
    {
        _promptPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(460, 0) };
        _promptPanel.AddThemeStyleboxOverride(
            "panel", ScreenChrome.FlatBox(new Color(0.10f, 0.07f, 0.04f, 0.82f), radius: 10));

        _promptPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _promptPanel.Position = new Vector2(-230, -120);

        _prompt = ScreenChrome.Text(string.Empty, 22, ScreenChrome.Cream, HorizontalAlignment.Center);
        _promptPanel.AddChild(_prompt);

        return _promptPanel;
    }

    private Control BuildToastColumn()
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        holder.Position = new Vector2(-250, 24);
        holder.CustomMinimumSize = new Vector2(500, 0);

        _toasts = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        _toasts.AddThemeConstantOverride("separation", 8);
        holder.AddChild(_toasts);

        return holder;
    }
}
