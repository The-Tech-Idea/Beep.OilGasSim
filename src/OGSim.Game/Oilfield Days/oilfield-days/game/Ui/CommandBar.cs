#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Ui;

/// <summary>
/// The action bar of plan 08 §6.5, and the only place a command is built.
///
/// <para>Its contents are decided by what the truck is standing next to, which
/// is plan 08 §8's placement rule: drill and survey belong to the map, choke,
/// test, log, core and abandon belong to a well, and install/expand belong to
/// the plant. There is no menu of things the engine cannot do.</para>
///
/// <para>What it does not have is a shop. The nine commands in
/// <c>OGSim.Composition</c> are the whole player vocabulary; anything else the
/// game appeared to offer would be a promise the engine never made.</para>
/// </summary>
public sealed partial class CommandBar : CanvasLayer
{
    /// <summary>The depth a well is drilled to, matching the reference client.</summary>
    private static readonly Length WellDepth = new(2000.0);

    private readonly List<Action> _actions = new();
    private readonly List<string> _labels = new();

    private PanelContainer _panel = null!;
    private Label _target = null!;
    private VBoxContainer _list = null!;

    public event Action<string, bool>? Reported;

    /// <summary>The actions now on offer, in order — what the hotbar shows.</summary>
    public event Action<string[]>? Offered;

    public override void _Ready()
    {
        Layer = 11;

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _panel = ScreenChrome.Sign(
            "ACTIONS", new Vector2(400, 0), Control.LayoutPreset.BottomLeft, new Vector2(18, -122));

        _panel.GrowVertical = Control.GrowDirection.Begin;
        root.AddChild(_panel);

        VBoxContainer column = ScreenChrome.ContentOf(_panel);
        column.CustomMinimumSize = new Vector2(364, 0);
        column.AddThemeConstantOverride("separation", 10);

        _target = new Label
        {
            CustomMinimumSize = new Vector2(364, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        _target.AddThemeFontSizeOverride("font_size", 16);
        _target.AddThemeColorOverride("font_color", ScreenChrome.Cream);
        column.AddChild(_target);

        _list = new VBoxContainer { CustomMinimumSize = new Vector2(364, 0) };
        _list.AddThemeConstantOverride("separation", 6);
        column.AddChild(_list);

        ShowNothing();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        for (int i = 0; i < _actions.Count && i < 9; i++)
        {
            if (@event.IsActionPressed($"od_action_{i + 1}"))
            {
                _actions[i]();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    /// <summary>Offer what can be done to a prospect.</summary>
    public void ShowProspect(ProspectView prospect, Action afterDrill)
    {
        Reset($"{prospect.Play} prospect\nPOS {prospect.ProbabilityOfSuccess * 100:F0}% · " +
              $"source {prospect.Source:F2} reservoir {prospect.Reservoir:F2} seal {prospect.Seal:F2} " +
              $"trap {prospect.Trap:F2} timing {prospect.Timing:F2}\n" +
              $"{prospect.ToMarket.Metres / 1000.0:F0} km to market");

        var target = new EntityId<IProspect>(prospect.Prospect.Value);

        Add("Shoot seismic", ScreenChrome.Wood, () => Send(new SeismicSurveyCommand(target)));
        Add("Drill a well (2,000 m)", ScreenChrome.Gold, () =>
        {
            if (Send(new DrillWellCommand(target, WellDepth)))
                afterDrill();
        });
    }

    /// <summary>Offer what can be done to a well, and to whatever it produces into.</summary>
    public void ShowWell(WellStatusView well, IReadOnlyList<EntityId<IReservoirCompartmentEntity>> compartments)
    {
        Reset($"{well.DisplayId}\n{well.Status} · {well.ProducedThisTick.CubicMetres:N0} m³ this month");

        var completion = new EntityId<ICompletion>(well.Well.Value);
        bool shut = well.Status == WellStatus.ShutIn;

        Add(shut ? "Open the well" : "Shut the well in", ScreenChrome.Cash,
            () => Send(new SetWellChokeCommand(completion, !shut)));

        for (int i = 0; i < compartments.Count && i < 1; i++)
        {
            EntityId<IReservoirCompartmentEntity> compartment = compartments[i];

            Add("Run a well test", ScreenChrome.Wood, () => Send(new WellTestCommand(compartment)));
            Add("Run a wireline log", ScreenChrome.Wood, () => Send(new WirelineLogCommand(compartment)));
            Add("Cut a core", ScreenChrome.Wood, () => Send(new CutCoreCommand(compartment)));
        }

        Add("Abandon the well", ScreenChrome.Bad, () => Send(new AbandonWellCommand(completion)));
    }

    /// <summary>Offer what can be done to the surface plant.</summary>
    public void ShowPlant(FieldReadModel snapshot)
    {
        var text = new System.Text.StringBuilder("Surface facilities\n");

        for (int i = 0; i < snapshot.Chain.Count; i++)
            text.Append(snapshot.Chain[i].DisplayId).Append(i == snapshot.Chain.Count - 1 ? "" : " → ");

        Reset(text.ToString());

        Add("Install another separator", ScreenChrome.Cash, () => Send(new InstallSeparatorCommand()));
        Add("Expand export capacity", ScreenChrome.Cash, () => Send(new ExpandExportCommand()));
    }

    /// <summary>Nothing is in reach.</summary>
    public void ShowNothing() =>
        Reset("Drive to a prospect, a well, or the plant.\n\nW A S D to drive · Space advances a month · P pauses");

    private void Reset(string target)
    {
        _target.Text = target;
        _actions.Clear();
        _labels.Clear();
        Offered?.Invoke([]);

        foreach (Node child in _list.GetChildren())
            child.QueueFree();
    }

    private void Add(string label, Color colour, Action action)
    {
        Button button = ScreenChrome.Action($"{_actions.Count + 1}.  {label}", colour, new Vector2(364, 42), fontSize: 16);
        button.Pressed += () => action();
        _labels.Add(label);
        _list.AddChild(button);
        _actions.Add(action);
        Offered?.Invoke(_labels.ToArray());
    }

    /// <summary>Submit, and say what came back. Both outcomes are player feedback.</summary>
    private bool Send(Command command)
    {
        CommandResult result = EngineHost.Instance.Submit(command);

        if (result is Accepted)
        {
            Reported?.Invoke($"{Name(command)} ordered.", false);
            return true;
        }

        if (result is Rejected rejected)
        {
            for (int i = 0; i < rejected.Reasons.Count; i++)
                Reported?.Invoke($"{Name(command)} refused — {rejected.Reasons[i].Detail}", true);
        }

        return false;
    }

    private static string Name(Command command) => command switch
    {
        DrillWellCommand => "A well",
        SeismicSurveyCommand => "A seismic survey",
        WellTestCommand => "A well test",
        WirelineLogCommand => "A wireline log",
        CutCoreCommand => "A core",
        SetWellChokeCommand choke => choke.Open ? "Opening the well" : "Shutting the well in",
        AbandonWellCommand => "Abandonment",
        InstallSeparatorCommand => "A separator",
        ExpandExportCommand => "An export expansion",
        _ => command.GetType().Name,
    };
}
