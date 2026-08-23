// Plans 26 §6's named next probe — the ledger by category over one producing
// year (finding 285).
//
// THE QUESTION, in the doc's own words: with three producers and cargos
// cycling, a Days field nets about −$3.1M a month while the gross-margin
// arithmetic says +$1.4M — "roughly $2M a month is going somewhere the margin
// arithmetic does not name. The next probe is the ledger by category over one
// producing year."
//
// TWO RUNS, NOT ONE, because the −$3.1M was measured under the auto-player,
// whose own monthly actions (surveys, appraisals, holes) spend money the
// margin arithmetic never counted. Measuring the same span twice — once with
// the cycle playing, once with the field left alone but repaired — separates
// what the FIELD loses from what the PLAYER spends, which are different
// problems with different levers.
//
// AND EVERY EXIT PRINTS ITS LEDGER. The first version reported "insolvent
// before first delivery — nothing to measure", which is exactly backwards: a
// company that dies before delivering is the strongest possible reason to see
// where the money went.
//
// AN INSTRUMENT, NOT A PRODUCT SURFACE. Like the test suites, it reaches
// `Provided.Resolve` for the ledger's own movements — the read model's
// per-category row answers the headline and the movement-by-movement join is
// what names the line items under it.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Engineer;

public static class LedgerProbe
{
    /// <summary>How long a producing year is.</summary>
    private const int Year = 12;

    public static int Run(
        IGameStyle mode, EngineSettings settings, WorldParameters world, int monthsCap)
    {
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(world);

        Console.WriteLine($"LEDGER PROBE — {mode.Title}, seed {settings.WorldSeed}");
        Console.WriteLine();

        CycleRun(settings, world, monthsCap);
        FieldRun(settings, world, monthsCap);

        return 0;
    }

    /// <summary>
    /// Variant A — the cycle's own run: the auto-player plays until the field
    /// first delivers, then twelve more months. Both spans are printed: the
    /// run-up's burn is a measurement too, and when the company dies before
    /// delivering it is the only one there is.
    /// </summary>
    private static void CycleRun(EngineSettings settings, WorldParameters world, int monthsCap)
    {
        Engine engine = Compose(settings, world);
        var cycle = new WorkflowCycle(engine, drillAbove: 0.25);

        cycle.Step();       // the look — a read model exists from here on

        var runUp = new Sample(engine, "A — THE CYCLE, from the opening to first delivery");

        var months = 1;
        while (months < monthsCap)
        {
            cycle.Step();
            runUp.Month();
            months++;

            FieldReadModel now = engine.ReadModel!;

            if (now.Insolvent)
            {
                runUp.Print($"INSOLVENT at month {months}, before first delivery");
                Plant(engine);
                return;
            }

            if (now.ProducedThisTick.CubicMetres > 0.0) break;
        }

        if (engine.ReadModel!.ProducedThisTick.CubicMetres <= 0.0)
        {
            runUp.Print($"nothing delivered in {months} months");
            Plant(engine);
            return;
        }

        runUp.Print($"first delivery at month {months}");

        var producing = new Sample(engine, "A — THE CYCLE'S PRODUCING YEAR");

        for (var month = 0; month < Year && !engine.ReadModel!.Insolvent; month++)
        {
            cycle.Step();
            producing.Month();
        }

        producing.Print(engine.ReadModel!.Insolvent ? "ended INSOLVENT" : "completed");
        Plant(engine);
    }

