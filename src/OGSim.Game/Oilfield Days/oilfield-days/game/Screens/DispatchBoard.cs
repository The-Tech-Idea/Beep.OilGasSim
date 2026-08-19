#nullable enable

using System;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// The dispatch terminal — the job-board mockup, built to its layout.
///
/// <para>Board on the left with icon cards, state plates and difficulty stamps;
/// the order on the right on parchment with objective, equipment, destination,
/// reward and time; a green dispatch and a red back beneath it; the equipment
/// strip along the bottom. That is mockup 2, panel for panel.</para>
///
/// <para><b>The cards are the engine's nine commands, not invented jobs.</b>
/// Plan 11 §7 forbids faking engine state, so a card's reward is what the engine
/// will actually do, its state is whether the engine would take it now, and its
/// stamp — EASY, FAIR, HARD — is read off the target's probability of success
/// rather than assigned.</para>
/// </summary>
public sealed partial class DispatchBoard : Control
{
    private static readonly Length WellDepth = new(2000.0);

    private sealed record Order(
        string Title,
        string Icon,
        string Objective,
        string Equipment,
        string EquipmentIcon,
        string Reward,
        bool NeedsRig,
        Func<FieldReadModel, bool> Possible,
        Func<FieldReadModel, Command?> Build);

    private static readonly Order[] Catalogue =
    {
        new("Shoot seismic", "well-testing-skid",
            "Send the survey crew over a structure. It sharpens the trap and structure beliefs without putting a hole down.",
            "Survey crew", "well-testing-skid",
            "A sharper probability of success", false,
            s => s.Prospects.Count > 0,
            s => new SeismicSurveyCommand(new EntityId<IProspect>(Best(s).Prospect.Value))),

        new("Drill a well", "drilling-rig-derrick",
            "Put a 2,000 m hole into the best structure the company has. Months of rig time, and it may come back dry.",
            "The rig", "drilling-rig-derrick",
            "A well, or the knowledge there is nothing there", true,
            s => s.Prospects.Count > 0,
            s => new DrillWellCommand(new EntityId<IProspect>(Best(s).Prospect.Value), WellDepth)),

        new("Run a well test", "well-testing-skid",
            "Shut the well in and watch the pressure build back. The sharpest thing the company can learn about the rock.",
            "Well testing skid", "well-testing-skid",
            "Reservoir pressure, much better known", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new WellTestCommand(c) : null),

        new("Run a wireline log", "wireline-service-truck",
            "Run tools down the hole on a cable. Refines porosity and permeability.",
            "Wireline truck", "wireline-service-truck",
            "Porosity and permeability, refined", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new WirelineLogCommand(c) : null),

        new("Cut a core", "power-swivel-unit",
            "Bring rock to surface. Slow, expensive, and the least uncertain measurement there is.",
            "Coring assembly", "power-swivel-unit",
            "The rock itself, measured", true,
            s => Compartment(s) is not null,
            s => Compartment(s) is EntityId<IReservoirCompartmentEntity> c ? new CutCoreCommand(c) : null),

        new("Install another separator", "three-phase-separator",
            "Debottleneck separation so the chain stops holding production back.",
            "Construction crew", "mobile-crane-truck",
            "Separation capacity the field is short of", false,
            _ => true,
            _ => new InstallSeparatorCommand()),

        new("Expand export capacity", "metering-station",
            "Take the ceiling off what can leave the field.",
            "Construction crew", "mobile-crane-truck",
            "A higher export ceiling", false,
            _ => true,
            _ => new ExpandExportCommand()),
    };

