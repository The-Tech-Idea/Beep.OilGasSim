#nullable enable

using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.App;
using OilfieldDays.Host;
using OilfieldDays.Ui;
using OilfieldDays.World;

namespace OilfieldDays.Screens;

/// <summary>
/// The gameplay workspace (plan 08 §6.5): the basin, the truck, the HUD, and the
/// boards that open over them.
///
/// <para><b>What the game is.</b> The engine generates a basin and scatters
/// prospects through it, each with a probability of success it risked from five
/// petroleum-system factors. The player drives to one, decides whether it is
/// worth seismic or a hole, and finds oil or doesn't. A discovery becomes a well
/// that produces through the surface chain, month after month, and the money
/// lands in the company's ledger. The scenario asks for $600M inside ten years
/// and ends the run on insolvency.</para>
///
/// <para><b>What this class does not do.</b> It does not decide where anything
/// is, what anything costs, how much a well makes, or whether a command is
/// allowed. Every one of those is the engine's, and this is the host that shows
/// them and sends intents back (plan 09 §14).</para>
/// </summary>
public sealed partial class Gameplay : Node2D
{
    /// <summary>How far from a thing the truck counts as standing at it.</summary>
    private const float Reach = BasinWorld.TileSize * 3.2f;

    private const int DefaultBasinCells = 24;
    private const ulong DefaultSeed = 20260818UL;

    /// <summary>The running workspace, for the boards that open over it. Null in
    /// every other scene, which is what makes a board's use of it explicit.</summary>
    public static Gameplay? Current { get; private set; }

    /// <summary>How many kilometres across the basin is.</summary>
    public int BasinCells { get; private set; } = DefaultBasinCells;

    /// <summary>The world the boards draw their maps from.</summary>
    public BasinWorld World => _world;

    private BasinWorld _world = null!;
    private ServiceTruck _truck = null!;
    private GameHud _hud = null!;
    private CommandBar _bar = null!;

    private ProspectView? _prospect;
    private WellStatusView? _well;
    private bool _atPlant;
    private double _minimapClock;

    public override void _Ready()
    {
        Current = this;
        GameInput.Configure();

        // The basin was sized on the setup screen; the world is drawn to what
        // the engine actually generated, never to a number of its own.
        BasinCells = EngineHost.Instance.Snapshot is null
            ? DevOptions.Basin ?? DefaultBasinCells
            : EngineHost.Instance.BasinKilometres;

        _world = new BasinWorld { Name = "Basin" };
        AddChild(_world);
        _world.Build(BasinCells, EngineHost.Instance.Seed);

        _hud = new GameHud { Name = "Hud" };
        AddChild(_hud);

        _bar = new CommandBar { Name = "CommandBar" };
        AddChild(_bar);
        _bar.Reported += (message, bad) => _hud.Toast(message, bad);
        _bar.Offered += actions => _hud.BindHotbar(actions);

        _truck = new ServiceTruck
        {
            Name = "ServiceTruck",
            Position = _world.Extent * 0.5f,
            Bounds = _world.Extent,
        };

        AddChild(_truck);
        Camera2D camera = BuildCamera();

        if (DevOptions.Zoom is float zoom)
        {
            camera.Zoom = new Vector2(zoom, zoom);
            camera.PositionSmoothingEnabled = false;
        }

        _truck.AddChild(camera);

        EngineHost.Instance.SnapshotChanged += OnSnapshot;
        EngineHost.Instance.TickFaulted += OnFault;
        SimulationController.Instance.SpeedChanged += OnSpeedChanged;

        StartRun();

        if (DevOptions.At is Vector2 cell)
            _truck.Position = cell * BasinWorld.TileSize * BasinWorld.TilesPerKilometre;

        // A development switch can open a board straight away, so a screen can
        // be looked at without driving to the thing that opens it.
        switch (DevOptions.Screen)
        {
            case "dispatch":
                SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard);
                break;

            case "lease":
                SceneRouter.Instance.OpenOverlay(SceneRouter.LeaseBoard);
                break;

            case "fleet":
                SceneRouter.Instance.OpenOverlay(SceneRouter.FleetBoard);
                break;

            case "pause":
                SceneRouter.Instance.OpenOverlay(SceneRouter.PauseMenu);
                break;

            case "result":
                SceneRouter.Instance.Go(SceneRouter.Result);
                break;
        }

