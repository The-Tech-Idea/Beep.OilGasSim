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

    private static MemoryStream Saved(Engine engine)
    {
        var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        return container;
    }

    /// <summary>
    /// EVERY OWNER CAPTURES TOGETHER, which has never been true before: nine
    /// blocks, each stamped with its own schema version, digested as a set.
    ///
    /// <para>The continuation test this file was written for — save, reload,
    /// play on, compare — cannot exist yet, and the reason is worth stating
    /// where someone will look for it. Nothing rebuilds a loaded engine's FIELD:
    /// `WellsState.Restore` documents that completions are "rebuilt from content
    /// first", and which wells a company drilled is not content (finding
    /// 194).</para>
    ///
    /// <para>So what is asserted here is the half that works, and it is asserted
    /// for real: the walk reaches every registered owner, the state digest covers
    /// them all, and the eight stream positions are on the header — which is what
    /// makes the missing half a rebuild step rather than a format.</para>
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