    /// <summary>
    /// Variant B — the field's own run: a declared reservoir, three wells, the
    /// plant bought through the real command, and then NOTHING but repairs.
    /// What this run loses is what the FIELD loses.
    /// </summary>
    private static void FieldRun(EngineSettings settings, WorldParameters world, int monthsCap)
    {
        Engine engine = Compose(settings, world);

        FieldControl field = engine.Provided.Resolve<FieldControl>();
        EntityId<IReservoirCompartmentEntity> reservoir = Declare(engine);

        // THREE PRODUCERS, matching the run the −$3.1M was measured on.
        for (var well = 0; well < 3; well++)
            field.Drill(reservoir, new Length(2000.0));

        if (engine.Commands.Submit(new InstallEarlyProductionFacilityCommand())
            is Rejected refused)
        {
            Console.WriteLine("  field run: the plant order was refused — " +
                string.Join("; ", refused.Reasons.Select(r => r.Detail)));
            return;
        }

        engine.Pipeline.AdvanceTick();      // a read model exists from here on

        var runUp = new Sample(
            engine, "B — THE FIELD ALONE, from the plant order to first delivery");

        var months = 1;
        while (months < monthsCap)
        {
            Repair(engine);
            engine.Pipeline.AdvanceTick();
            runUp.Month();
            months++;

            FieldReadModel now = engine.ReadModel!;

            if (now.Insolvent)
            {
                runUp.Print($"INSOLVENT at month {months}, before first delivery");
                Plant(engine);
                return;
            }

            if (now.ProducedThisTick.CubicMetres > 0.0) break;
        }

        if (engine.ReadModel!.ProducedThisTick.CubicMetres <= 0.0)
        {
            runUp.Print($"nothing delivered in {months} months");
            Plant(engine);
            return;
        }

        runUp.Print($"first delivery at month {months}");

        var producing = new Sample(
            engine, "B — THE FIELD'S OWN PRODUCING YEAR (three wells, repairs only)");

        for (var month = 0; month < Year && !engine.ReadModel!.Insolvent; month++)
        {
            Repair(engine);
            engine.Pipeline.AdvanceTick();
            producing.Month();
        }

        producing.Print(engine.ReadModel!.Insolvent ? "ended INSOLVENT" : "completed");
        Plant(engine);
    }

    /// <summary>
    /// Where the mass stops, said from the read model alone: every element with
    /// its throughput, its failure flag and its spec breaches (finding 280's own
    /// field), and every well with its status. Printed on every exit, because a
    /// ledger that says lifting was paid and nothing was delivered is exactly
    /// half an answer — this is the other half.
    /// </summary>
    private static void Plant(Engine engine)
    {
        FieldReadModel now = engine.ReadModel!;

        Console.WriteLine("  the chain, as the read model shows it:");

        for (var i = 0; i < now.Chain.Count; i++)
        {
            ChainElementView element = now.Chain[i];

            var said = $"    {element.DisplayId,-16} {element.Throughput.Kilograms,14:N0} kg";

            if (element.Failed) said += "  FAILED";

            for (var b = 0; b < element.Breaches.Count; b++)
                said += $"  breach {element.Breaches[b].Property}: " +
                        $"{element.Breaches[b].Measured:F4} over {element.Breaches[b].Limit:F4}";

            Console.WriteLine(said);
        }

        for (var i = 0; i < now.Wellbores.Count; i++)
            Console.WriteLine($"    {now.Wellbores[i].DisplayId,-16} {now.Wellbores[i].Status}");

        Console.WriteLine();
    }

    /// <summary>One month's maintenance, the way every long fixture runs it:
    /// order a repair for anything the chain reports down.</summary>
    private static void Repair(Engine engine)
    {
        if (engine.ReadModel is not FieldReadModel seen) return;

        for (var i = 0; i < seen.Chain.Count; i++)
            if (seen.Chain[i].Failed)
                engine.Commands.Submit(new RepairEquipmentCommand(seen.Chain[i].Element));
    }

    private static Engine Compose(EngineSettings settings, WorldParameters world)
    {
        BuildResult result = EngineBuilder.CreateNew(settings, world);

        if (result is not Built built)
            throw new InvalidOperationException(
                "the probe could not compose the engine the ordinary path already composes");

        return built.Engine;
    }

