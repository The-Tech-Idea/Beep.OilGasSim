#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
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
[Tool]
public sealed partial class SidePanels : PanelContainer
{
    /// <summary>How many months of production the trend keeps.</summary>
    private const int Window = 36;

    /// <summary>The shipped scenario's deadline, in months.</summary>
    private const int DeadlineMonths = 120;

    private VBoxContainer _objectives = null!;
    private VBoxContainer _alerts = null!;
    private VBoxContainer _reserves = null!;
    private PanelContainer _objectiveRowTemplate = null!;
    private Label _objectiveCaptionTemplate = null!;
    private Button _alertButtonTemplate = null!;
    private Label _alertCaptionTemplate = null!;
    private PanelContainer _reserveRowTemplate = null!;
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
    private static void Order(Container parent, string name, string label, string captionName, string does, System.Action<bool> set, bool connect = true)
    {
        CheckBox box = RequireNamed<CheckBox>(parent, name);
        box.Text = label;
        box.ButtonPressed = false;
        box.AddThemeFontSizeOverride("font_size", 15);

        if (box is KitCheckBox kit)
            kit.OnRole = UiSurface.Role.Success;

        if (connect)
            box.Toggled += on => set(on);

        Label caption = RequireNamed<Label>(parent, captionName);
        caption.Text = "   " + does;
        caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        caption.AddThemeFontSizeOverride("font_size", 13);
        caption.AddThemeColorOverride("font_color", KitTheme.Muted);
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
        ScrollContainer scroll = RequireNamed<ScrollContainer>(this, "SideScroll");
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;

        VBoxContainer column = RequireNamed<VBoxContainer>(scroll, "SideColumn");
        column.CustomMinimumSize = new Vector2(340, 0);
        column.AddThemeConstantOverride("separation", 6);

        _objectives = SlateChrome.Collapsible(
            "OBJECTIVES", column, 340, UiSurface.Role.Success, bodyName: "ObjectivesBody");
        _alerts = SlateChrome.Collapsible(
            "ALERTS", column, 340, UiSurface.Role.Danger, bodyName: "AlertsBody");

        _objectiveRowTemplate = RequireNamed<PanelContainer>(_objectives, "ObjectiveRowTemplate");
        StyleInfoRow(_objectiveRowTemplate, UiSurface.Role.Success);
        _objectiveRowTemplate.Visible = Godot.Engine.IsEditorHint();

        _objectiveCaptionTemplate = RequireNamed<Label>(_objectives, "ObjectiveCaptionTemplate");
        StyleCaption(_objectiveCaptionTemplate);
        _objectiveCaptionTemplate.Visible = Godot.Engine.IsEditorHint();

        _alertButtonTemplate = RequireNamed<Button>(_alerts, "AlertButtonTemplate");
        StyleAlertButton(_alertButtonTemplate);
        _alertButtonTemplate.Visible = Godot.Engine.IsEditorHint();

        _alertCaptionTemplate = RequireNamed<Label>(_alerts, "AlertCaptionTemplate");
        StyleCaption(_alertCaptionTemplate);
        _alertCaptionTemplate.Visible = Godot.Engine.IsEditorHint();

        VBoxContainer trend = SlateChrome.Collapsible(
            "PRODUCTION", column, 340, UiSurface.Role.Warning, bodyName: "ProductionBody");

        _trend = RequireNamed<Trend>(trend, "ProductionTrend");
        _trend.CustomMinimumSize = new Vector2(300, 56);

        _trendNote = RequireNamed<Label>(trend, "TrendNote");
        _trendNote.Name = "TrendNote";

        // Where the chain is jammed belongs beside the production it is holding
        // back, not in a sign of its own on the far side of the screen.
        _bottleneck = RequireNamed<Label>(trend, "Bottleneck");
        _bottleneck.Name = "Bottleneck";
        _bottleneck.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bottleneck.CustomMinimumSize = new Vector2(290, 0);

        VBoxContainer orders = SlateChrome.Collapsible(
            "STANDING ORDERS", column, 340, UiSurface.Role.Success, startFolded: true,
            bodyName: "OrdersBody");

        bool runtime = !Godot.Engine.IsEditorHint();

        Order(orders, "KeepPlantOrder", "Keep the plant running", "KeepPlantOrderCaption",
            "send a crew the moment something stops",
            on => Orders?.Invoke(0, on), runtime);

        Order(orders, "AnswerBottlenecksOrder", "Answer bottlenecks", "AnswerBottlenecksOrderCaption",
            "build what a lasting jam needs",
            on => Orders?.Invoke(1, on), runtime);

        Order(orders, "KeepRigBusyOrder", "Keep the rig busy", "KeepRigBusyOrderCaption",
            "drill the best undrilled structure",
            on => Orders?.Invoke(2, on), runtime);

        _reserves = SlateChrome.Collapsible(
            "RESERVES", column, 340, UiSurface.Role.Info, startFolded: true,
            bodyName: "ReservesBody");

        _reserveRowTemplate = RequireNamed<PanelContainer>(_reserves, "ReserveRowTemplate");
        StyleInfoRow(_reserveRowTemplate, UiSurface.Role.Info);
        _reserveRowTemplate.Visible = Godot.Engine.IsEditorHint();

        VBoxContainer basin = SlateChrome.Collapsible(
            "THE BASIN", column, 340, UiSurface.Role.Neutral, bodyName: "BasinBody");

        _minimap = RequireNamed<Minimap>(basin, "Minimap");
        _minimap.CustomMinimumSize = new Vector2(300, 190);
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

            _objectives.AddChild(InfoRow(
                _objectiveRowTemplate,
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
            _objectives.AddChild(Caption(_objectiveCaptionTemplate, "the scenario sets no objectives"));

        // The deadline is the scenario's too, and reads next to what it bounds.
        int left = Mathf.Max(0, DeadlineMonths - snapshot.Tick.Value);

        _objectives.AddChild(InfoRow(
            _objectiveRowTemplate,
            "Time left",
            $"{left / 12}y {left % 12}m",
            left <= 12 ? UiSurface.Role.Danger : UiSurface.Role.Info));

        _objectives.AddChild(InfoRow(
            _objectiveRowTemplate,
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

            Button row = AlertButton($"{element.DisplayId} - out of service");
            row.Pressed += () => EmitSignal(SignalName.GoTo, id);
            _alerts.AddChild(row);
        }

        for (int i = 0; i < _log.Count && i < 3 - Mathf.Min(stopped, 2); i++)
            _alerts.AddChild(Caption(_alertCaptionTemplate, _log[i]));

        if (stopped == 0 && _log.Count == 0)
            _alerts.AddChild(Caption(_alertCaptionTemplate, "nothing the engine called a warning"));
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

        _reserves.AddChild(InfoRow(
            _reserveRowTemplate,
            "Proved", $"{book.Proved.CubicMetres:N0} m3", UiSurface.Role.Success));

        _reserves.AddChild(InfoRow(
            _reserveRowTemplate,
            "Probable", $"{book.Probable.CubicMetres:N0} m3", UiSurface.Role.Warning));

        _reserves.AddChild(InfoRow(
            _reserveRowTemplate,
            "Possible", $"{book.Possible.CubicMetres:N0} m3", UiSurface.Role.Neutral));

        // Null is not zero here, and saying so matters: under twelve months of
        // history there is no window to measure over, and printing 0.00 would
        // state a replacement failure that has not happened.
        _reserves.AddChild(InfoRow(
            _reserveRowTemplate,
            "Replacement",
            snapshot.ReserveReplacementRatio is double ratio
                ? ratio.ToString("F2", CultureInfo.InvariantCulture)
                : "not yet measurable",
            snapshot.ReserveReplacementRatio is double r && r < 1.0
                ? UiSurface.Role.Danger
                : UiSurface.Role.Info));
    }

    private static string Pretty(string id) => id.Replace('-', ' ');

    private static PanelContainer InfoRow(PanelContainer template, string label, string value, UiSurface.Role role)
    {
        var row = (PanelContainer)template.Duplicate();
        row.Name = "InfoRow";
        row.Visible = true;
        StyleInfoRow(row, role);

        RequireNamed<Label>(row, "Label").Text = label;
        RequireNamed<Label>(row, "Value").Text = value;

        return row;
    }

    private Button AlertButton(string text)
    {
        var row = (Button)_alertButtonTemplate.Duplicate();
        row.Name = "AlertButton";
        row.Visible = true;
        StyleAlertButton(row);
        row.Text = "  " + text;
        return row;
    }

    private static Label Caption(Label template, string text)
    {
        var label = (Label)template.Duplicate();
        label.Name = "Caption";
        label.Visible = true;
        label.Text = text;
        StyleCaption(label);
        return label;
    }

    private static void StyleInfoRow(PanelContainer row, UiSurface.Role role)
    {
        row.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());
        row.MouseFilter = MouseFilterEnum.Ignore;

        HBoxContainer line = RequireNamed<HBoxContainer>(row, "Line");
        line.AddThemeConstantOverride("separation", 8);

        Label label = RequireNamed<Label>(row, "Label");
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);

        Label value = RequireNamed<Label>(row, "Value");
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        value.AddThemeFontSizeOverride("font_size", 13);
        value.AddThemeColorOverride("font_color", role switch
        {
            UiSurface.Role.Success => KitTheme.Green.Lightened(0.35f),
            UiSurface.Role.Warning => KitTheme.Amber,
            UiSurface.Role.Danger => KitTheme.Red.Lightened(0.35f),
            UiSurface.Role.Info => KitTheme.Sky,
            _ => KitTheme.Ink,
        });
    }

    private static void StyleAlertButton(Button row)
    {
        row.CustomMinimumSize = new Vector2(300, 34);
        row.Alignment = HorizontalAlignment.Left;
        row.AddThemeColorOverride("font_color", KitTheme.Red.Lightened(0.35f));
        row.AddThemeColorOverride("font_hover_color", KitTheme.Amber);
        row.AddThemeStyleboxOverride("normal", SlateChrome.Row(false));
        row.AddThemeStyleboxOverride("hover", SlateChrome.Row(true));
        row.AddThemeStyleboxOverride("pressed", SlateChrome.Row(true));
        row.AddThemeStyleboxOverride("focus", SlateChrome.Nothing);
    }

    private static void StyleCaption(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitTheme.Muted);
    }

    private static void Clear(Container container)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child.Name.ToString().EndsWith("Template", StringComparison.Ordinal))
                continue;

            container.RemoveChild(child);
            child.QueueFree();
        }
    }

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
            $"{nameof(SidePanels)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}

