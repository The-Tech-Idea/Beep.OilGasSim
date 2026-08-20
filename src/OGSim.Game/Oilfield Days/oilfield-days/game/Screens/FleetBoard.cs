#nullable enable

using Beep.ECS.UI;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The yard — the fleet/garage mockup, built to its layout.
///
/// <para>A wooden panel of rows down the left, each with art, a name, two bars
/// and a state; a parchment card on the right with the selected thing large, its
/// meters, and what it is doing; a row of actions along the bottom. That is
/// mockup 4.</para>
///
/// <para><b>The rows are what the company owns and the engine reports.</b> There
/// are no vehicles or fuel gauges in the engine and plan 11 §11 forbids inventing
/// them, so the two bars carry real numbers: for a well, its share of the field's
/// month and whether it is flowing; for a chain element, how much of what it was
/// offered it actually passed.</para>
/// </summary>
public sealed partial class FleetBoard : Control
{
    private enum Tab
    {
        Wells,
        Chain,
        Rig,
    }

    private VBoxContainer _rows = null!;
    private VBoxContainer _detail = null!;
    private HBoxContainer _tabs = null!;
    private Control? _topBar;
    private Tab _tab = Tab.Wells;
    private int _selected;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(SlateChrome.Backdrop());

        BuildList();
        BuildDetail();
        BuildActions();
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.CloseOverlay();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildList()
    {
        PanelContainer sign = SlateChrome.Sign(
            "OILFIELD FLEET", new Vector2(660, 560), LayoutPreset.CenterLeft, new Vector2(36, -310));

        AddChild(sign);

        VBoxContainer column = SlateChrome.ContentOf(sign);

        _tabs = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 8);
        column.AddChild(_tabs);