    /// <summary>
    /// A reservoir the company already knows is there (SDD-010 §4b) — the
    /// shipped fixture's own declaration, restated the way a CLIENT must state
    /// it: `Defaults` is internal to composition and this project gets the
    /// published surface and nothing else.
    /// </summary>
    private static EntityId<IReservoirCompartmentEntity> Declare(Engine engine)
    {
        EntityId<IReservoirCompartmentEntity> field =
            engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(100.0e6),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0),
                    FluidSystem: new ContentId("medium-crude")),
                permeability: new Permeability(1.0e-13),
                netThickness: new Length(20.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                wettability: RelativePermeabilityCurve.Validated(
                    swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
                drive: new ContentId("water-drive"),
                aquiferStrength: 4.0,
                aquiferResponseTime: Duration.FromTicks(40.0 * 12.0));

        engine.Provided.Resolve<WorldState>()
              .DeclareKnownField(field, new ReservoirVolume(100.0e6));

        return field;
    }

    /// <summary>
    /// A span's ledger, gathered two ways: the read model's own signed cash
    /// per category (the headline), and the movements joined to their audit
    /// causes (the line items under it).
    /// </summary>
    private sealed class Sample(Engine engine, string title)
    {
        private readonly Money[] _byCategory =
            new Money[OGSim.Company.CostLedger.Causes.Count];

        private readonly Tick _from = engine.ReadModel!.Tick;

        private int _months;
        private double _producedM3;
        private readonly Money _opening = engine.ReadModel!.Cash;

        public void Month()
        {
            FieldReadModel now = engine.ReadModel!;

            for (var i = 0; i < _byCategory.Length; i++)
                _byCategory[i] += now.CashByCause[i];

            _producedM3 += now.ProducedThisTick.CubicMetres;
            _months++;
        }

        public void Print(string outcome)
        {
            FieldReadModel end = engine.ReadModel!;

            Console.WriteLine($"  {title} — {outcome}");
            Console.WriteLine(
                $"  {_months} months, {_producedM3:N0} m3 delivered; cash " +
                $"{Millions(_opening)} -> {Millions(end.Cash)} " +
                $"({Millions(new Money(end.Cash.Cents - _opening.Cents))}, " +
                $"{Millions(new Money((end.Cash.Cents - _opening.Cents) / Math.Max(1, _months)))}/month)");
            Console.WriteLine();
            Console.WriteLine("  signed cash by category (the read model's own row):");

            for (var i = 0; i < _byCategory.Length; i++)
                if (_byCategory[i].Cents != 0)
                    Console.WriteLine(
                        $"    {OGSim.Company.CostLedger.Causes[i],-12} {Millions(_byCategory[i]),10}" +
                        $"  ({Millions(new Money(_byCategory[i].Cents / Math.Max(1, _months))),10}/month)");

            Console.WriteLine();
            Console.WriteLine("  the line items (movements joined to their audit causes):");

            foreach ((string what, long cents) in LineItems(end.Tick))
                Console.WriteLine(
                    $"    {what,-28} {Millions(new Money(cents)),10}" +
                    $"  ({Millions(new Money(cents / Math.Max(1, _months))),10}/month)");

            Console.WriteLine();
        }

        /// <summary>
        /// Signed cash per named line item over the sampled window. The name is
        /// the audit cause's own `spend`/`accrual` kind where the movement has
        /// one, and `category:account` where it does not — never invented here.
        /// </summary>
        private IEnumerable<(string What, long Cents)> LineItems(Tick to)
        {
            var kinds = new Dictionary<AuditId, string>();

            foreach (AuditEntry entry in engine.Audit.Query(
                new AuditQuery(null, AuditCategory.Financial, null, null)))
            {
                if (entry.Data.TryGetValue("spend", out AuditValue spend))
                    kinds[entry.Id] = spend.Value;
                else if (entry.Data.TryGetValue("accrual", out AuditValue accrual))
                    kinds[entry.Id] = accrual.Value;
            }

            var byItem = new Dictionary<string, long>(StringComparer.Ordinal);

            IReadOnlyList<OGSim.Company.Movement> movements =
                engine.Provided.Resolve<OGSim.Company.CompanyState>().Ledger.Movements;

            for (var i = 0; i < movements.Count; i++)
            {
                OGSim.Company.Movement movement = movements[i];

                if (movement.Tick.Value <= _from.Value || movement.Tick.Value > to.Value)
                    continue;

                long signed =
                    movement.Debit == OGSim.Company.Account.Cash ? movement.Amount.Cents
                    : movement.Credit == OGSim.Company.Account.Cash ? -movement.Amount.Cents
                    : 0L;

                if (signed == 0L) continue;

                string what = kinds.TryGetValue(movement.Cause, out string? kind)
                    ? kind
                    : movement.Category + ":" + Named(movement);

                byItem[what] = byItem.GetValueOrDefault(what) + signed;
            }

            return byItem.OrderBy(pair => pair.Value)
                         .Select(pair => (pair.Key, pair.Value));
        }

        /// <summary>The non-cash leg, which is what a movement is FOR.</summary>
        private static string Named(OGSim.Company.Movement movement) =>
            (movement.Debit == OGSim.Company.Account.Cash
                ? movement.Credit
                : movement.Debit).ToString();

        private static string Millions(Money money) =>
            "$" + (money.Cents / 100.0 / 1.0e6).ToString(
                "F2", System.Globalization.CultureInfo.InvariantCulture) + "M";
    }
}
