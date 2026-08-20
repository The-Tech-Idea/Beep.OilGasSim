#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Ui;

/// <summary>
/// The right-hand column of the gameplay mockups: objectives, alerts, the
/// production trend and what the company thinks it has left.
///
/// <para>Every panel reads the published snapshot or the tick's own event set.
/// The mockups' fifth panel, Next Payday, is absent — there is no payday in the
/// engine's economics; cash settles every tick.</para>
/// </summary>
public sealed partial class SidePanels : PanelContainer
{
    /// <summary>How many months of production the trend keeps.</summary>
    private const int Window = 36;

    /// <summary>The shipped scenario's deadline, in months.</summary>
    private const int DeadlineMonths = 120;

    private VBoxContainer _objectives = null!;
    private VBoxContainer _alerts = null!;
    private VBoxContainer _reserves = null!;
    private Trend _trend = null!;
    private Label _trendNote = null!;
    private Label _bottleneck = null!;
    private Minimap _minimap = null!;

    private readonly List<string> _log = new();
    private int _lastTick = -1;

    /// <summary>A standing order was switched. Index, and whether it is on.</summary>
    public System.Action<int, bool>? Orders { get; set; }

    /// <summary>
    /// One standing order, with what it will do written under it.
    /// </summary>
    /// <remarks>
    /// The explanation is not decoration. An automation a player cannot see is
    /// one they will blame the engine for, and one they cannot predict is one
    /// they will turn off.
    /// </remarks>
    private static void Order(Container parent, string label, string does, System.Action<bool> set)
    {
        CheckBox box = SlateChrome.Tick(label, false);
        box.Toggled += on => set(on);
        parent.AddChild(box);
        parent.AddChild(SlateChrome.Caption("   " + does));
    }

    /// <summary>A player wants to look at, and act on, something the panel named.</summary>
    [Signal]
    public delegate void GoToEventHandler(ulong element);

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", SlateChrome.Nothing);

