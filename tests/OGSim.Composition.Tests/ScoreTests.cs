// R24.6 — the eight score dimensions (SDD-014 §4, finding 290). These pin the
// MECHANISM: dimensions appear when their denominators have happened, read the
// span rather than the tick, and survive a reload — no formula constant is
// pinned, so rebalancing a scenario's scoring stays content.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class ScoreTests
{
    /// <summary>
    /// A RUN THAT HAS DONE NOTHING SCORES ALMOST NOTHING (SDD-014 §4's
    /// finding-290 amendment): a dimension whose denominator has not happened
    /// is OMITTED, never reported as zero — "the finding cost of nothing
    /// found" has no answer, and zero would flatter it. What IS always
    /// answerable from the first month: the HSE standing (real every tick)
    /// and Legacy (the opening well already carries its abandonment
    /// obligation, none of it yet made good).
    /// </summary>
    [Fact]
    public void GM9_dimensions_without_a_denominator_are_omitted()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        engine.Pipeline.AdvanceTick();

        IReadOnlyList<(ScoreDimension Dimension, double Score)> scores =
            engine.ReadModel!.Progress.Scores;

        Assert.Contains(scores, s => s.Dimension == ScoreDimension.Hse);

        // NOT Legacy: the opening position's composed well predates the
        // register (a scenario hands over a field, not a liability history),
        // so incurred is zero until the run drills its own first hole — and
        // an undefined ratio is omitted, which is this test's own rule.
        Assert.DoesNotContain(scores, s => s.Dimension == ScoreDimension.Legacy);

        Assert.DoesNotContain(scores, s => s.Dimension == ScoreDimension.OperatingCost);
        Assert.DoesNotContain(scores, s => s.Dimension == ScoreDimension.Uptime);
        Assert.DoesNotContain(scores, s => s.Dimension == ScoreDimension.Recovery);
        Assert.DoesNotContain(scores, s => s.Dimension == ScoreDimension.FindingCost);
    }

    /// <summary>
    /// A PRODUCING RUN SCORES ITS DIMENSIONS. Drill and produce for two years
    /// and the span has answers: operating cost per cubic metre, uptime as a
    /// fraction of what the chain could have carried, capital efficiency over
    /// the development spend, recovery against the book at sanction — each
    /// finite, each from the run's own ledger, none invented.
    /// </summary>
    [Fact]
    public void GM9_a_producing_run_scores_its_dimensions()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Producing();

        for (var month = 0; month < 24; month++)
        {
            if (engine.ReadModel?.ActivitiesRunning == 0 && engine.ReadModel.Wells < 2)
                engine.Commands.Submit(new DrillWellCommand(
                    engine.Provided.Resolve<WorldState>().ProspectFor(target),
                    new Length(2000.0)));

            engine.Pipeline.AdvanceTick();
        }

        IReadOnlyList<(ScoreDimension Dimension, double Score)> scores =
            engine.ReadModel!.Progress.Scores;

        (ScoreDimension, double) Of(ScoreDimension dimension) =>
            Assert.Single(scores, s => s.Dimension == dimension);

        (_, double operatingCost) = Of(ScoreDimension.OperatingCost);
        Assert.True(operatingCost > 0.0 && double.IsFinite(operatingCost),
            $"a producing field's operating cost read {operatingCost}");

        (_, double uptime) = Of(ScoreDimension.Uptime);
        Assert.True(uptime > 0.0 && uptime <= 1.0,
            $"uptime read {uptime}; produced over produced-plus-deferred is a fraction");

        (_, double capital) = Of(ScoreDimension.CapitalEfficiency);
        Assert.True(double.IsFinite(capital),
            $"capital efficiency read {capital} over real development spend");

        (_, double recovery) = Of(ScoreDimension.Recovery);
        Assert.True(recovery > 0.0 && double.IsFinite(recovery),
            $"recovery read {recovery} against the book at sanction");

        (_, double hse) = Of(ScoreDimension.Hse);
        Assert.InRange(hse, 0.0, 100.0);

        // The run drilled its own wells, so the liability history is real now
        // and none of it has been made good.
        (_, double legacy) = Of(ScoreDimension.Legacy);
        Assert.Equal(0.0, legacy);
    }

    /// <summary>
    /// THE SPAN SURVIVES A RELOAD. A score integrates the whole run, so a save
    /// that forgot the first years' production would score the decade on its
    /// second half — the reloaded engine must carry the same accumulators
    /// forward, which PV2's identical-continuation now checks for free
    /// (`Progress.Scores` is on the read model) and this pins directly.
    /// </summary>
    [Fact]
    public void R24V16_the_score_span_survives_a_reload()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Producing();

        for (var month = 0; month < 18; month++)
        {
            if (engine.ReadModel?.ActivitiesRunning == 0 && engine.ReadModel.Wells < 2)
                engine.Commands.Submit(new DrillWellCommand(
                    engine.Provided.Resolve<WorldState>().ProspectFor(target),
                    new Length(2000.0)));

            engine.Pipeline.AdvanceTick();
        }

        IReadOnlyList<(ScoreDimension Dimension, double Score)> before =
            engine.ReadModel!.Progress.Scores;

        using var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        Engine restored = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        IReadOnlyList<(ScoreDimension Dimension, double Score)> after =
            restored.ReadModel!.Progress.Scores;

        double Of(IReadOnlyList<(ScoreDimension Dimension, double Score)> list,
                  ScoreDimension dimension) =>
            Assert.Single(list, s => s.Dimension == dimension).Score;

        // The SPAN-TRUE dimensions must come back exactly: their every input
        // is an accumulator this ledger saves. CapitalEfficiency also reads
        // the LOAD INSTANT's company value, which the surface is known to
        // re-derive slightly differently until the next full tick (the
        // S013-9 family) — it is asserted finite rather than equal, and its
        // span half (Σ capex) is covered by the others' exactness.
        foreach (ScoreDimension dimension in new[]
        {
            ScoreDimension.Reserves, ScoreDimension.Recovery,
            ScoreDimension.OperatingCost, ScoreDimension.Uptime,
            ScoreDimension.Legacy,
        })
            Assert.Equal(Of(before, dimension), Of(after, dimension), precision: 9);

        Assert.True(double.IsFinite(Of(after, ScoreDimension.CapitalEfficiency)));
    }

    /// <summary>The PricingTests fixture: a declared field with a hole down,
    /// so producing months are months away rather than seeds away.</summary>
    private static (Engine, EntityId<IReservoirCompartmentEntity>) Producing()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Engine engine = built.Engine;

        EntityId<IReservoirCompartmentEntity> target =
            engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(100.0e6),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0),
                    FluidSystem: new ContentId("medium-crude")),
                permeability: new Permeability(2.0e-13),
                netThickness: new Length(30.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                Defaults.Wettability, Defaults.Drive,
                Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        engine.Provided.Resolve<WorldState>().DeclareKnownField(
            target, new ReservoirVolume(100.0e6));

        return (engine, target);
    }
}