        AddTab("WELLS", Tab.Wells);
        AddTab("CHAIN", Tab.Chain);
        AddTab("RIG", Tab.Rig);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(626, 430) };
        _rows = new VBoxContainer { CustomMinimumSize = new Vector2(612, 0) };
        _rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_rows);
        column.AddChild(scroll);
    }

    private void BuildDetail()
    {
        PanelContainer sign = SlateChrome.Sign(
            "DETAIL", new Vector2(470, 560), LayoutPreset.CenterRight, new Vector2(-36, -310));

        sign.GrowHorizontal = GrowDirection.Begin;
        AddChild(sign);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(436, 470) };
        paper.AddThemeStyleboxOverride("panel", SlateChrome.FieldPlate());

        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 10);
        paper.AddChild(_detail);
        SlateChrome.ContentOf(sign).AddChild(paper);
    }

    private void BuildActions()
    {
        PanelContainer bar = SlateChrome.Sign(
            string.Empty, new Vector2(700, 0), LayoutPreset.CenterBottom, new Vector2(-350, -18));

        bar.GrowVertical = GrowDirection.Begin;
        AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        Button dispatch = SlateChrome.Chunk("DISPATCH BOARD", UiSurface.Role.Success, new Vector2(280, 50));
        dispatch.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard);
        row.AddChild(dispatch);

        Button lease = SlateChrome.Chunk("THE LEASE", UiSurface.Role.Neutral, new Vector2(200, 50));
        lease.Pressed += () => SceneRouter.Instance.OpenOverlay(SceneRouter.LeaseBoard);
        row.AddChild(lease);

        Button back = SlateChrome.Chunk("BACK", UiSurface.Role.Danger, new Vector2(150, 50));
        back.Pressed += () => SceneRouter.Instance.CloseOverlay();
        row.AddChild(back);

        SlateChrome.ContentOf(bar).AddChild(row);
    }

    private void AddTab(string text, Tab tab)
    {
        Button button = SlateChrome.Action(
            text,
            _tab == tab ? KitTheme.Green : KitTheme.Void,
            new Vector2(200, 42),
            fontSize: 16);

        button.Pressed += () =>
        {
            _tab = tab;
            _selected = 0;
            Refresh();
        };

        _tabs.AddChild(button);
    }

    private void Refresh()
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        if (_topBar is not null && IsInstanceValid(_topBar))
            _topBar.QueueFree();

        _topBar = DispatchBoard.Header(snapshot);
        AddChild(_topBar);

        foreach (Node child in _tabs.GetChildren())
            child.QueueFree();

        AddTab("WELLS", Tab.Wells);
        AddTab("CHAIN", Tab.Chain);
        AddTab("RIG", Tab.Rig);

        foreach (Node child in _rows.GetChildren())
            child.QueueFree();

        switch (_tab)
        {
            case Tab.Wells:
                ShowWells(snapshot);
                break;

            case Tab.Chain:
                ShowChain(snapshot);
                break;

            default:
                ShowRig(snapshot);
                break;
        }
    }

    private void ShowWells(FieldReadModel snapshot)
    {
        if (snapshot.Wellbores.Count == 0)
        {
            _rows.AddChild(SlateChrome.Text("Nothing drilled yet.", 18, KitTheme.Ink));
            ShowDetail("drilling-rig-derrick", "No wells", "The company owns a rig and a lease, and that is all.", []);
            return;
        }

        double most = 1.0;

        for (int i = 0; i < snapshot.Wellbores.Count; i++)
            most = Mathf.Max(most, snapshot.Wellbores[i].ProducedThisTick.CubicMetres);

        for (int i = 0; i < snapshot.Wellbores.Count; i++)
        {
            WellStatusView well = snapshot.Wellbores[i];
            int index = i;
            bool flowing = well.Status == WellStatus.Producing;

            Button row = Row(
                flowing ? "pumpjack" : "wellhead-tree",
                well.DisplayId,
                well.Status.ToString(),
                ("output", well.ProducedThisTick.CubicMetres / most, flowing ? KitTheme.Green : KitTheme.Muted),
                ("status", flowing ? 1.0 : 0.0, flowing ? KitTheme.Green : KitTheme.Red),
                i == _selected);

            row.Pressed += () =>
            {
                _selected = index;
                Refresh();
            };

            _rows.AddChild(row);
        }

        WellStatusView chosen = snapshot.Wellbores[Mathf.Clamp(_selected, 0, snapshot.Wellbores.Count - 1)];

        ShowDetail(
            chosen.Status == WellStatus.Producing ? "pumpjack" : "wellhead-tree",
            chosen.DisplayId,
            "Open, shut, test, log, core or abandon it from the truck, standing at the wellhead.",
            [
                ("Output this month", chosen.ProducedThisTick.CubicMetres / most, $"{chosen.ProducedThisTick.CubicMetres:N0} m3", KitTheme.Green),
                ("Flowing", chosen.Status == WellStatus.Producing ? 1.0 : 0.0, chosen.Status.ToString(), KitTheme.Green),
            ]);
    }

    private void ShowChain(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            ChainElementView element = snapshot.Chain[i];
            int index = i;
            double held = Held(element);
            double offered = element.Throughput.Kilograms + held;
            double passed = offered <= 0.0 ? 1.0 : element.Throughput.Kilograms / offered;

            Button row = Row(
                IconFor(element.DisplayId),
                element.DisplayId,
                held > 0.0 ? "throttling" : "clear",
                ("passed", passed, held > 0.0 ? KitTheme.Red : KitTheme.Green),
                ("throughput", offered <= 0.0 ? 0.0 : element.Throughput.Kilograms / Mathf.Max(1.0, Busiest(snapshot)),
                    KitTheme.Green),
                i == _selected);

            row.Pressed += () =>
            {
                _selected = index;
                Refresh();
            };

            _rows.AddChild(row);
        }

        if (snapshot.Chain.Count == 0)
            return;

        ChainElementView chosen = snapshot.Chain[Mathf.Clamp(_selected, 0, snapshot.Chain.Count - 1)];
        double chosenHeld = Held(chosen);
        double chosenOffered = chosen.Throughput.Kilograms + chosenHeld;

        ShowDetail(
            IconFor(chosen.DisplayId),
            chosen.DisplayId,
            chosenHeld > 0.0
                ? "This is where the field is jammed. Install another separator or expand export, from the truck at the plant."
                : "Passing everything it is offered.",
            [
                ("Passed of what it was offered", chosenOffered <= 0.0 ? 1.0 : chosen.Throughput.Kilograms / chosenOffered,
                    $"{chosen.Throughput.Kilograms / 1000.0:N0} t", chosenHeld > 0.0 ? KitTheme.Red : KitTheme.Green),
                ("Held back", chosenOffered <= 0.0 ? 0.0 : chosenHeld / chosenOffered,
                    $"{chosenHeld / 1000.0:N0} t", KitTheme.Amber),
            ]);
    }

    private void ShowRig(FieldReadModel snapshot)
    {
        bool busy = snapshot.ActivitiesRunning > 0;

        _rows.AddChild(Row(
            "drilling-rig-derrick",
            "Drilling rig",
            busy ? "out on a job" : "in the yard",
            ("busy", busy ? 1.0 : 0.0, busy ? KitTheme.Amber : KitTheme.Green),
            ("activities", Mathf.Min(1.0, snapshot.ActivitiesRunning / 3.0), KitTheme.Green),
            selected: true));

        ShowDetail(
            "drilling-rig-derrick",
            "One rig",
            "The company owns a single rig, and that is what makes drilling a decision rather than a list. " +
            "While it is out, another hole waits — the engine refuses the order rather than queueing it.",
            [("Working", busy ? 1.0 : 0.0, busy ? "out" : "idle", KitTheme.Amber)]);
    }

    /// <summary>The mockup's row: art, name, state, and two bars.</summary>
    private static Button Row(
        string icon,
        string name,
        string state,
        (string Label, double Value, Color Colour) first,
        (string Label, double Value, Color Colour) second,
        bool selected)
    {
        Button card = SlateChrome.Slab(string.Empty, selected, false, new Vector2(632, 92));

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        SlateChrome.LayAcross(row, "field");
        row.OffsetTop = 8;
        row.OffsetBottom = -8;
        row.AddThemeConstantOverride("separation", 12);
        card.AddChild(row);

        row.AddChild(SlateChrome.Icon(icon, 58.0f));

        var names = new VBoxContainer { CustomMinimumSize = new Vector2(180, 0) };
        names.AddChild(SlateChrome.Text(name, 17, KitTheme.Ink));
        names.AddChild(SlateChrome.Text(state, 14, new Color(0.42f, 0.36f, 0.28f)));
        row.AddChild(names);

        var bars = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bars.AddThemeConstantOverride("separation", 6);
        bars.AddChild(Bar(first.Label, first.Value, first.Colour));
        bars.AddChild(Bar(second.Label, second.Value, second.Colour));
        row.AddChild(bars);

        return card;
    }

    private static Control Bar(string label, double value, Color colour)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        Label name = SlateChrome.Text(label, 13, new Color(0.45f, 0.40f, 0.34f));
        name.CustomMinimumSize = new Vector2(86, 0);
        row.AddChild(name);

        var bar = new ProgressBar
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = Mathf.Clamp(value, 0.0, 1.0),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(176, 16),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
        bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));
        row.AddChild(bar);

        row.AddChild(SlateChrome.Text($"{value * 100:F0}%", 13, new Color(0.42f, 0.36f, 0.28f)));

        return row;
    }

    private void ShowDetail(string icon, string title, string body, (string Label, double Value, string Readout, Color Colour)[] meters)
    {
        foreach (Node child in _detail.GetChildren())
            child.QueueFree();

        _detail.AddChild(SlateChrome.Icon(icon, 150.0f));
        _detail.AddChild(SlateChrome.Text(title, 24, KitTheme.Ink, HorizontalAlignment.Center));

        foreach ((string label, double value, string readout, Color colour) in meters)
        {
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 2);
            column.AddChild(SlateChrome.Text($"{label}: {readout}", 15, KitTheme.Ink));

            var bar = new ProgressBar
            {
                MinValue = 0.0,
                MaxValue = 1.0,
                Value = Mathf.Clamp(value, 0.0, 1.0),
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(400, 18),
            };

            bar.AddThemeStyleboxOverride("background", SlateChrome.Track());
            bar.AddThemeStyleboxOverride("fill", SlateChrome.Fill(colour));
            column.AddChild(bar);
            _detail.AddChild(column);
        }

        Label text = SlateChrome.Body(body, 16);
        text.CustomMinimumSize = new Vector2(404, 0);
        _detail.AddChild(text);
    }

    private static double Held(ChainElementView element)
    {
        double held = 0.0;

        for (int i = 0; i < element.Deferred.Count; i++)
            held += element.Deferred[i].Deferred.Kilograms;

        return held;
    }

    private static double Busiest(FieldReadModel snapshot)
    {
        double most = 1.0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
            most = Mathf.Max(most, snapshot.Chain[i].Throughput.Kilograms);

        return most;
    }

    private static string IconFor(string displayId)
    {
        if (displayId.StartsWith("gathering", System.StringComparison.Ordinal))
            return "choke-manifold";

        if (displayId.StartsWith("well", System.StringComparison.Ordinal))
            return "wellhead-tree";

        return displayId switch
        {
            "separator" => "three-phase-separator",
            "tank" => "crude-oil-storage-tank",
            "flare" => "flare-stack",
            "water-disposal" => "water-injection-pump",
            "custody-meter" => "metering-station",
            "flowline" => "pipe-rack-section",
            _ => "pipeline-manifold",
        };
    }
}