        // Scrolled, not clipped. The column's height depends on how many
        // objectives a scenario sets and how many warnings are live, so a fixed
        // stack either wastes a third of the screen on a quiet month or loses the
        // basin off the bottom on a busy one.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };

        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);

        _objectives = SlateChrome.Collapsible("OBJECTIVES", column, 340, UiSurface.Role.Success);
        _alerts = SlateChrome.Collapsible("ALERTS", column, 340, UiSurface.Role.Danger);

        VBoxContainer trend = SlateChrome.Collapsible("PRODUCTION", column, 340, UiSurface.Role.Warning);
        _trend = new Trend { CustomMinimumSize = new Vector2(300, 56) };
        trend.AddChild(_trend);

        _trendNote = SlateChrome.Caption("no production yet");
        trend.AddChild(_trendNote);

        // Where the chain is jammed belongs beside the production it is holding
        // back, not in a sign of its own on the far side of the screen.
        _bottleneck = SlateChrome.Caption(string.Empty);
        _bottleneck.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bottleneck.CustomMinimumSize = new Vector2(290, 0);
        trend.AddChild(_bottleneck);

        VBoxContainer orders = SlateChrome.Collapsible(
            "STANDING ORDERS", column, 340, UiSurface.Role.Success, startFolded: true);

        Order(orders, "Keep the plant running", "send a crew the moment something stops",
            on => Orders?.Invoke(0, on));

        Order(orders, "Answer bottlenecks", "build what a lasting jam needs",
            on => Orders?.Invoke(1, on));

        Order(orders, "Keep the rig busy", "drill the best undrilled structure",
            on => Orders?.Invoke(2, on));

        _reserves = SlateChrome.Collapsible("RESERVES", column, 340, UiSurface.Role.Info, startFolded: true);

        VBoxContainer basin = SlateChrome.Collapsible("THE BASIN", column, 340, UiSurface.Role.Neutral);

        _minimap = new Minimap { CustomMinimumSize = new Vector2(300, 190) };
        basin.AddChild(_minimap);
    }

    public void Bind(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // One sample a tick, taken once. Bind runs every frame; appending on
        // every call would fill the window inside a second and draw a flat line
        // of whatever the current month happens to be.
        if (snapshot.Tick.Value != _lastTick)
        {
            _lastTick = snapshot.Tick.Value;
            _trend.Push(snapshot.ProducedThisTick.CubicMetres);
            Remember(snapshot);
        }

        ShowObjectives(snapshot);
        ShowAlerts(snapshot);
        ShowReserves(snapshot);

        _trendNote.Text = _trend.Peak <= 0.0
            ? "no production yet"
            : $"peak {_trend.Peak:N0} m3/month over {_trend.Count} month{(_trend.Count == 1 ? "" : "s")}";

        _bottleneck.Text = snapshot.Insolvent
            ? "Out of money."
            : snapshot.Bottlenecks.Count > 0
                ? $"{snapshot.Bottlenecks[0].DisplayId} is holding production back."
                : snapshot.Wells == 0
                    ? "Nothing drilled. Survey a structure, or put a hole in one."
                    : string.Empty;
    }

    /// <summary>Redraw the basin, which moves with the truck rather than the tick.</summary>
    public void BindMinimap(World.BasinWorld world, FieldReadModel snapshot, Vector2 truck) =>
        _minimap.Bind(world.Terrain, snapshot, truck, world.Tiles);

    /// <summary>
    /// The scenario's objectives, exactly as the engine judged them.
    /// </summary>
    /// <remarks>
    /// The state and the fraction are both the engine's: it judges at stage 12
    /// and publishes at stage 13, so a host that recomputed "am I there yet"
    /// would be able to disagree with the run it is displaying.
    /// </remarks>
    private void ShowObjectives(FieldReadModel snapshot)
    {
        Clear(_objectives);

        ScenarioProgress progress = snapshot.Progress;

        for (int i = 0; i < progress.Objectives.Count; i++)
        {
            (ContentId objective, ObjectiveState state, double amount) = progress.Objectives[i];

            _objectives.AddChild(SlateChrome.Row2(
                Pretty(objective.ToString()),
                $"{amount * 100.0:F0}%   {state}",
                state switch
                {
                    ObjectiveState.Met => UiSurface.Role.Success,
                    ObjectiveState.Failed or ObjectiveState.Expired => UiSurface.Role.Danger,
                    _ => UiSurface.Role.Warning,
                }));
        }

        if (progress.Objectives.Count == 0)
            _objectives.AddChild(SlateChrome.Caption("the scenario sets no objectives"));

        // The deadline is the scenario's too, and reads next to what it bounds.
        int left = Mathf.Max(0, DeadlineMonths - snapshot.Tick.Value);

        _objectives.AddChild(SlateChrome.Row2(
            "Time left",
            $"{left / 12}y {left % 12}m",
            left <= 12 ? UiSurface.Role.Danger : UiSurface.Role.Info));

        _objectives.AddChild(SlateChrome.Row2(
            "Overall", snapshot.Outcome.ToString(),
            snapshot.Insolvent ? UiSurface.Role.Danger : UiSurface.Role.Neutral));
    }

    /// <summary>
    /// What the engine said this tick, worst first.
    /// </summary>
    /// <remarks>
    /// Only warnings and worse are kept. The bus seals a complete, ordered set
    /// every tick — hundreds of them on a busy month — and a panel showing all of
    /// it would be a log, not an alert list. What is dropped is dropped by
    /// severity, which is the engine's own judgement of what matters.
    /// </remarks>
    private void Remember(FieldReadModel snapshot)
    {
        IReadOnlyList<EngineEvent> events = EngineHost.Instance.EventsThisTick();

        for (int i = 0; i < events.Count; i++)
        {
            EngineEvent raised = events[i];

            if (raised.Severity is not (Severity.Warning or Severity.Critical))
                continue;

            _log.Insert(0, $"{raised.Category} - {raised.GetType().Name} (m{snapshot.Tick.Value})");

            if (_log.Count > 12)
                _log.RemoveAt(_log.Count - 1);
        }
    }

    /// <summary>
    /// What is wrong, and a way to do something about it.
    /// </summary>
    /// <remarks>
    /// <b>A failed element is the front of a path, not a line of text.</b>
    /// Clicking one takes the view to it and selects it, so a player is two
    /// clicks from "the separator has failed" to "a crew is on its way". The
    /// panel already knew; what it lacked was anywhere to go.
    /// </remarks>
    private void ShowAlerts(FieldReadModel snapshot)
    {
        Clear(_alerts);

        int stopped = 0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            ChainElementView element = snapshot.Chain[i];

            if (!element.Failed)
                continue;

            stopped++;
            ulong id = element.Element.Value;

            Button row = SlateChrome.Slab(
                $"  {element.DisplayId} — out of service", false, false, new Vector2(300, 34));

            row.AddThemeColorOverride("font_color", KitTheme.Red.Lightened(0.35f));
            row.Alignment = HorizontalAlignment.Left;
            row.Pressed += () => EmitSignal(SignalName.GoTo, id);
            _alerts.AddChild(row);
        }

        for (int i = 0; i < _log.Count && i < 3 - Mathf.Min(stopped, 2); i++)
            _alerts.AddChild(SlateChrome.Caption(_log[i]));

        if (stopped == 0 && _log.Count == 0)
            _alerts.AddChild(SlateChrome.Caption("nothing the engine called a warning"));
    }

    /// <summary>
    /// Proved, probable and possible — the three the engine publishes.
    /// </summary>
    /// <remarks>
    /// Not the mockup's oil / gas / NGL split, which the read model does not
    /// carry and which the host has no way to derive. These three are what a
    /// reserves book actually reports, and they are already an estimate with a
    /// confidence attached, which is the honest shape for this panel.
    /// </remarks>
    private void ShowReserves(FieldReadModel snapshot)
    {
        Clear(_reserves);

        ReservesEstimate book = snapshot.Reserves;

        _reserves.AddChild(SlateChrome.Row2(
            "Proved", $"{book.Proved.CubicMetres:N0} m3", UiSurface.Role.Success));

        _reserves.AddChild(SlateChrome.Row2(
            "Probable", $"{book.Probable.CubicMetres:N0} m3", UiSurface.Role.Warning));

        _reserves.AddChild(SlateChrome.Row2(
            "Possible", $"{book.Possible.CubicMetres:N0} m3", UiSurface.Role.Neutral));

        // Null is not zero here, and saying so matters: under twelve months of
        // history there is no window to measure over, and printing 0.00 would
        // state a replacement failure that has not happened.
        _reserves.AddChild(SlateChrome.Row2(
            "Replacement",
            snapshot.ReserveReplacementRatio is double ratio
                ? ratio.ToString("F2", CultureInfo.InvariantCulture)
                : "not yet measurable",
            snapshot.ReserveReplacementRatio is double r && r < 1.0
                ? UiSurface.Role.Danger
                : UiSurface.Role.Info));
    }

    private static string Pretty(string id) => id.Replace('-', ' ');

    private static void Clear(Container container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}

