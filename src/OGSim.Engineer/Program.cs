// Oilfield Engineer — the realistic build of the same engine (plans 23 §4's
// 2026-08-23 amendment).
//
// USAGE: dotnet run --project src/OGSim.Engineer [-- --months=120 --seed=7 --mode=engineer]

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Engineer;
using OGSim.Kernel;

int months = Argument("--months=", 120);
ulong seed = (ulong)Argument("--seed=", 20260806);
string modeId = Text("--mode=", GameStyles.Engineer.Id.Value);

// The one probe this instrument carries (plans 26 §6's own named next step):
// `--probe=ledger` measures a producing year's ledger by category and by line
// item, twice — under the auto-player, and with the field left alone.
string probe = Text("--probe=", string.Empty);

IGameStyle mode = GameStyles.Named(new ContentId(modeId));

Console.WriteLine(mode.Title);
Console.WriteLine(mode.Premise);
Console.WriteLine();
Console.WriteLine($"  physics        {mode.RealityProfile.Value}");
Console.WriteLine($"  opens holding  {mode.StartingState.Value}");
Console.WriteLine($"  rules          {mode.Rules.Value}");
Console.WriteLine($"  seed           {seed}");
Console.WriteLine();

// THE SAME COMPOSITION THE GODOT HOST USES, at the other named mode. The three
// axes are written by the mode and nowhere else, which is the whole reason a
// mode is a thing (law L5).
var settings = mode.Compose(new EngineSettings(
    Epoch: new GameDate(1965, 1),
    WorldSeed: seed,
    Retention: new AuditRetention(DetailWindowTicks: 12),
    LogSink: new ConsoleSink(),
    MinimumLogLevel: LogLevel.Warning,
    FaultHandling: FaultHandling.Resilient,

    // Overwritten by Compose; named here because the record requires them and a
    // default would be law L2's missing dependency wearing a value.
    RealityProfile: mode.RealityProfile,
    Content: RepositoryContent.StyleOverrides(mode.Id) is { } overlay
        ? [RepositoryContent.Read(), overlay]
        : [RepositoryContent.Read()],
    StartingState: mode.StartingState,
    Rules: mode.Rules,
    Style: mode.Id));

// COMPOSITION IS ALL-OR-NOTHING (design 03 §3), and a refusal names every
// reason rather than the first one — a player told one of four problems fixes
// one of four. Built ONCE: composing twice would generate the world twice.
// AND A WORLD TO WORK. Composition builds the stores; generation writes truth
// into them, and in that order (SDD-010 §4) — an engine composed but never
// generated is a company holding a licence over ground that does not exist,
// which is exactly what it looked like the first time this ran: sixty months of
// "ran the field" with no field.
var world = new WorldParameters(
    new ContentId("world-template-basin"),
    WidthCells: 32,
    HeightCells: 32,
    LandFraction: 0.72,
    ResourceRichness: 1.0,
    BasinMaturity: 0.5,
    ClimateSeverity: 0.28,
    RivalCount: 4,
    StartEra: Era.E1);

if (probe == "ledger")
    return LedgerProbe.Run(mode, settings, world, months);

BuildResult result = EngineBuilder.CreateNew(settings, world);

if (result is BuildRefusedByContent bad)
{
    Console.Error.WriteLine("the content will not load:");

    for (var i = 0; i < bad.Failures.Count; i++)
        Console.Error.WriteLine(
            $"  - {bad.Failures[i].File} [{bad.Failures[i].Stage}] {bad.Failures[i].Message}");

    return 1;
}

if (result is BuildRefused refused)
{
    Console.Error.WriteLine("the engine refused to compose:");

    for (var i = 0; i < refused.Refusal.Problems.Count; i++)
        Console.Error.WriteLine(
            $"  - {refused.Refusal.Problems[i].Kind} in " +
            $"{refused.Refusal.Problems[i].Module.Value}: {refused.Refusal.Problems[i].Detail}");

    return 1;
}

var built = (Built)result;

// AN OPERATOR'S HURDLE, not a game's. A company with a producing field has
// somewhere better to put the money than a one-in-eight chance.
var cycle = new WorkflowCycle(built.Engine, drillAbove: 0.25);

CycleRun run = cycle.Run(months);

Console.WriteLine("  tick  date       phase          cash        wells   m3 this month   what happened");

for (var i = 0; i < run.Months.Count; i++)
{
    CycleMonth m = run.Months[i];

    // Every month is a line, but only the months that DID something are worth
    // reading; a hundred lines of "ran the field" buries the six that matter.
    if (m.Phase == Phase.Produce && i % 12 != 0)
        continue;

    Console.WriteLine(
        $"  {m.Tick.Value,4}  {m.Date.Year}-{m.Date.Month:00}   {m.Phase,-13}  " +
        $"{Millions(m.Cash),9}   {m.Wells,4}   {m.Produced.CubicMetres,12:N0}   {m.Note}");
}

Console.WriteLine();
Console.WriteLine($"  closed at {Millions(run.Closing)} with {run.Wells} wells; " +
                  $"{run.Lifetime.CubicMetres:N0} m3 lifted over {run.Months.Count} months" +
                  (run.Insolvent ? " — INSOLVENT" : string.Empty));

Console.WriteLine("  standing: " + (run.Standing.Count == 0
    ? "nothing at all"
    : string.Join(", ", run.Standing)));

return run.Insolvent ? 2 : 0;

static string Millions(Money money) =>
    "$" + (money.Cents / 100.0 / 1.0e6).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "M";

static int Argument(string flag, int fallback)
{
    string raw = Text(flag, string.Empty);

    return int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int value)
        ? value
        : fallback;
}

static string Text(string flag, string fallback)
{
    string[] argv = Environment.GetCommandLineArgs();

    for (var i = 0; i < argv.Length; i++)
        if (argv[i].StartsWith(flag, StringComparison.Ordinal))
            return argv[i][flag.Length..];

    return fallback;
}

/// <summary>Warnings and faults, on the console.</summary>
/// <remarks>
/// Fields are printed as they come rather than formatted per event: a client
/// that knew the shape of every engine log line would be a second place the
/// engine's events are described.
/// </remarks>
internal sealed class ConsoleSink : ILogSink
{
    public void Emit(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var line = new System.Text.StringBuilder();

        line.Append('[').Append(record.Level).Append("] ").Append(record.EventName);

        for (var i = 0; i < record.Fields.Count; i++)
            line.Append("  ").Append(record.Fields[i].Name).Append('=').Append(record.Fields[i].Value);

        Console.Error.WriteLine(line.ToString());
    }
}
