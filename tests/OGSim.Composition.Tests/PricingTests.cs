// Finding 289 — no activity has a price; it has a RATE, and the world supplies
// the quantity (SDD-007 §3's amendment). These pin the mechanism, deliberately
// not the numbers: every rate stays a content edit.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

public sealed class PricingTests
{
    /// <summary>
    /// PRICE AND PACE SCALE WITH THE WORK. Three times the hole is three times
    /// the money and three times the months — checked as proportionality on the
    /// shipped drill terms so no rate value is pinned and rebalancing stays a
    /// content edit.
    /// </summary>
    [Fact]
    public void F289_price_and_duration_scale_with_the_quantity()
    {
        ActivityTerms drill = Defaults.DrillWellTerms(Fixture.Activities());

        Money shallow = ActivityState.PriceFor(drill, 1000.0);
        Money deep = ActivityState.PriceFor(drill, 3000.0);

        Assert.Equal(shallow.Cents * 3, deep.Cents);

        Assert.True(ActivityState.TurnsFor(drill, 3000.0) > ActivityState.TurnsFor(drill, 1000.0),
            "a deeper hole must take longer, or duration is not derived from the work");

        // And no order finishes before it starts, however small the job.
        Assert.Equal(1, ActivityState.TurnsFor(drill, 0.0));
    }

    /// <summary>
    /// A DEEP WELL RUNS LONGER IN THE ENGINE, not only in the arithmetic: the
    /// 3,000 m hole is still turning after the months the reference 2,000 m
    /// hole takes.
    /// </summary>
    [Fact]
    public void F289_a_deeper_hole_takes_more_months_end_to_end()
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
                    Depth: new Length(3000.0),
                    FluidSystem: new ContentId("medium-crude")),
                permeability: new Permeability(2.0e-13),
                netThickness: new Length(30.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(2900.0),
                oilWaterContact: new Length(3100.0),
                Defaults.Wettability, Defaults.Drive,
                Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        engine.Provided.Resolve<WorldState>().DeclareKnownField(
            target, new ReservoirVolume(100.0e6));
        engine.Pipeline.AdvanceTick();

        Assert.IsType<Accepted>(engine.Commands.Submit(new DrillWellCommand(
            engine.Provided.Resolve<WorldState>().ProspectFor(target),
            new Length(3000.0))));

        int reference = ActivityState.TurnsFor(
            Defaults.DrillWellTerms(Fixture.Activities()), 2000.0);

        for (var month = 0; month < reference; month++) engine.Pipeline.AdvanceTick();

        Assert.True(engine.ReadModel!.ActivitiesRunning > 0,
            "the 3,000 m hole finished in the 2,000 m hole's months; duration is not " +
            "derived from the work");
    }

    /// <summary>
    /// A RATE IN THE WRONG DIMENSION REFUSES TO COMPOSE, naming both sides:
    /// the engine owns which quantity a verb measures, content owns the rate
    /// per unit of it, and the two disagreeing must never load as a price.
    /// </summary>
    [Fact]
    public void F289_a_rate_in_the_wrong_dimension_refuses_to_compose()
    {
        InvariantFault fault = Assert.Throws<InvariantFault>(() => EngineBuilder.Build(
            Fixture.Settings(content:
                [Edited("drill-development-well", "\"unit\": \"metre\"",
                        "\"unit\": \"fortnight\"")])));

        Assert.Contains("metre", fault.Fault.Detail, StringComparison.Ordinal);
        Assert.Contains("fortnight", fault.Fault.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// AN OUTCOME TABLE THAT DOES NOT SUM TO ONE REFUSES TO LOAD (SDD-007 §4,
    /// R12b.12, finding 292): a table summing short draws an outcome nobody
    /// declared, and one summing long silently re-normalises the designer's
    /// odds. The rows are the entry's own content now, and the loader is the
    /// gate.
    /// </summary>
    [Fact]
    public void F292_an_outcome_table_that_does_not_sum_to_one_refuses_to_load()
    {
        BuildResult result = EngineBuilder.Build(
            Fixture.Settings(content:
                [Edited("drill-development-well",
                        "\"probability\": 0.4,", "\"probability\": 0.3,")]));

        BuildRefusedByContent refused = Assert.IsType<BuildRefusedByContent>(result);

        Assert.Contains(refused.Failures,
            failure => failure.Message.Contains("sum to", StringComparison.Ordinal));
    }

    /// <summary>
    /// AN UNKNOWN OUTCOME GRADE REFUSES AT COMPOSITION, by name — the grade
    /// vocabulary lives in the contracts layer, so the name crosses from
    /// content exactly once, where the enum can answer for it.
    /// </summary>
    [Fact]
    public void F292_an_unknown_outcome_grade_refuses_to_compose()
    {
        ContentFault fault = Assert.Throws<ContentFault>(() => EngineBuilder.Build(
            Fixture.Settings(content:
                [Edited("drill-development-well",
                        "\"grade\": \"delayed\"", "\"grade\": \"sideways\"")])));

        Assert.Contains("sideways", fault.Fault.Detail, StringComparison.Ordinal);
        Assert.Contains("drill-development-well", fault.Fault.Detail, StringComparison.Ordinal);
    }

    /// <summary>The suite's own one-entry content edit, per-class like its two
    /// siblings (rebalance-shaped tests each carry their own).</summary>
    private static IContentSource Edited(string id, string find, string replace)
    {
        var files = new List<ContentFile>();

        foreach (ContentFile file in Fixture.ShippedContent().Files)
        {
            if (!file.RelativePath.EndsWith(id + ".json", StringComparison.Ordinal))
            {
                files.Add(file);
                continue;
            }

            Assert.Contains(find, file.Json, StringComparison.Ordinal);
            files.Add(file with { Json = file.Json.Replace(find, replace, StringComparison.Ordinal) });
        }

        return new EditedSource(files);
    }

    private sealed class EditedSource(IReadOnlyList<ContentFile> files) : IContentSource
    {
        public string Name => "base";
        public int DeclaredOrder => 0;
        public IReadOnlyList<ContentFile> Files => files;
    }
}