    private VBoxContainer _cards = null!;
    private VBoxContainer _detail = null!;
    private HBoxContainer _fleet = null!;
    private Control? _topBar;
    private Label _status = null!;
    private Button _dispatch = null!;
    private int _selected;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ScreenChrome.Backdrop());

        BuildBoard();
        BuildDetail();
        BuildFleetStrip();
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

    private void BuildBoard()
    {
        PanelContainer board = ScreenChrome.Sign(
            "DISPATCH BOARD", new Vector2(600, 540), LayoutPreset.CenterLeft, new Vector2(40, -300));

        AddChild(board);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(566, 452) };
        _cards = new VBoxContainer { CustomMinimumSize = new Vector2(552, 0) };
        _cards.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_cards);
        ScreenChrome.ContentOf(board).AddChild(scroll);
    }

    private void BuildDetail()
    {
        PanelContainer sign = ScreenChrome.Sign(
            "THE ORDER", new Vector2(560, 540), LayoutPreset.CenterRight, new Vector2(-40, -300));

        sign.GrowHorizontal = GrowDirection.Begin;
        AddChild(sign);

        VBoxContainer column = ScreenChrome.ContentOf(sign);

        var paper = new PanelContainer { CustomMinimumSize = new Vector2(524, 330) };
        paper.AddThemeStyleboxOverride("panel", ScreenChrome.PaperBox());

        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 10);
        paper.AddChild(_detail);
        column.AddChild(paper);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);

        _dispatch = ScreenChrome.Action("DISPATCH", ScreenChrome.Good, new Vector2(330, 52));
        _dispatch.Pressed += () => Send(Catalogue[_selected]);
        buttons.AddChild(_dispatch);

        Button back = ScreenChrome.Action("BACK", ScreenChrome.Bad, new Vector2(180, 52));
        back.Pressed += () => SceneRouter.Instance.CloseOverlay();
        buttons.AddChild(back);

        column.AddChild(buttons);

        _status = ScreenChrome.Text(string.Empty, 15, ScreenChrome.Cream);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(524, 0);
        column.AddChild(_status);
    }

    private void BuildFleetStrip()
    {
        PanelContainer strip = ScreenChrome.Sign(
            "EQUIPMENT", new Vector2(640, 0), LayoutPreset.CenterBottom, new Vector2(-320, -14));

        strip.GrowVertical = GrowDirection.Begin;
        AddChild(strip);

        _fleet = new HBoxContainer();
        _fleet.AddThemeConstantOverride("separation", 10);
        ScreenChrome.ContentOf(strip).AddChild(_fleet);
    }

    private void Refresh()
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        if (_topBar is not null && IsInstanceValid(_topBar))
            _topBar.QueueFree();

        _topBar = ScreenChrome.TopBar(Line(snapshot));
        AddChild(_topBar);

        foreach (Node child in _cards.GetChildren())
            child.QueueFree();

        for (int i = 0; i < Catalogue.Length; i++)
        {
            Order order = Catalogue[i];
            bool possible = order.Possible(snapshot);
            bool rigBusy = order.NeedsRig && snapshot.ActivitiesRunning > 0;
            int index = i;

            (string? stamp, Color stampColour) = Difficulty(order, snapshot);

            Button card = ScreenChrome.IconCard(
                order.Icon,
                order.Title,
                [order.Equipment, order.Reward],
                !possible ? "LOCKED" : rigBusy ? "RIG OUT" : "AVAILABLE",
                !possible ? ScreenChrome.Faded : rigBusy ? ScreenChrome.Gold : ScreenChrome.Good,
                stamp,
                stampColour,
                i == _selected,
                !possible || rigBusy,
                new Vector2(552, 84));

            card.Pressed += () =>
            {
                _selected = index;
                Refresh();
            };

            _cards.AddChild(card);
        }

        ShowDetail(snapshot);
        ShowFleet(snapshot);
    }

    private void ShowDetail(FieldReadModel snapshot)
    {
        foreach (Node child in _detail.GetChildren())
            child.QueueFree();

        Order order = Catalogue[_selected];

        _detail.AddChild(ScreenChrome.Text(order.Title.ToUpperInvariant(), 24, ScreenChrome.Ink));
        _detail.AddChild(Section("Objective", order.Objective));

        var equipment = new HBoxContainer();
        equipment.AddThemeConstantOverride("separation", 10);
        equipment.AddChild(ScreenChrome.Icon(order.EquipmentIcon, 46.0f));

        var lines = new VBoxContainer();
        lines.AddChild(ScreenChrome.Text("Required equipment", 14, new Color(0.45f, 0.40f, 0.34f)));
        lines.AddChild(ScreenChrome.Text(order.Equipment, 18, ScreenChrome.Ink));
        equipment.AddChild(lines);
        _detail.AddChild(equipment);

        _detail.AddChild(Section("Destination", Destination(order, snapshot)));
        _detail.AddChild(Section("Reward", order.Reward));
        _detail.AddChild(Section(
            "Time",
            order.NeedsRig
                ? "Months, and the rig is out for all of them."
                : "It runs alongside whatever else is happening."));

        bool rigBusy = order.NeedsRig && snapshot.ActivitiesRunning > 0;
        _dispatch.Disabled = !order.Possible(snapshot);
        _dispatch.Text = rigBusy ? "DISPATCH (RIG IS OUT)" : "DISPATCH";
    }

    private void ShowFleet(FieldReadModel snapshot)
    {
        foreach (Node child in _fleet.GetChildren())
            child.QueueFree();

        bool rigOut = snapshot.ActivitiesRunning > 0;
        bool hasWell = snapshot.Wells > 0;

        _fleet.AddChild(Chip("drilling-rig-derrick", "Rig", rigOut ? "out" : "ready", !rigOut));
        _fleet.AddChild(Chip("well-testing-skid", "Test skid", hasWell ? "ready" : "no well", hasWell));
        _fleet.AddChild(Chip("wireline-service-truck", "Wireline", hasWell ? "ready" : "no well", hasWell));
        _fleet.AddChild(Chip("mobile-crane-truck", "Crane", "ready", true));
        _fleet.AddChild(Chip("tanker-truck", "Tanker", "ready", true));
    }

    private static Control Chip(string icon, string name, string state, bool ready)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(112, 92) };
        panel.AddThemeStyleboxOverride("panel", ScreenChrome.PaperBox(ready ? ScreenChrome.Good : ScreenChrome.Faded));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(ScreenChrome.Icon(icon, 38.0f));
        column.AddChild(ScreenChrome.Text(name, 14, ScreenChrome.Ink, HorizontalAlignment.Center));
        column.AddChild(ScreenChrome.Text(
            ready ? "OK - " + state : state,
            13,
            ready ? ScreenChrome.Cash : new Color(0.5f, 0.45f, 0.4f),
            HorizontalAlignment.Center));

        panel.AddChild(column);

        return panel;
    }

    private static Control Section(string heading, string body)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(ScreenChrome.Text(heading, 14, new Color(0.45f, 0.40f, 0.34f)));

        Label text = ScreenChrome.Body(body, 17);
        text.CustomMinimumSize = new Vector2(492, 0);
        column.AddChild(text);

        return column;
    }

    /// <summary>
    /// The mockup's difficulty stamp, read off a real number.
    /// </summary>
    /// <remarks>
    /// For anything aimed at a structure it is that structure's probability of
    /// success — the one number the exploration game turns on. Work on the plant
    /// has nothing uncertain to stamp, so it carries none.
    /// </remarks>
    private static (string?, Color) Difficulty(Order order, FieldReadModel snapshot)
    {
        if (!order.Possible(snapshot) || snapshot.Prospects.Count == 0)
            return (null, ScreenChrome.Faded);

        if (!order.Title.Contains("seismic", StringComparison.Ordinal)
            && !order.Title.Contains("Drill", StringComparison.Ordinal))
        {
            return (null, ScreenChrome.Faded);
        }

        double pos = Best(snapshot).ProbabilityOfSuccess;

        return pos switch
        {
            >= 0.35 => ("EASY", ScreenChrome.Good),
            >= 0.20 => ("FAIR", ScreenChrome.Gold),
            _ => ("HARD", ScreenChrome.Bad),
        };
    }

    private static string Destination(Order order, FieldReadModel snapshot)
    {
        if (!order.Possible(snapshot))
            return "nothing to aim it at yet";

        if (order.Title.Contains("seismic", StringComparison.Ordinal)
            || order.Title.Contains("Drill", StringComparison.Ordinal))
        {
            ProspectView best = Best(snapshot);

            return $"{best.Play} at ({best.At.X / 1000.0:F0} km, {best.At.Y / 1000.0:F0} km), " +
                   $"{best.ToMarket.Metres / 1000.0:F0} km to market";
        }

        return order.NeedsRig ? "the well the company has drilled" : "the surface facilities";
    }

    private void Send(Order order)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        Command? command = order.Build(snapshot);

        if (command is null)
        {
            _status.Text = "There is nothing to aim that at yet.";
            return;
        }

        ProspectView? aimed = command is DrillWellCommand && snapshot.Prospects.Count > 0 ? Best(snapshot) : null;
        CommandResult result = EngineHost.Instance.Submit(command);

        if (result is Accepted)
        {
            if (aimed is not null)
                Gameplay.Current?.RecordDrill(aimed);

            _status.Text = "Ordered. It runs over the coming months.";
            Refresh();
            return;
        }

        if (result is Rejected rejected)
        {
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < rejected.Reasons.Count; i++)
                text.AppendLine(rejected.Reasons[i].Detail);

            _status.Text = text.ToString().TrimEnd();
        }
    }

    /// <summary>The top strip's four readings, shared by every board.</summary>
    internal static ScreenChrome.FieldSnapshotLine Line(FieldReadModel snapshot)
    {
        int left = Mathf.Max(0, 120 - snapshot.Tick.Value);

        return new ScreenChrome.FieldSnapshotLine(
            $"{snapshot.Date.Year}-{snapshot.Date.Month:00}  -  month {snapshot.Tick.Value}",
            $"${snapshot.Cash.Cents / 100.0 / 1_000_000.0:N1}M",
            $"{snapshot.Wells} wells  -  {snapshot.ProducedThisTick.CubicMetres:N0} m3  -  {snapshot.ActivitiesRunning} running",
            $"{left / 12}y {left % 12}m left");
    }

    private static ProspectView Best(FieldReadModel snapshot)
    {
        ProspectView best = snapshot.Prospects[0];

        for (int i = 1; i < snapshot.Prospects.Count; i++)
        {
            if (snapshot.Prospects[i].ProbabilityOfSuccess > best.ProbabilityOfSuccess)
                best = snapshot.Prospects[i];
        }

        return best;
    }

    private static EntityId<IReservoirCompartmentEntity>? Compartment(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Beliefs.Count; i++)
        {
            EntityRef subject = snapshot.Beliefs[i].Subject;

            if (subject.Kind == EntityKind.Compartment)
                return new EntityId<IReservoirCompartmentEntity>(subject.Value);
        }

        return null;
    }
}