/// <summary>
/// The mockups' production trend: what was produced each month, drawn as a line.
///
/// <para><b>The history is the host's, and it is only what was published.</b>
/// The read model carries one month at a time, so a trend needs somebody to
/// remember — and that somebody must be the client, because the engine keeps no
/// series a host can ask for. Each point is a value the engine published on the
/// tick it was sampled; nothing is interpolated, smoothed or filled in, so a gap
/// in play is a gap in the line.</para>
/// </summary>
public sealed partial class Trend : Control
{
    private const int Window = 36;

    private readonly List<double> _points = new();

    /// <summary>How many months are on the chart.</summary>
    public int Count => _points.Count;

    /// <summary>The highest month recorded, which is what the chart scales to.</summary>
    public double Peak { get; private set; }

    public void Push(double value)
    {
        _points.Add(value);

        if (_points.Count > Window)
            _points.RemoveAt(0);

        Peak = 0.0;

        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i] > Peak)
                Peak = _points[i];
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.09f, 0.13f, 0.85f));

        // Three rules, so the eye has something to read height against.
        for (int i = 1; i < 4; i++)
        {
            float y = Size.Y * i / 4.0f;
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), new Color(1, 1, 1, 0.06f));
        }

        if (_points.Count < 2 || Peak <= 0.0)
            return;

        float step = Size.X / (Window - 1);

        for (int i = 1; i < _points.Count; i++)
        {
            var from = new Vector2((i - 1) * step, Height(_points[i - 1]));
            var to = new Vector2(i * step, Height(_points[i]));

            DrawLine(from, to, KitTheme.Amber, 2.0f, antialiased: true);
        }

        // The latest month, marked: on a chart that scrolls, the end is the
        // number a player is actually reading.
        DrawCircle(
            new Vector2((_points.Count - 1) * step, Height(_points[^1])), 3.0f, KitTheme.Amber);
    }

    private float Height(double value) =>
        Size.Y - (float)(value / Peak * (Size.Y - 6.0)) - 3.0f;
}
