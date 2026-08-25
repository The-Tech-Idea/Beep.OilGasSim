#nullable enable

using System;
using System.Collections.Generic;
using Beep.ECS.UI;
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
[Tool]
public sealed partial class CommandBar : CanvasLayer
{
    /// <summary>The depth a well is drilled to, matching the reference client.</summary>
    private static readonly Length WellDepth = new(2000.0);

    private readonly List<Action> _actions = new();
    private readonly List<string> _labels = new();

    private PanelContainer _panel = null!;
    private Label _target = null!;
    private VBoxContainer _list = null!;
    private Button _actionTemplate = null!;

    public event Action<string, bool>? Reported;

    /// <summary>The actions now on offer, in order — what the hotbar shows.</summary>
    public event Action<string[]>? Offered;

    public override void _Ready()
    {
        Layer = 11;

        Control root = RequireNamed<Control>(this, "CommandRoot");
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;

        _panel = RequireNamed<PanelContainer>(root, "ActionPanel");

        _panel.CustomMinimumSize = new Vector2(400, 0);
        _panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _panel.Position = new Vector2(122, -128);
        _panel.GrowVertical = Control.GrowDirection.Begin;
        StyleHeader(_panel);

        VBoxContainer column = RequireNamed<VBoxContainer>(_panel, "Content");
        column.CustomMinimumSize = new Vector2(364, 0);
        column.AddThemeConstantOverride("separation", 10);

        _target = RequireNamed<Label>(column, "Target");
        ConfigureTarget();

        _list = RequireNamed<VBoxContainer>(column, "ActionList");
        ConfigureList();

        _actionTemplate = RequireNamed<Button>(_list, "ActionButtonTemplate");
        StyleAction(_actionTemplate, _actionTemplate.Text, KitTheme.Amber);
        _actionTemplate.Visible = Godot.Engine.IsEditorHint();

        if (!Godot.Engine.IsEditorHint())
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
    /// <summary>
    /// Who carries a job out to the field, if anybody does.
    /// </summary>
    /// <remarks>
    /// Set by the gameplay screen once the yard is raised. When it is present the
    /// action sends a UNIT and the command is submitted on arrival (plans 17);
    /// when it is null — a board opened with no world behind it — the command
    /// goes straight to the engine, which is the behaviour every board already
    /// had.
    /// </remarks>
    public World.Dispatcher? Yard { get; set; }

    /// <summary>Where the selected thing stands, for a unit to drive to.</summary>
    public Vector2 SiteAt { get; set; }

    /// <summary>The line break panel text is built from.</summary>
    private static readonly string NewLine = ((char)10).ToString();

    /// <summary>Send a unit if there is a yard, or the command if there is not.</summary>
    private void Dispatch(World.JobKind job, ulong subject, Command command)
    {
        if (Yard is null)
        {
            Send(command);

            return;
        }

        Yard.Send(job, SiteAt, subject);
    }

    public void ShowProspect(ProspectView prospect, Action afterDrill)
    {
        Reset($"{prospect.Play} site\nChance {prospect.ProbabilityOfSuccess * 100:F0}%\n" +
              $"{prospect.ToMarket.Metres / 1000.0:F0} km from the export road");

        var target = new EntityId<IProspect>(prospect.Prospect.Value);

        Add("Scout the ground", KitTheme.Muted, () =>
            Dispatch(World.JobKind.Survey, prospect.Prospect.Value, new SeismicSurveyCommand(target)));
        Add("Send the drill crew", KitTheme.Amber, () =>
        {
            if (Yard is not null)
            {
                // The rig is going. Whether the engine takes the work is decided
                // when it gets there, so the world records the hole then, not now.
                Yard.Send(World.JobKind.Drill, SiteAt, prospect.Prospect.Value);

                return;
            }

            if (Send(new DrillWellCommand(target, WellDepth)))
                afterDrill();
        });
    }

    /// <summary>Offer what can be done to a well, and to whatever it produces into.</summary>
    /// <summary>
    /// A block of the licence: what is known about it, and the one thing that
    /// can be done to it.
    /// </summary>
    /// <remarks>
    /// A shot block offers NOTHING, and that absence is the point — the money
    /// has been spent and the answer is on the map. Offering a second pass would
    /// be selling the same acreage twice, which the engine refuses anyway; the
    /// bar refusing to ask is better than the engine refusing to answer.
    /// </remarks>
    public void ShowBlock(BlockView block)
    {
        ArgumentNullException.ThrowIfNull(block);

        string state = block.Surveyed
            ? block.Structures > 0
                ? $"{block.Structures} promising spot{(block.Structures == 1 ? string.Empty : "s")} found here"
                : "scouted - nothing useful found"
            : "unscouted ground";

        Reset($"Wild ground {block.Block.Value:00}\n{state}");

        if (block.Surveyed)
            return;

        Add("Send scouts", KitTheme.Amber, () =>
            Dispatch(World.JobKind.SurveyBlock, block.Block.Value,
                     new SurveyBlockCommand(new EntityId<IBlock>(block.Block.Value))));
    }

    public void ShowWell(WellStatusView well, IReadOnlyList<EntityId<IReservoirCompartmentEntity>> compartments)
    {
        Reset($"{well.DisplayId}\n{well.Status} · {well.ProducedThisTick.CubicMetres:N0} oil this month");

        var completion = new EntityId<ICompletion>(well.Well.Value);
        bool shut = well.Status == WellStatus.ShutIn;

        Add(shut ? "Open the well" : "Shut the well in", KitTheme.Green,
            () => Send(new SetWellChokeCommand(completion, !shut)));

        for (int i = 0; i < compartments.Count && i < 1; i++)
        {
            EntityId<IReservoirCompartmentEntity> compartment = compartments[i];

            Add("Check the flow", KitTheme.Muted, () =>
                Dispatch(World.JobKind.WellTest, well.Well.Value, new WellTestCommand(compartment)));

            Add("Map the rock", KitTheme.Muted, () =>
                Dispatch(World.JobKind.WirelineLog, well.Well.Value, new WirelineLogCommand(compartment)));

            Add("Take a sample", KitTheme.Muted, () =>
                Dispatch(World.JobKind.CutCore, well.Well.Value, new CutCoreCommand(compartment)));
        }

        Add("Close this well", KitTheme.Red, () => Send(new AbandonWellCommand(completion)));
    }

    /// <summary>Offer what can be done to the surface plant.</summary>
    /// <summary>
    /// One piece of the chain: what it is doing, and what a crew can do to it.
    /// </summary>
    /// <remarks>
    /// Condition is shown only where the engine publishes it. A null condition
    /// is UNMEASURED — the company has not fitted a monitoring kit — and printing
    /// "as new" for it would report truth nobody bought, which is the door the
    /// whole belief system exists to keep shut.
    /// </remarks>
    public void ShowElement(ChainElementView element)
    {
        string wear = element.Condition is double condition
            ? $"health {condition * 100.0:F0}%"
            : "needs an inspection post";

        Reset(
            $"{element.DisplayId}\nMoved {element.Throughput.Kilograms / 1000.0:N0} loads this month · {wear}"
            + (element.Failed
                ? "\nSTOPPED - send a crew to get the camp moving again"
                : string.Empty));

        ulong id = element.Element.Value;

        if (element.Failed)
        {
            Add("Repair it", KitTheme.Red, () =>
                Dispatch(World.JobKind.Repair, id, new RepairEquipmentCommand(element.Element)));

            return;
        }

        // Planned work only while it still runs, and monitoring only while there
		// is nothing to read: both are the engine's own rules, and offering the
		// other one would be offering a refusal.
		if (element.Condition is not null)
		{
			Add("Overhaul it", KitTheme.Green, () =>
                Dispatch(World.JobKind.Service, id, new ServiceEquipmentCommand(element.Element)));
		}
		else
		{
			Add("Build an inspection post", KitTheme.Sky, () =>
				Dispatch(World.JobKind.FitMonitoring, id, new InstallMonitoringCommand(element.Element)));
		}
	}

	/// <summary>A unit, and the one thing a player can still change about it.</summary>
	public void ShowUnit(World.Unit unit, System.Action afterRecall)
	{
		string doing = unit.State switch
		{
			World.UnitState.Idle => "in the yard",
			World.UnitState.Travelling => "on its way out",
			World.UnitState.Preparing => "clearing and preparing the site",
			World.UnitState.Working => $"working since month {unit.StartedOn}",
			_ => "on its way home",
		};

		Reset($"{unit.Kind.DisplayName}\n{doing}");

		// Only while travelling: nothing has been submitted yet, so there is
		// something to call back. After arrival the work is the engine's.
        if (unit.State != World.UnitState.Travelling)
            return;

        Add("Call it back", KitTheme.Red, () =>
        {
            if (unit.Recall())
                afterRecall();
        });
    }

    /// <summary>
    /// The plant: what the chain is, and what a construction crew can add to it.
    /// </summary>
    /// <remarks>
    /// <b>No placement is offered.</b> OGSim has no coordinate for a facility
    /// (gap G-02), so every separator is the same separator wherever it is drawn
    /// — the host chooses a bay and says so. A tile grid and a placement ghost
    /// would imply a choice the engine cannot honour.
    /// </remarks>
    public void ShowPlant(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // BARE GROUND OFFERS ONE THING (plans 22 4, plans 23). A company that
        // has built nothing has no vessels to enlarge and no bottleneck to
        // answer, so a catalogue of upgrades would be a list of buttons that all
        // refuse. What it can do is commission a facility, and that is the whole
        // panel until it has.
        if (snapshot.Chain.Count == 0)
        {
            Reset("Empty camp" + NewLine +
                  "Nothing is built here yet. Start the camp so wells have a place to send oil.");

            Add("Build the first camp", KitTheme.Amber, () =>
                Dispatch(World.JobKind.Commission, 0UL,
                         new InstallEarlyProductionFacilityCommand()));

            return;
        }

        var text = new System.Text.StringBuilder("Camp works\n");

        for (int i = 0; i < snapshot.Chain.Count; i++)
            text.Append(snapshot.Chain[i].DisplayId).Append(i == snapshot.Chain.Count - 1 ? "" : " → ");

        if (Yard is not null && Yard.Rising.Count > 0)
        {
            text.Append("\nBeing built: ");

            for (int i = 0; i < Yard.Rising.Count; i++)
                text.Append(i == 0 ? "" : ", ").Append(Yard.Rising[i]);
        }

        Reset(text.ToString());

        if (Yard is null)
        {
            Add("Add a processing shed", KitTheme.Green, () => Send(new InstallSeparatorCommand()));
            Add("Improve the export road", KitTheme.Green, () => Send(new ExpandExportCommand()));

            return;
        }

        for (int i = 0; i < Yard.Catalogue.Count; i++)
        {
            World.BuildKind kind = Yard.Catalogue[i];
            ulong at = (ulong)i;

            Add($"Build {kind.DisplayName}", KitTheme.Green, () =>
                Yard.Send(World.JobKind.Build, SiteAt, at));
        }
    }

    /// <summary>Nothing is in reach.</summary>
    public void ShowNothing() =>
        Reset(
            "Click a site, well, camp building, or crew to choose what happens next.\n\n"
            + "W A S D or the screen edge moves the view · wheel zooms · "
            + "Space advances a month · P pauses");

    private void Reset(string target)
    {
        _target.Text = target;
        _actions.Clear();
        _labels.Clear();
        Offered?.Invoke([]);

        foreach (Node child in _list.GetChildren())
        {
            if (child == _actionTemplate)
                continue;

            _list.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void Add(string label, Color colour, Action action)
    {
        Button button = (Button)_actionTemplate.Duplicate();
        button.Name = "ActionButton";
        button.Visible = true;
        StyleAction(button, $"{_actions.Count + 1}.  {label}", colour);
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
            Reported?.Invoke($"{Describe(command)} ordered.", false);
            return true;
        }

        if (result is Rejected rejected)
        {
            for (int i = 0; i < rejected.Reasons.Count; i++)
                Reported?.Invoke($"{Describe(command)} refused — {rejected.Reasons[i].Detail}", true);
        }

        return false;
    }

    private static string Describe(Command command) => command switch
    {
        DrillWellCommand => "A well",
        SeismicSurveyCommand => "Scouts",
        WellTestCommand => "A flow check",
        WirelineLogCommand => "Rock mapping",
        CutCoreCommand => "A sample",
        SetWellChokeCommand choke => choke.Open ? "Opening the well" : "Shutting the well in",
        AbandonWellCommand => "Abandonment",
        InstallSeparatorCommand => "A processing shed",
        ExpandExportCommand => "A road upgrade",
        _ => command.GetType().Name,
    };

    private void ConfigureTarget()
    {
        _target.CustomMinimumSize = new Vector2(364, 0);
        _target.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _target.AddThemeFontSizeOverride("font_size", 16);
        _target.AddThemeColorOverride("font_color", KitTheme.Ink);
    }

    private void ConfigureList()
    {
        _list.CustomMinimumSize = new Vector2(364, 0);
        _list.AddThemeConstantOverride("separation", 6);
    }

    private static void StyleAction(Button button, string text, Color colour)
    {
        SlateChrome.ApplyChunk(button, text, RoleOf(colour), new Vector2(364, 42), fontSize: 16);
    }

    private static UiSurface.Role RoleOf(Color colour) =>
        colour == KitTheme.Green ? UiSurface.Role.Success
        : colour == KitTheme.Red ? UiSurface.Role.Danger
        : colour == KitTheme.Amber ? UiSurface.Role.Warning
        : colour == KitTheme.Sky ? UiSurface.Role.Info
        : UiSurface.Role.Neutral;

    private static void StyleHeader(Node panel)
    {
        if (FindNamed<Label>(panel, "Header") is { } header)
        {
            header.Text = "ACTIONS";
            SlateChrome.PromoteHeader(header, UiSurface.Role.Warning);
        }

        if (FindNamed<ColorRect>(panel, "Rule") is { } rule)
            rule.Visible = false;
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
			$"{nameof(CommandBar)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
