#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OilfieldDays.Host;

namespace OilfieldDays.World;

/// <summary>
/// The yard's roster, and the one place a job becomes an engine command.
///
/// <para><b>The rule this whole class exists to hold:</b> a command is submitted
/// when a unit ARRIVES, never when a player presses a button (plans 17 §B2).
/// Travel therefore decides <em>when</em> a command is submitted and nothing
/// else — plans 15 §2b — which is what keeps the client pacing input rather than
/// simulating.</para>
///
/// <para>Units raise <see cref="Unit.Arrived"/> and this listens; the units
/// themselves know nothing about commands (plans 21 §P5, §P8).</para>
/// </summary>
public sealed partial class Dispatcher : Node2D
{
    private const string Folder = "res://data/units";
    private const string Builds = "res://data/builds";
    private const float BasePrepareSeconds = 2.2f;

    private readonly List<UnitKind> _kinds = new();
    private readonly List<BuildKind> _catalogue = new();
    private readonly List<Unit> _roster = new();

    /// <summary>A build the engine has taken, and how to tell when it lands.</summary>
    private readonly List<(BuildKind Kind, int Was)> _rising = new();

    private Vector2 _yard;

    /// <summary>Something happened a player should be told about.</summary>
    [Signal]
    public delegate void ReportedEventHandler(string message, bool bad);

    /// <summary>A unit has arrived and is preparing the ground before the engine job starts.</summary>
    public event Action<JobKind, ulong, Vector2>? PreparingSite;

    /// <summary>The engine accepted a job after a unit prepared its site.</summary>
    public event Action<JobKind, ulong>? JobAccepted;

    /// <summary>Raise the yard: load the kinds and stand one of each in it.</summary>
    public void Raise(Vector2 yard)
    {
        _yard = yard;
        LoadKinds();
        LoadBuilds();

        foreach (UnitKind kind in _kinds)
        {
            // Vehicles drive and crews walk; which one a kind is comes from how
            // it moves, and the only thing that differs per kind is the resource.
            Unit unit = kind.Speed >= 700.0f
                ? new VehicleUnit()
                : new CrewUnit();

            unit.Name = kind.DisplayName.Replace(' ', '-');
            AddChild(unit);
            unit.Station(kind, yard + (kind.YardStand * BasinWorld.TileSize));
            unit.Arrived += OnArrived;
            unit.Prepared += OnPrepared;
            unit.Home += _ => { };
            _roster.Add(unit);
        }
    }

