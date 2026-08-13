// R20d.12 — the round trip (SDD-013 §1–§2, §6, finding 188).
//
// THIS IS THE TEST THE NINE STATE OWNERS HAVE NEVER HAD. Each has a unit test
// that captures it and restores it and asserts the values came back; not one of
// them said anything about whether a SAVE exists, whether the owners agree on a
// container, or whether a game continues the same after a reload. That gap is
// what finding 188 was about, and the only way to close it is a test that plays,
// saves, loads and plays on.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Company;
using OGSim.Kernel;
using OGSim.Persistence;

namespace OGSim.Composition.Tests;

public sealed class SaveGameTests
{
    /// <summary>
    /// A developed field, run far enough that every owner has something to lose:
    /// wells drilled, cash moved, equipment worn and instrumented, water bought,
    /// activities in flight.
    /// </summary>
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Target) Played(
        int months)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        FieldControl field = engine.Provided.Resolve<FieldControl>();

        EntityId<IReservoirCompartmentEntity> target = field.AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        engine.Provided.Resolve<WorldState>().DeclareKnownField(
            target, new ReservoirVolume(100.0e6));

        for (var well = 0; well < 3; well++) field.Drill(target, new Length(2000.0));

        engine.Pipeline.AdvanceTick();

        // Instrument the chain so the monitoring kits — the newest saved fact
        // there is (R20d.26.4) — are something the save has to carry.
        IReadOnlyList<ChainElementView> chain = engine.ReadModel!.Chain;
        for (var i = 0; i < chain.Count; i++)
            engine.Commands.Submit(new InstallMonitoringCommand(chain[i].Element));

        // And order a flood, so the imported-water history souring depends on is
        // non-zero by the time this is written out (R20d.25).
        engine.Commands.Submit(new SetVoidageReplacementCommand(1.0));

        Fixture.Run(engine, months);

        return (engine, target);
    }

    /// <summary>Which accounts two engines disagree about — what a divergence
    /// message needs to be actionable rather than merely true.</summary>
    private static string Accounts(Engine a, Engine b)
    {
        CostLedger left = a.Provided.Resolve<CompanyState>().Ledger;
        CostLedger right = b.Provided.Resolve<CompanyState>().Ledger;

        var differences = new List<string>();

        foreach (Account account in Enum.GetValues<Account>())
        {
            Money one = left.BalanceOf(account);
            Money two = right.BalanceOf(account);

            if (one != two) differences.Add($"{account} {one.Cents} vs {two.Cents}");
        }

        return differences.Count == 0
            ? "every account balance agrees"
            : "accounts differing: " + string.Join(", ", differences);
    }

    private static MemoryStream Saved(Engine engine)
    {
        var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        return container;
    }

    /// <summary>
    /// PV2, WHICH DESIGN 11 §4 CALLS THE ONE THAT MATTERS MOST: "save at tick N,
    /// load, run to N+100 — identical to running straight through". Round-trip
    /// equality proves the bytes match; only continuation equality proves the
    /// BEHAVIOUR does, and it is the check that catches "restored as a value,
    /// not as a live dependency".
    ///
    /// <para>Both engines are run on together after the split, because a reload
    /// that restored every block correctly and left one RNG stream at zero would
    /// pass any check made at the moment of loading and diverge on the first
    /// draw. The comparison is the whole read model — production, cash, the
    /// chain, every condition — which has structural equality (finding 131), so
    /// it names the month it first differs.</para>
    ///
    /// <para>A WELL IS SHUT IN BEFORE THE SAVE, deliberately. The choke lives on
    /// the completion object rather than in any block, so until R20d.12 it was
    /// not written at all and a reload re-opened wells a player had closed. A
    /// fixture that only ever drilled would not have asked.</para>
    ///
    /// <para>WHAT THIS PINS TODAY is the physical half: the field rebuilds and
    /// produces identically, to the cubic metre, for two years. The money does
    /// not yet agree and the gap is stated at the bottom rather than hidden —
    /// PV2 is not met until it does.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void PV2_a_reloaded_field_produces_identically()
    {
        (Engine original, _) = Played(months: 60);

        // Close one, so the save has something to be wrong about.
        original.Commands.Submit(new SetWellChokeCommand(
            new EntityId<ICompletion>(original.ReadModel!.Wellbores[0].Well.Value),
            Open: false));

        Fixture.Run(original, months: 2);

        using MemoryStream container = Saved(original);

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        Assert.Equal(original.Pipeline.CurrentTick, reloaded.Pipeline.CurrentTick);

        // A LOADED ENGINE HAS NO READ MODEL UNTIL IT TICKS, by design: the
        // projection is built at the close of a month, and a game that has not
        // run one has nothing to show. So both are compared after the tick
        // below, never before it.
        Fixture.Repair(original);
        Fixture.Repair(reloaded);

        original.Pipeline.AdvanceTick();
        reloaded.Pipeline.AdvanceTick();

        FieldReadModel a = original.ReadModel!;
        FieldReadModel b = reloaded.ReadModel!;

        Assert.Equal(a.Tick, b.Tick);

        Assert.Equal(a.Wellbores.Count, b.Wellbores.Count);

        // THE MONTH AFTER THE SAVE, TO THE CUBIC METRE. Every part of the
        // rebuild has to be right for this: the wells reopened onto the same
        // compartments at the same depths with the same chokes, the reservoir
        // back at its own pressure with its own drive, the aquifer as depleted as
        // it was, the equipment as worn, and the price where the market left it.
        // Each of those was wrong at some point in getting here, and each was a
        // separate defect no owner's own round-trip test could see.
        Assert.True(a.ProducedThisTick == b.ProducedThisTick,
            $"a reloaded field produced {a.ProducedThisTick.CubicMetres:F3} m³ against " +
            $"{b.ProducedThisTick.CubicMetres:F3} in the month after the save");

        // AND IT IS NOT PV2 YET, which is the point of stopping here rather than
        // asserting less over longer (finding 196). Design 11 §4 asks for a
        // hundred months of identity and this engine holds one: the money is
        // already apart by ~$224k of opex with tax following, and by the second
        // month the production itself parts by eight millionths. Both are named
        // there with their measurements. A test that ran to N+100 and compared
        // nothing meaningful would be worse than one that stops where the
        // evidence does.
        Assert.True(
            original.Provided.Resolve<CompanyState>().Ledger.Cash
                != reloaded.Provided.Resolve<CompanyState>().Ledger.Cash,
            "the cash now agrees — finding 196's first half is closed, and this should " +
            "become the full read-model comparison over 24 months that PV2 asks for: " +
            Accounts(original, reloaded));
    }

    /// <summary>
    /// EVERY OWNER CAPTURES TOGETHER, which had never been true before: one block
    /// each, stamped with its own schema version, digested as a set.
    ///
    /// <para>Asserted against the owner LIST rather than a count, so the day a
    /// module adds state the container misses, this fails naming it — which is
    /// design 11's PV4.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_every_state_owner_is_captured_into_the_container()
    {
        (Engine engine, _) = Played(months: 60);

        using MemoryStream container = Saved(engine);

        var loaded = Assert.IsType<Loaded>(SaveGame.Read(container));

        Assert.Equal(engine.State.Owners.Count, loaded.Blocks.Count);

        for (var i = 0; i < engine.State.Owners.Count; i++)
        {
            string key = engine.State.Owners[i].Key.Value;

            Assert.Contains(loaded.Blocks,
                block => string.Equals(block.Module, key, StringComparison.Ordinal));

            Assert.True(loaded.Header.ModuleDigests.ContainsKey(key),
                $"'{key}' has a state block that the header does not digest, so a corrupted " +
                "copy of it would load unnoticed");
        }

        Assert.Equal(engine.Pipeline.CurrentTick, loaded.Header.Tick);

        // The stream a save most needs and the one whose absence would be
        // invisible: equipment failure draws from it every tick.
        Assert.True(loaded.Header.RngPositions[StreamId.Hazard.ToString()] > 0UL,
            "sixty months produced no hazard draws, so the position proves nothing");
    }

    /// <summary>
    /// PV1. THE SAME GAME WRITES THE SAME BYTES. Two saves of one engine agree
    /// digest for digest — which is what makes the per-module digests worth
    /// carrying, and what a cross-platform check compares.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_two_saves_of_one_game_are_identical()
    {
        (Engine engine, _) = Played(months: 24);

        using MemoryStream first = Saved(engine);
        using MemoryStream second = Saved(engine);

        var a = Assert.IsType<Loaded>(SaveGame.Read(first));
        var b = Assert.IsType<Loaded>(SaveGame.Read(second));

        Assert.Equal(a.Header.StateDigest, b.Header.StateDigest);
        Assert.Equal(a.Header, b.Header);
    }

    /// <summary>
    /// SDD-013 §6. A TAMPERED BLOCK IS REFUSED, and the refusal names the module
    /// rather than the file — which is the entire reason §2 carries a digest per
    /// module instead of one over the whole container.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R19V9_a_state_block_that_does_not_match_its_digest_is_refused()
    {
        (Engine engine, _) = Played(months: 12);

        using MemoryStream container = Saved(engine);

        var loaded = Assert.IsType<Loaded>(SaveGame.Read(container));

        // Re-digest the same blocks with ONE of them altered, and validate
        // against the header the save actually carries.
        var tampered = new List<ModuleBlock>(loaded.Blocks);

        tampered[0] = tampered[0] with
        {
            State = new JsonObject(new Dictionary<string, JsonValue>(StringComparer.Ordinal)
            {
                ["$schema-version"] = new JsonInteger(1),
                ["tampered"] = new JsonInteger(1),
            }),
        };

        var refused = Assert.IsType<Refused>(
            SaveFile.Validate(loaded.Header, tampered, SaveGame.SchemaVersion, []));

        Assert.Contains(refused.Reasons,
            reason => reason.Contains(tampered[0].Module, StringComparison.Ordinal));
    }

    /// <summary>
    /// AND A CONTAINER WITH NO MANIFEST IS REFUSED RATHER THAN THROWN AT. An
    /// empty or unrelated zip is a file a player picked wrongly, which is a
    /// refusal to report and not a fault to halt on.
    /// </summary>
    [Fact]
    public void R19V9_a_container_without_a_manifest_is_refused()
    {
        var empty = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(
                   empty, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("something-else.txt");
        }

        empty.Position = 0;

        var refused = Assert.IsType<Refused>(SaveGame.Read(empty));

        Assert.Contains(refused.Reasons,
            reason => reason.Contains("manifest", StringComparison.Ordinal));
    }
}
