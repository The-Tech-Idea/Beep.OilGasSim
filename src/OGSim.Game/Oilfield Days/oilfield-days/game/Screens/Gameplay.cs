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
    private CameraRig _camera = null!;
    private World.Dispatcher _yard = null!;
    private StandingOrders _orders = null!;
    private int _reportedMonth = -1;
    private ProspectView? _pickedProspect;
    private WellStatusView? _pickedWell;
    private ChainElementView? _pickedElement;
    private Unit? _pickedUnit;
    private bool _pickedPlant;
    private GameHud _hud = null!;
    private StatusBar _status = null!;
    private IconRail _rail = null!;
    private SidePanels _side = null!;
    private SelectionCard _selection = null!;
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
        EngineHost.NewGameDraft draft = EngineHost.Instance.Draft;
        _world.Build(BasinCells, EngineHost.Instance.Seed, draft.LandFraction, draft.ClimateSeverity);

        _hud = new GameHud { Name = "Hud" };
        AddChild(_hud);

        // The shell of the gameplay mockups, over the yard's own HUD: the status
        // bar across the top and the rail down the left, both on the atlas's
        // plates. They frame the world; the wood HUD belongs to it.
        var shell = new CanvasLayer { Name = "Shell", Layer = 20 };
        AddChild(shell);

        _status = new StatusBar { Name = "StatusBar" };
        _status.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _status.OffsetLeft = 8;
        _status.OffsetRight = -8;
        _status.OffsetTop = 6;
        _status.OffsetBottom = 66;
        shell.AddChild(_status);

        _rail = new IconRail { Name = "Rail" };
        _rail.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _rail.OffsetLeft = 8;
        _rail.OffsetTop = 76;
        _rail.GrowHorizontal = Control.GrowDirection.End;
        _rail.GrowVertical = Control.GrowDirection.End;
        shell.AddChild(_rail);

        // Bottom-right, under the side column: the mockups put the selected
        // entity's card there, and it appears only while something is selected.
        _selection = new SelectionCard { Name = "Selection" };
        // Left of the side column, not under it: both are pinned to the right
        // edge, and the column is drawn second, so anything sharing its lane is
        // simply covered.
        _selection.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        _selection.OffsetLeft = -740;
        _selection.OffsetRight = -376;
        _selection.OffsetTop = -340;
        _selection.OffsetBottom = -8;
        shell.AddChild(_selection);

        _side = new SidePanels { Name = "Side" };
        _side.GoTo += LookAt;

        _orders = new StandingOrders { Name = "StandingOrders" };
        AddChild(_orders);
        _orders.Serve(_yard, _world, EngineHost.Instance.Drilled);

        _side.Orders = (which, on) =>
        {
            switch (which)
            {
                case 0: _orders.KeepRunning = on; break;
                case 1: _orders.AnswerJams = on; break;
                default: _orders.KeepRigBusy = on; break;
            }
        };
        _side.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        _side.OffsetLeft = -364;
        _side.OffsetRight = -8;
        _side.OffsetTop = 76;
        _side.OffsetBottom = -8;
        _side.GrowHorizontal = Control.GrowDirection.Begin;
        shell.AddChild(_side);

        _bar = new CommandBar { Name = "CommandBar" };
        AddChild(_bar);
        _bar.Reported += (message, bad) => _hud.Toast(message, bad);

        // Parked, not driven. Stage A takes the player out of the cab; Stage B
        // gives the truck a job to be sent on. Until then it stands at the yard
        // like the rest of the roster will.
        _truck = new ServiceTruck
        {
            Name = "ServiceTruck",
            Position = _world.PlantSite,
            Bounds = _world.Extent,
            ControlsEnabled = false,
        };

        AddChild(_truck);

        // The camera is its own rig now. It used to be a child of the truck,
        // which is precisely what made the player a truck: the only way to look
        // at anything was to drive to it.
        // The yard: the roster stands in it, and it is the only place a job
        // becomes an engine command (plans 17 §B2).
        _yard = new World.Dispatcher { Name = "Yard" };
        AddChild(_yard);
        _yard.Raise(_world.PlantSite);
        _yard.Reported += (message, bad) => _hud.Toast(message, bad);
        _yard.Unpack(EngineHost.Instance.RestoredYard);
        EngineHost.Instance.PackYard = _yard.Pack;
        _bar.Yard = _yard;

        _camera = new CameraRig { Name = "Camera" };
        AddChild(_camera);
        _camera.Frame(_world.Extent, _world.PlantSite);

        // --zoom was parsed and never read, so every dev screenshot came back at
        // the default step no matter what was asked for.
        if (DevOptions.Zoom is float pinned)
            _camera.Pin(pinned);

        EngineHost.Instance.SnapshotChanged += OnSnapshot;
        EngineHost.Instance.TickFaulted += OnFault;
        SimulationController.Instance.SpeedChanged += OnSpeedChanged;

        StartRun();

        if (DevOptions.At is Vector2 cell)
            _camera.GlobalPosition = cell * BasinWorld.TileSize * BasinWorld.TilesPerKilometre;

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
    public void RecordDrill(ProspectView prospect)
    {
        _world.RecordDrill(prospect);
        EngineHost.Instance.Drilled.Record(prospect);
    }

    public override void _Process(double delta)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        // A board covers the game; the HUD behind it would be two sets of
        // numbers on one screen, which is not what any mockup shows.
        bool overlay = SceneRouter.Instance.OverlayOpen;
        _hud.Visible = !overlay;
        _status.Visible = !overlay;
        _rail.Visible = !overlay;
        _side.Visible = !overlay;
        _selection.Visible = !overlay;
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
            _side.BindMinimap(_world, snapshot, _camera.GlobalPosition);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && !SceneRouter.Instance.OverlayOpen)
        {
            Pick(GetCanvasTransform().AffineInverse() * click.Position);
            GetViewport().SetInputAsHandled();

            return;
        }

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
        if (EngineHost.Instance.Snapshot is null && DevOptions.LoadNewest)
        {
            SaveSlots.Slot? newest = SaveSlots.Newest();

            if (newest is null)
                GD.Print("[dev] --load: nothing is saved");
            else if (!EngineHost.Instance.Load(newest))
                GD.Print($"[dev] --load refused: {string.Join(" | ", EngineHost.Instance.StartupProblems)}");
            else
                GD.Print($"[dev] loaded {newest.Name}");
        }

        if (EngineHost.Instance.Snapshot is null)
        {
            // Entered without going through the setup screen — a developer run,
            // or the scene opened straight from the editor. Build the default
            // basin rather than showing an empty world.
            ulong seed = DevOptions.Seed ?? DefaultSeed;

            EngineHost.NewGameDraft draft = EngineHost.NewGameDraft.Default(seed) with
            {
                RealityProfile = DevOptions.Profile ?? "arcade",
                Cells = DevOptions.Basin ?? BasinCells,
            };

            if (!EngineHost.Instance.NewGame(draft))
            {
                _hud.Toast("The engine refused to start — see the log for every reason.", true);
                return;
            }
        }

        OnSnapshot();
        _hud.Toast("A basin, and nothing drilled. Click a structure and decide: seismic, or a hole.", false);

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
        if (DevOptions.Play > 0)
        {
            DevAutoPlayer.Play(DevOptions.Play);

            return;
        }

        int wells = DevOptions.DrillBest;

        for (int i = 0; i < wells; i++)
        {
            FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

            if (snapshot is null || snapshot.Prospects.Count == 0)
                break;

            // The same picker the board uses, so a development run drills the
            // basin the way a player would rather than the same hole three times.
            ProspectView? pick = EngineHost.Instance.Drilled.BestUndrilled(snapshot);

            if (pick is not ProspectView best)
                break;

            CommandResult result = EngineHost.Instance.Submit(
                new DrillWellCommand(new EntityId<IProspect>(best.Prospect.Value), new Length(2000.0)));

            if (result is Accepted)
            {
                RecordDrill(best);
                GD.Print($"[dev] drilling {best.Play} #{best.Prospect.Value} at " +
                         $"({best.At.X:F0}, {best.At.Y:F0}), POS {best.ProbabilityOfSuccess:F2}" +
                         $" - {snapshot.Prospects.Count} prospects on the list");
            }
            else if (result is Rejected rejected && rejected.Reasons.Count > 0)
            {
                GD.Print($"[dev] drill refused: {rejected.Reasons[0].Detail}");
            }

            // A rig does one hole at a time, so give it the months to finish.
            for (int m = 0; m < 6; m++)
            {
                KeepThePlantRunning();
                SimulationController.Instance.StepOneMonth();
            }
        }

        for (int m = 0; m < DevOptions.Months; m++)
        {
            KeepThePlantRunning();
            SimulationController.Instance.StepOneMonth();
        }

        if (DevOptions.Save)
        {
            GD.Print(EngineHost.Instance.Save(out string problem)
                ? "[dev] saved"
                : $"[dev] save refused: {problem}");
        }

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

                // The chain, whole. A field with producing wells and no oil at
                // the far end has a jam in it, and the engine already says
                // where: the row that refused is the row with deferred mass.
                for (int i = 0; i < snapshot.Chain.Count; i++)
                {
                    ChainElementView element = snapshot.Chain[i];
                    string deferred = string.Empty;

                    for (int d = 0; d < element.Deferred.Count; d++)
                        deferred += $"  [{element.Deferred[d].Kind} {element.Deferred[d].Deferred.Kilograms:F0} kg]";

                    GD.Print($"[dev] chain {element.DisplayId}: {element.Throughput.Kilograms:F0} kg" +
                             (element.Failed ? " FAILED" : string.Empty) + deferred);
                }
            }
        }
    }

    /// <summary>
    /// The one standing order a development run gives: repair whatever has
    /// stopped.
    /// </summary>
    /// <remarks>
    /// Not a policy the game holds — a player decides this — but a harness that
    /// never repairs measures a field that has been abandoned rather than one
    /// that is being run, and every long run would report zero for reasons that
    /// have nothing to do with what is being tested.
    /// </remarks>
    private static void KeepThePlantRunning()
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (!snapshot.Chain[i].Failed)
                continue;

            EngineHost.Instance.Submit(new RepairEquipmentCommand(snapshot.Chain[i].Element));
            return;
        }
    }

    /// <summary>
    /// Report the month that just ended.
    /// </summary>
    /// <remarks>
    /// A month that passes silently is the current experience and the reason the
    /// mid-game reads as a wait. Every figure here is published; nothing is
    /// summed by the host beyond counting how many elements the engine flagged.
    /// </remarks>
    private void CloseTheMonth(FieldReadModel snapshot)
    {
        if (snapshot.Tick.Value == _reportedMonth)
            return;

        _reportedMonth = snapshot.Tick.Value;

        // Not the opening tick: a report of the month before the game started
        // would be a report of nothing.
        if (snapshot.Tick.Value <= 1)
            return;

        int stopped = 0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].Failed)
                stopped++;
        }

        if (stopped > 0)
        {
            _hud.Toast(
                $"{stopped} element{(stopped == 1 ? "" : "s")} out of service — the chain is shut in behind it.",
                true);

            return;
        }

        if (snapshot.ProducedThisTick.CubicMetres <= 0.0)
            return;

        _hud.Toast(
            $"{snapshot.ProducedThisTick.CubicMetres:N0} m3 this month at ${snapshot.OilPrice.Cents / 100.0:N0}/t.",
            false);
    }

    /// <summary>
    /// Take the view to a chain element and select it.
    /// </summary>
    /// <remarks>
    /// The other half of the alert path: the panel says what failed, this puts
    /// it on screen with its repair already offered. Two clicks from a warning
    /// to a crew on its way, which is the whole point of an alert list.
    /// </remarks>
    private void LookAt(ulong element)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null || _world.WhereIs(element) is not Vector2 at)
            return;

        _camera.Release();
        _camera.GlobalPosition = at;

        _pickedUnit = null;
        _pickedProspect = null;
        _pickedWell = null;
        _pickedPlant = false;
        _pickedElement = _world.ElementNear(at, Reach);

        UpdateContext(snapshot);
    }

    /// <summary>The unit under a point, if one is standing there.</summary>
    private Unit? UnitNear(Vector2 at)
    {
        Unit? nearest = null;
        float best = Reach * 0.7f;

        foreach (Node node in GetTree().GetNodesInGroup("units"))
        {
            if (node is not Unit unit)
                continue;

            float distance = unit.GlobalPosition.DistanceTo(at);

            if (distance > best)
                continue;

            nearest = unit;
            best = distance;
        }

        return nearest;
    }

    /// <summary>
    /// A unit: what it is doing, and the one thing a player can still change.
    /// </summary>
    /// <remarks>
    /// Recall is offered only while it is TRAVELLING, because nothing has been
    /// submitted yet. Once it has arrived the activity is the engine's and the
    /// host cannot take it back — offering a recall there would be offering to
    /// undo something the client does not own.
    /// </remarks>
    private void ShowUnit(Unit unit)
    {
        _bar.ShowUnit(unit, () => _selection.ShowNothing());
        _selection.ShowUnit(unit);
    }

    /// <summary>
    /// Select whatever was clicked, and offer what can be done to it.
    /// </summary>
    /// <remarks>
    /// Nearest wins within a reach, and one thing is selected at a time — a
    /// structure and a well never share a place, because drilling the structure
    /// is what removed it. Clicking open ground clears the selection, which is
    /// how a player puts the action panel away.
    /// </remarks>
    private void Pick(Vector2 at)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;

        if (snapshot is null)
            return;

        // A yard building answers first. The plant site sits in the middle of the
        // yard, so without this the office and the workshop would both be "the
        // plant" and clicking a base building would open nothing.
        if (_world.DoorNear(at, Reach * 0.9f) is string opens)
        {
            SceneRouter.Instance.OpenOverlay(opens);

            return;
        }

        // Most specific first. A unit stands on a pad and a chain element stands
        // in the yard, so testing the plant before either would hand every click
        // in the base to "the plant" and nothing else would ever be selectable.
        _pickedUnit = UnitNear(at);
        _pickedElement = _pickedUnit is null ? _world.ElementNear(at, Reach * 0.8f) : null;

        _pickedProspect = _pickedUnit is null && _pickedElement is null
            ? _world.ProspectNear(at, Reach, snapshot)
            : null;

        _pickedWell = _pickedUnit is null && _pickedElement is null && _pickedProspect is null
            ? _world.WellNear(at, Reach, snapshot)
            : null;

        _pickedPlant = _pickedUnit is null
            && _pickedElement is null
            && _pickedProspect is null
            && _pickedWell is null
            && at.DistanceTo(_world.PlantSite) <= Reach * 1.6f;

        // LAST, because a block covers everything. A licence block is the whole
        // ground rather than a thing standing on it, so testing it any earlier
        // would answer every click in the basin with "the acreage" and nothing
        // on the map would ever be selectable again.
        _pickedBlock = _pickedUnit is null
            && _pickedElement is null
            && _pickedProspect is null
            && _pickedWell is null
            && !_pickedPlant
            ? _world.BlockAt(at)
            : null;

        UpdateContext(snapshot);
    }

    /// <summary>
    /// Offer what can be done to whatever is selected.
    /// </summary>
    /// <remarks>
    /// Nearest wins and only one thing is ever offered, which is plan 11's
    /// interaction rule. A prospect and a well never share a place: drilling the
    /// prospect is what removed it.
    /// </remarks>
    private void UpdateContext(FieldReadModel snapshot)
    {
        // What the player CHOSE, not what a truck happens to be standing beside.
        // Proximity was the input method when the player was a vehicle; a
        // director points at things.
        bool plant = _pickedPlant;
        ProspectView? prospect = _pickedProspect;
        WellStatusView? well = _pickedWell;

        // A unit or an element short-circuits the cache below: both change what
        // is offered without changing any of the three fields it compares.
        if (_pickedUnit is not null)
        {
            ShowUnit(_pickedUnit);

            return;
        }

        if (_pickedElement is ChainElementView element)
        {
            _bar.SiteAt = _world.WhereIs(element.Element.Value) ?? _world.PlantSite;
            _bar.ShowElement(element);
            _selection.ShowElement(element);
            _atPlant = false;
            _prospect = null;
            _well = null;

            return;
        }

        BlockView? block = _pickedBlock;

        if (plant == _atPlant
            && prospect?.Prospect == _prospect?.Prospect
            && well?.Well == _well?.Well
            && block?.Block == _block?.Block
            && block?.Surveyed == _block?.Surveyed)
        {
            return;
        }

        _block = block;

        _atPlant = plant;
        _prospect = prospect;
        _well = well;

        _bar.SiteAt = plant ? _world.PlantSite
            : prospect is not null ? BasinWorld.ToWorld(prospect.At)
            : well is not null ? _world.SiteOf(well.Well.Value) ?? _world.PlantSite
            : _world.PlantSite;

        if (plant)
        {
            _bar.ShowPlant(snapshot);
            _selection.ShowPlant(snapshot);
        }
        else if (prospect is not null)
        {
            _bar.ShowProspect(prospect, () => _world.RecordDrill(prospect));
            _selection.ShowProspect(prospect);
        }
        else if (well is not null)
        {
            _bar.ShowWell(well, MeasurableCompartments(snapshot));
            _selection.ShowWell(well);
        }
        else if (block is not null)
        {
            // The crew is sent to the middle of the block, because a block is
            // ground rather than a point and its centre is the only place on it
            // that means anything without picking one.
            _bar.SiteAt = BasinWorld.ToWorld(block.Centre);
            _bar.ShowBlock(block);
            _selection.ShowNothing();
        }
        else
        {
            _bar.ShowNothing();
            _selection.ShowNothing();
        }
    }

    private BlockView? _pickedBlock;
    private BlockView? _block;

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
        _status.Bind(snapshot);
        _yard.Bind(snapshot);
        _world.ShowRising(_yard.Rising);
        _orders.Consider(snapshot);
        CloseTheMonth(snapshot);
        _side.Bind(snapshot);
        _side.BindMinimap(_world, snapshot, _camera.GlobalPosition);

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

    // The speed reads on the status bar now, beside the buttons that set it.
    private void OnSpeedChanged(int speed) => _status.BindSpeed((SimulationController.Speed)speed);

    private void OnFault(string detail, bool fatal)
    {
        _hud.Toast(fatal ? $"The engine stopped: {detail}" : $"A month was discarded: {detail}", true);

        if (fatal)
            SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);
    }

}
