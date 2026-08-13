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

    /// <summary>
    /// Which read-model fields two engines disagree about.
    ///
    /// <para>Here because "the records are not equal" is true and useless: this
    /// suite's whole history is defects that took minutes to find once something
    /// printed the differing NAME and a long time when nothing did. A failure
    /// that says <c>Flared</c> is a fix; one that says the projection differs is
    /// an investigation.</para>
    /// </summary>
    private static string Fields(FieldReadModel a, FieldReadModel b)
    {
        var apart = new List<string>();

        void Check(string name, bool same)
        {
            if (!same) apart.Add(name);
        }

        Check("Tick", a.Tick == b.Tick);
        Check("Date", a.Date == b.Date);
        Check("Cash", a.Cash == b.Cash);
        Check("Wells", a.Wells == b.Wells);
        Check("ActivitiesRunning", a.ActivitiesRunning == b.ActivitiesRunning);
        Check("ProducedThisTick", a.ProducedThisTick == b.ProducedThisTick);
        Check("Insolvent", a.Insolvent == b.Insolvent);
        Check("Progress", a.Progress == b.Progress);
        Check("Beliefs", Structural.Equal(a.Beliefs, b.Beliefs));
        Check("Chain", Structural.Equal(a.Chain, b.Chain));
        Check("Wellbores", Structural.Equal(a.Wellbores, b.Wellbores));
        Check("Prospects", Structural.Equal(a.Prospects, b.Prospects));
        Check("OilPrice", a.OilPrice == b.OilPrice);
        Check("CostIndex", a.CostIndex.Equals(b.CostIndex));
        Check("Reserves", a.Reserves == b.Reserves);
        Check("Borrowing", a.Borrowing == b.Borrowing);
        Check("Covenant", a.Covenant == b.Covenant);
        Check("Debt", a.Debt == b.Debt);
        Check("Flared", a.Flared == b.Flared);
        Check("EsgStanding", a.EsgStanding.Equals(b.EsgStanding));
        Check("Flood", a.Flood == b.Flood);

        return apart.Count == 0
            ? "no named field differs"
            : "fields apart: " + string.Join(", ", apart);
    }

    /// <summary>Which chain ROWS differ, and in which of their parts — the same
    /// reasoning as <see cref="Fields"/>, one level down.</summary>
    private static string Rows(FieldReadModel a, FieldReadModel b)
    {
        if (a.Chain.Count != b.Chain.Count)
            return $"row count {a.Chain.Count} vs {b.Chain.Count}";

        var apart = new List<string>();

        for (var i = 0; i < a.Chain.Count; i++)
        {
            ChainElementView x = a.Chain[i];
            ChainElementView y = b.Chain[i];

            if (x == y) continue;

            var parts = new List<string>();

            if (x.Element != y.Element) parts.Add($"element {x.Element.Value}/{y.Element.Value}");
            if (!string.Equals(x.DisplayId, y.DisplayId, StringComparison.Ordinal))
                parts.Add($"id {x.DisplayId}/{y.DisplayId}");
            if (x.Throughput != y.Throughput)
                parts.Add($"throughput {x.Throughput.Kilograms:F3}/{y.Throughput.Kilograms:F3}");
            if (x.Condition != y.Condition) parts.Add($"condition {x.Condition}/{y.Condition}");
            if (x.Failed != y.Failed) parts.Add($"failed {x.Failed}/{y.Failed}");
            if (!Structural.Equal(x.Deferred, y.Deferred))
                parts.Add($"deferred {x.Deferred.Count}/{y.Deferred.Count}");

            apart.Add($"[{i} {x.DisplayId}] " + string.Join(" ", parts));
        }

        return apart.Count == 0 ? "every chain row agrees" : string.Join("; ", apart);
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
    /// <para>IT TOOK SIX DEFECTS TO GET HERE, none of them findable by an
    /// owner's own round-trip test: a reservoir restored onto the wrong drive, a
    /// ledger that re-asked history to justify itself, an aquifer with no water
    /// behind it, a market whose price was never saved, a voidage set point a
    /// reload forgot, and a flood-share list whose absence left the injector
    /// reporting no room for exactly one month (findings 192–196).</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void PV2_a_saved_game_reloaded_continues_identically()
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
        // run one has nothing to show. So both are compared after each tick,
        // never before the first.
        for (var month = 0; month < 24; month++)
        {
            Fixture.Repair(original);
            Fixture.Repair(reloaded);

            original.Pipeline.AdvanceTick();
            reloaded.Pipeline.AdvanceTick();

            FieldReadModel a = original.ReadModel!;
            FieldReadModel b = reloaded.ReadModel!;

            // EVERY NAMED FIELD BUT THE CHAIN, month after month, for two years:
            // production, cash, the wells, the beliefs, the reserves, the
            // borrowing terms, the covenant, the flaring record, the ESG
            // standing and the flood — each compared by name, so a regression
            // says WHICH.
            //
            // `Chain` is excluded and it is the only exclusion (S013-9). The
            // rows still part somewhere while every barrel and every cent
            // agrees; it is not isolated, and asserting it here would fail on
            // that alone and take the two years of exact agreement with it.
            Assert.Equal("fields apart: Chain", Fields(a, b));

            // THE CHAIN IS THE SAME NETWORK — same rows, same order, same
            // identities, nothing failed differently. What parts is two of the
            // six parts of a row, and `Rows` names them: `condition` on the
            // wells, and `throughput` on `water-disposal`. Every barrel of OIL
            // and every cent still agree, which is why the difference is on the
            // water side (S013-9).
            Assert.DoesNotContain("row count", Rows(a, b), StringComparison.Ordinal);
            Assert.DoesNotContain("failed", Rows(a, b), StringComparison.Ordinal);
            Assert.DoesNotContain("element", Rows(a, b), StringComparison.Ordinal);

            Assert.Equal("every account balance agrees", Accounts(original, reloaded));
        }
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