    /// <summary>An idle unit that carries this job, or null if none is free.</summary>
    public Unit? Free(JobKind job)
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            if (_roster[i].IsIdle && _roster[i].Kind.Carries == job)
                return _roster[i];
        }

        return null;
    }

    /// <summary>Every unit, for the fleet board and the yard's own readouts.</summary>
    public IReadOnlyList<Unit> Roster => _roster;

    /// <summary>Everything a construction crew can add.</summary>
    public IReadOnlyList<BuildKind> Catalogue => _catalogue;

    /// <summary>What is under construction right now, for the world to draw.</summary>
    public IReadOnlyList<string> Rising
    {
        get
        {
            var names = new List<string>(_rising.Count);

            for (int i = 0; i < _rising.Count; i++)
                names.Add(_rising[i].Kind.DisplayName);

            return names;
        }
    }

    /// <summary>
    /// Commission a job. The unit leaves now; the engine hears about it later.
    /// </summary>
    public bool Send(JobKind job, Vector2 site, ulong subject)
    {
        Unit? unit = Free(job);

        if (unit is null)
        {
            EmitSignal(SignalName.Reported, $"No {Name(job)} is free.", true);

            return false;
        }

        unit.SendTo(site, job, subject);
        EmitSignal(SignalName.Reported, $"{unit.Kind.DisplayName} is on its way.", false);

        return true;
    }

    /// <summary>
    /// A unit reached its site. This is the only place a command is submitted.
    /// </summary>
    /// <remarks>
    /// A refusal here is normal rather than exceptional: the engine can refuse
    /// for reasons that were not true when the job was commissioned — the rig
    /// became busy, the cash ran out. The unit turns round and every reason is
    /// reported, which is §9.1 applied to a crew standing on a pad.
    /// </remarks>
    private void OnArrived(Unit unit)
    {
        PreparingSite?.Invoke(unit.Job, unit.Subject, unit.Position);
        unit.Prepare(PrepareSeconds(unit.Job));
        EmitSignal(SignalName.Reported, $"{unit.Kind.DisplayName} is preparing the site.", false);
    }

    private void OnPrepared(Unit unit)
    {
        FieldReadModel? snapshot = EngineHost.Instance.Snapshot;
        Command? command = Build(unit.Job, unit.Subject, snapshot);

        if (snapshot is null || command is null)
        {
            EmitSignal(SignalName.Reported, $"{unit.Kind.DisplayName} found nothing to do.", true);
            unit.GoHome();

            return;
        }

        CommandResult result = EngineHost.Instance.Submit(command);

        if (result is Rejected rejected)
        {
            var why = new System.Text.StringBuilder();

            for (int i = 0; i < rejected.Reasons.Count; i++)
            {
                if (i > 0)
                    why.Append(" · ");

                why.Append(rejected.Reasons[i].Detail);
            }

            EmitSignal(SignalName.Reported, $"{unit.Kind.DisplayName} was turned away: {why}", true);
            unit.GoHome();

            return;
        }

        JobAccepted?.Invoke(unit.Job, unit.Subject);
        unit.Settle(snapshot.Tick.Value);

        // A build is watched for rather than timed. The count of elements
        // answering to this kind's fragment is taken NOW; when it goes up, the
        // engine has built the thing and the scaffold can become a unit. A host
        // timer would drift from the engine the first time a fault abandoned a
        // tick.
        if (unit.Job == JobKind.Build && unit.Subject < (ulong)_catalogue.Count)
        {
            BuildKind kind = _catalogue[(int)unit.Subject];
            _rising.Add((kind, Count(kind.ChainMatch, snapshot)));
        }

        EmitSignal(SignalName.Reported, $"{unit.Kind.DisplayName} has started.", false);
    }

    private static float PrepareSeconds(JobKind job) => job switch
    {
        JobKind.Drill => BasePrepareSeconds * 1.8f,
        JobKind.Build or JobKind.Commission => BasePrepareSeconds * 1.5f,
        JobKind.Repair or JobKind.Service or JobKind.FitMonitoring => BasePrepareSeconds * 0.8f,
        _ => BasePrepareSeconds,
    };

    /// <summary>Send home anything whose work the engine has finished.</summary>
    public void Bind(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        for (int i = _rising.Count - 1; i >= 0; i--)
        {
            (BuildKind kind, int was) = _rising[i];

            if (Count(kind.ChainMatch, snapshot) <= was)
                continue;

            _rising.RemoveAt(i);
            EmitSignal(SignalName.Reported, $"The {kind.DisplayName.ToLowerInvariant()} is up.", false);
        }

        // The read model publishes a COUNT of running activities and nothing per
        // activity (gap G-15), so a unit cannot ask whether its own job is done.
        // What it can see is that the field went quiet, and that is enough to
        // bring everybody home without inventing a per-job progress it has no
        // way to know.
        if (snapshot.ActivitiesRunning > 0)
            return;

        for (int i = 0; i < _roster.Count; i++)
        {
            if (_roster[i].State == UnitState.Working)
                _roster[i].GoHome();
        }
    }

    /// <summary>
    /// The yard, as a line of text for the save's sidecar.
    /// </summary>
    /// <remarks>
    /// The engine saves the ACTIVITY — that is its state — and the host saves
    /// which unit was carrying it and where the unit had got to. Neither is a
    /// copy of the other: an activity has no vehicle and a vehicle has no
    /// duration.
    /// </remarks>
    public string Pack()
    {
        var packed = new System.Text.StringBuilder();

        for (int i = 0; i < _roster.Count; i++)
        {
            Unit unit = _roster[i];

            if (i > 0)
                packed.Append(';');

            packed.Append(unit.Kind.DisplayName).Append('|')
                .Append((int)unit.State).Append('|')
                .Append((int)unit.Job).Append('|')
                .Append(unit.Subject).Append('|')
                .Append(unit.StartedOn).Append('|')
                .Append((int)unit.Position.X).Append('|')
                .Append((int)unit.Position.Y);
        }

        return packed.ToString();
    }

    /// <summary>Put the yard back the way a save left it.</summary>
    public void Unpack(string packed)
    {
        if (packed.Length == 0)
            return;

        foreach (string entry in packed.Split(';'))
        {
            string[] parts = entry.Split('|');

            if (parts.Length != 7)
                continue;

            Unit? unit = null;

            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster[i].Kind.DisplayName == parts[0])
                    unit = _roster[i];
            }

            if (unit is null)
                continue;

            unit.Restore(
                (UnitState)int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                (JobKind)int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                ulong.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                new Vector2(
                    float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private Command? Build(JobKind job, ulong subject, FieldReadModel? snapshot) => job switch
    {
        JobKind.Survey => new SeismicSurveyCommand(new EntityId<IProspect>(subject)),
        JobKind.SurveyBlock => new SurveyBlockCommand(new EntityId<IBlock>(subject)),
        JobKind.Commission => new InstallEarlyProductionFacilityCommand(),
        JobKind.Drill => new DrillWellCommand(new EntityId<IProspect>(subject), new Length(2000.0)),
        JobKind.WellTest => Compartment(snapshot) is EntityId<IReservoirCompartmentEntity> t
            ? new WellTestCommand(t)
            : null,
        JobKind.WirelineLog => Compartment(snapshot) is EntityId<IReservoirCompartmentEntity> l
            ? new WirelineLogCommand(l)
            : null,
        JobKind.CutCore => Compartment(snapshot) is EntityId<IReservoirCompartmentEntity> c
            ? new CutCoreCommand(c)
            : null,
        JobKind.Build => Install(subject),
        JobKind.Repair => Element(subject, snapshot) is EntityRef r ? new RepairEquipmentCommand(r) : null,
        JobKind.Service => Element(subject, snapshot) is EntityRef s ? new ServiceEquipmentCommand(s) : null,
        JobKind.FitMonitoring => Element(subject, snapshot) is EntityRef m
            ? new InstallMonitoringCommand(m)
            : null,
        _ => null,
    };

    /// <summary>The engine command a catalogue entry orders.</summary>
    private Command? Install(ulong at)
    {
        if (at >= (ulong)_catalogue.Count)
            return null;

        return _catalogue[(int)at].Orders switch
        {
            BuildCommand.Separator => new InstallSeparatorCommand(),
            BuildCommand.Manifold => new InstallManifoldCommand(),
            BuildCommand.GasPlant => new InstallGasPlantCommand(),
            BuildCommand.Treater => new InstallTreaterCommand(),
            BuildCommand.Tank => new InstallTankCommand(),
            BuildCommand.Export => new ExpandExportCommand(),
            _ => null,
        };
    }

    private void LoadBuilds()
    {
        using DirAccess? directory = DirAccess.Open(Builds);

        if (directory is null)
        {
            GD.PushError($"[yard] cannot open {Builds}: {DirAccess.GetOpenError()}");

            return;
        }

        string[] files = directory.GetFiles();
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            string name = file.EndsWith(".remap", StringComparison.Ordinal) ? file[..^6] : file;

            if (!name.EndsWith(".tres", StringComparison.Ordinal))
                continue;

            if (GD.Load<BuildKind>($"{Builds}/{name}") is BuildKind kind)
                _catalogue.Add(kind);
        }

        if (_catalogue.Count == 0)
            GD.PushError($"[yard] no build kinds found under {Builds}");
    }

    /// <summary>How many chain elements answer to a fragment right now.</summary>
    private static int Count(string match, FieldReadModel? snapshot)
    {
        if (snapshot is null)
            return 0;

        int found = 0;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].DisplayId.Contains(match, StringComparison.Ordinal))
                found++;
        }

        return found;
    }

    /// <summary>
    /// A compartment a downhole measurement can be aimed at.
    /// </summary>
    /// <remarks>
    /// Read off the beliefs the company holds, which is the only door a host has
    /// to a compartment id — reaching into field control for it would be reading
    /// engine internals.
    /// </remarks>
    private static EntityId<IReservoirCompartmentEntity>? Compartment(FieldReadModel? snapshot)
    {
        if (snapshot is null)
            return null;

        for (int i = 0; i < snapshot.Beliefs.Count; i++)
        {
            if (snapshot.Beliefs[i].Subject.Kind == EntityKind.Compartment)
                return new EntityId<IReservoirCompartmentEntity>(snapshot.Beliefs[i].Subject.Value);
        }

        return null;
    }

    private static EntityRef? Element(ulong id, FieldReadModel? snapshot)
    {
        if (snapshot is null)
            return null;

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            if (snapshot.Chain[i].Element.Value == id)
                return snapshot.Chain[i].Element;
        }

        return null;
    }

    private static string Name(JobKind job) => job switch
    {
        JobKind.Survey => "survey crew",
        JobKind.Drill => "rig",
        JobKind.WellTest => "well services unit",
        JobKind.WirelineLog => "wireline truck",
        JobKind.CutCore => "coring unit",
        JobKind.Repair or JobKind.Service or JobKind.FitMonitoring => "maintenance crew",
        _ => "unit",
    };

    private void LoadKinds()
    {
        using DirAccess? directory = DirAccess.Open(Folder);

        if (directory is null)
        {
            GD.PushError($"[yard] cannot open {Folder}: {DirAccess.GetOpenError()}");

            return;
        }

        string[] files = directory.GetFiles();
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            string name = file.EndsWith(".remap", StringComparison.Ordinal) ? file[..^6] : file;

            if (!name.EndsWith(".tres", StringComparison.Ordinal))
                continue;

            if (GD.Load<UnitKind>($"{Folder}/{name}") is UnitKind kind)
                _kinds.Add(kind);
        }

        if (_kinds.Count == 0)
            GD.PushError($"[yard] no unit kinds found under {Folder}");
    }
}