        DevScreenshot.ArmIfRequested(this);
    }

    public override void _ExitTree()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>Remember a hole the player ordered from a board, so the world can
    /// draw whatever it finds where it went.</summary>
    public void RecordDrill(ProspectView prospect) => _world.RecordDrill(prospect);

    public override void _Process(double delta)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        // A board covers the game; the HUD behind it would be two sets of
        // numbers on one screen, which is not what any mockup shows.
        bool overlay = SceneRouter.Instance.OverlayOpen;
        _hud.Visible = !overlay;
        _bar.Visible = !overlay;

        if (overlay)
            return;

        UpdateContext(snapshot);

        // The minimap follows the truck, so it is redrawn as it moves rather
        // than once a month.
        _minimapClock += delta;

        if (_minimapClock >= 0.2)
        {
            _minimapClock = 0.0;
            _hud.BindMinimap(_world, snapshot, _truck.GlobalPosition);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (SceneRouter.Instance.OverlayOpen)
            return;

        if (@event.IsActionPressed(GameInput.OpenDispatch))
        {
            SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInput.OpenLease))
        {
            SceneRouter.Instance.OpenOverlay(SceneRouter.LeaseBoard);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInput.OpenFleet))
        {
            SceneRouter.Instance.OpenOverlay(SceneRouter.FleetBoard);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInput.Cancel))
        {
            SceneRouter.Instance.OpenOverlay(SceneRouter.PauseMenu);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInput.AdvanceMonth))
        {
            SimulationController.Instance.StepOneMonth();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInput.TogglePause))
        {
            SimulationController.Instance.TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    private void StartRun()
    {
        if (EngineHost.Instance.Snapshot is null)
        {
            // Entered without going through the setup screen — a developer run,
            // or the scene opened straight from the editor. Build the default
            // basin rather than showing an empty world.
            ulong seed = DevOptions.Seed ?? DefaultSeed;

            if (!EngineHost.Instance.NewGame(seed, "arcade", DevOptions.Basin ?? BasinCells))
            {
                _hud.Toast("The engine refused to start — see the log for every reason.", true);
                return;
            }
        }

        OnSnapshot();
        _hud.Toast("A basin, and nothing drilled. Drive to a stake and decide: seismic, or a hole.", false);

        FastForward();
    }

    /// <summary>
    /// Play the opening the way a player would, when a development switch asks
    /// for it: drill the best prospects, then let the months run.
    /// </summary>
    /// <remarks>
    /// Every move goes through <see cref="EngineHost.Submit"/> and
    /// <see cref="SimulationController.StepOneMonth"/> — the same paths the
    /// buttons use — so a screenshot taken after it shows a field that was
    /// actually drilled and actually produced.
    /// </remarks>
    private void FastForward()
    {
        int wells = DevOptions.DrillBest;

        for (int i = 0; i < wells; i++)
        {
            FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

            if (snapshot is null || snapshot.Prospects.Count == 0)
                break;

            ProspectView best = snapshot.Prospects[0];

            for (int p = 1; p < snapshot.Prospects.Count; p++)
            {
                if (snapshot.Prospects[p].ProbabilityOfSuccess > best.ProbabilityOfSuccess)
                    best = snapshot.Prospects[p];
            }

            CommandResult result = EngineHost.Instance.Submit(
                new DrillWellCommand(new EntityId<IProspect>(best.Prospect.Value), new Length(2000.0)));

            if (result is Accepted)
            {
                _world.RecordDrill(best);
                GD.Print($"[dev] drilling {best.Play} at ({best.At.X:F0}, {best.At.Y:F0}), POS {best.ProbabilityOfSuccess:F2}");
            }
            else if (result is Rejected rejected && rejected.Reasons.Count > 0)
            {
                GD.Print($"[dev] drill refused: {rejected.Reasons[0].Detail}");
            }

            // A rig does one hole at a time, so give it the months to finish.
            for (int m = 0; m < 6; m++)
                SimulationController.Instance.StepOneMonth();
        }

        for (int m = 0; m < DevOptions.Months; m++)
            SimulationController.Instance.StepOneMonth();

        if (wells > 0 || DevOptions.Months > 0)
        {
            FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

            if (snapshot is not null)
            {
                GD.Print($"[dev] month {snapshot.Tick.Value}: cash ${snapshot.Cash.Cents / 100.0 / 1e6:F1}M, " +
                         $"{snapshot.Wells} wells, {snapshot.ProducedThisTick.CubicMetres:F0} m3, " +
                         $"{snapshot.Prospects.Count} prospects left, outcome {snapshot.Outcome}");

                for (int i = 0; i < snapshot.Wellbores.Count; i++)
                    GD.Print($"[dev] well {snapshot.Wellbores[i].DisplayId}: {snapshot.Wellbores[i].Status}");
            }
        }
    }

    /// <summary>
    /// Work out what the truck is standing at, and offer what can be done to it.
    /// </summary>
    /// <remarks>
    /// Nearest wins and only one thing is ever offered, which is plan 11's
    /// interaction rule. A prospect and a well never share a place: drilling the
    /// prospect is what removed it.
    /// </remarks>
    private void UpdateContext(FieldReadModel snapshot)
    {
        Vector2 at = _truck.GlobalPosition;
        bool plant = at.DistanceTo(_world.PlantSite) <= Reach * 1.6f;
        ProspectView? prospect = plant ? null : _world.ProspectNear(at, Reach, snapshot);
        WellStatusView? well = plant || prospect is not null ? null : _world.WellNear(at, Reach, snapshot);

        if (plant == _atPlant
            && prospect?.Prospect == _prospect?.Prospect
            && well?.Well == _well?.Well)
        {
            return;
        }

        _atPlant = plant;
        _prospect = prospect;
        _well = well;

        if (plant)
            _bar.ShowPlant(snapshot);
        else if (prospect is not null)
            _bar.ShowProspect(prospect, () => _world.RecordDrill(prospect));
        else if (well is not null)
            _bar.ShowWell(well, MeasurableCompartments(snapshot));
        else
            _bar.ShowNothing();
    }

    /// <summary>
    /// The compartments a downhole measurement can be aimed at.
    /// </summary>
    /// <remarks>
    /// Read off the beliefs the company holds, which is the only door a host has
    /// to a compartment id — the engine's own tests reach into <c>FieldControl</c>
    /// for it, and a host doing that would be reading engine internals. A company
    /// that has drilled nothing believes nothing about a compartment and cannot
    /// order a test, which is the rule the engine enforces anyway.
    /// </remarks>
    private static IReadOnlyList<EntityId<IReservoirCompartmentEntity>> MeasurableCompartments(FieldReadModel snapshot)
    {
        var found = new List<EntityId<IReservoirCompartmentEntity>>();
        var seen = new HashSet<ulong>();

        for (int i = 0; i < snapshot.Beliefs.Count; i++)
        {
            EntityRef subject = snapshot.Beliefs[i].Subject;

            if (subject.Kind != EntityKind.Compartment || !seen.Add(subject.Value))
                continue;

            found.Add(new EntityId<IReservoirCompartmentEntity>(subject.Value));
        }

        return found;
    }

    private void OnSnapshot()
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        _world.Bind(snapshot);
        _hud.Bind(snapshot);
        _hud.BindMinimap(_world, snapshot, _truck.GlobalPosition);

        // Make the action bar re-read: what a well or the plant offers changes
        // with the month even when the truck has not moved.
        _prospect = null;
        _well = null;
        _atPlant = false;

        if (snapshot.Insolvent || snapshot.Outcome != ObjectiveState.Pending)
        {
            SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);
            SceneRouter.Instance.Go(SceneRouter.Result);
        }
    }

    private void OnSpeedChanged(int speed) => _hud.BindSpeed((SimulationController.Speed)speed);

    private void OnFault(string detail, bool fatal)
    {
        _hud.Toast(fatal ? $"The engine stopped: {detail}" : $"A month was discarded: {detail}", true);

        if (fatal)
            SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);
    }

    private Camera2D BuildCamera() => new()
    {
        Name = "Camera",
        Zoom = new Vector2(0.9f, 0.9f),
        PositionSmoothingEnabled = true,
        PositionSmoothingSpeed = 6.0f,
        LimitLeft = 0,
        LimitTop = 0,
        LimitRight = (int)_world.Extent.X,
        LimitBottom = (int)_world.Extent.Y,
    };
}
