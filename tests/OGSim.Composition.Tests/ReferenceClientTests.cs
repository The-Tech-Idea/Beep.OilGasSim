// R21-V2 — SUFFICIENCY (R21 §2.5).
//
// The reference client plays a full game holding nothing but a read model and a
// command bus. What these assert is not that it plays WELL — it does not, and is
// not meant to — but that the published surface is enough to play at all.
//
// "If it needs anything the surface does not offer, the surface is incomplete,
// and that is discovered here rather than by a UI team six months later."

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.ReferenceClient;

namespace OGSim.Composition.Tests;

public sealed class ReferenceClientTests
{
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Prospect) Field(
        double poreVolume = 100.0e6)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        EntityId<IReservoirCompartmentEntity> prospect =
            built.Engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(poreVolume),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0)),
                permeability: new Permeability(2.0e-13),
                netThickness: new Length(30.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                Defaults.Wettability, Defaults.Drive,
                Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        return (built.Engine, prospect);
    }

    /// <summary>
    /// R21-V2. A client that can see only what a host can see develops a field
    /// and wins — which is the surface being sufficient, stated as a run rather
    /// than as an opinion.
    /// </summary>
    [Fact]
    public void R21V2_a_client_using_only_the_surface_can_play_and_win()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> prospect) = Field();

        Session session = new Operator(engine, prospect, wellTarget: 6, hurdle: Money.Zero).Play(months: 120);

        Assert.Equal(ObjectiveState.Met, session.Outcome);
        Assert.True(session.WellsDrilled > 1, "it developed the field rather than drilling once");
        Assert.True(session.Debottlenecked, "and answered the constraint the wells created");
    }

    /// <summary>
    /// The whole arc, played out: a client left running past the decade watches
    /// the field drown, and closes it.
    ///
    /// <para>Abandonment is the end of the story and this is the only test that
    /// reaches it the way a player would — by producing until there is nothing
    /// worth producing, rather than by issuing the command directly.</para>
    /// </summary>
    [Fact]
    public void R21V2_a_client_left_running_closes_the_field_when_it_is_finished()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> prospect) = Field();

        // A HURDLE, which is how the decision is actually made: this operator
        // wants $4M a month from the field and closes it when it stops
        // delivering that. The shipped field is marginally profitable for well
        // over fifty years — running it to literal zero would outlast any run a
        // player would sit through, and "still positive" is not the same as
        // "worth keeping open" (R20.4's open question).
        Session session = new Operator(
            engine, prospect, wellTarget: 6, hurdle: Money.FromMillions(4.0)).Play(months: 480);

        Assert.True(session.WellsAbandoned > 0,
            "a field that has stopped paying must be closable through the surface alone");

        foreach (WellStatusView well in engine.ReadModel!.Wellbores)
            Assert.Equal(WellStatus.Abandoned, well.Status);
    }

    /// <summary>
    /// R21-V3/V4, structurally: the client holds no module reference at all. It
    /// cannot see a compartment, a completion or a separator, so anything it
    /// managed to do is something a host can do.
    ///
    /// <para>Asserted on the ASSEMBLY, because a reference added in a hurry
    /// would otherwise quietly turn this suite from a proof about the surface
    /// into a proof about nothing.</para>
    /// </summary>
    [Fact]
    public void R21V3_the_client_references_no_domain_module()
    {
        string[] referenced = [.. typeof(Operator).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)];

        foreach (string module in new[]
                 {
                     "OGSim.Subsurface", "OGSim.Wells", "OGSim.Facilities",
                     "OGSim.Flow", "OGSim.Information", "OGSim.Operations",
                 })
            Assert.DoesNotContain(module, referenced);
    }

    // ------------------------------------- one content set, many field sizes
    //
    // Finding 164. THE POINT of an aquifer stated as a strength: the shipped
    // content has to mean the same thing against a field of any size. It could
    // not before — a single engine-wide aquifer sized for the shipped field
    // repressurised a small one ABOVE its discovery pressure, which the
    // material balance refuses outright, so the small field faulted rather than
    // playing badly.

    /// <summary>
    /// The same content, fields two orders of magnitude apart, all of them
    /// playable. Not "all of them the same" — a small field should be a worse
    /// business, and the next test says so — but none of them impossible.
    /// </summary>
    [Theory]
    [InlineData(5.0e6)]
    [InlineData(100.0e6)]
    [InlineData(500.0e6)]
    public void R20d8V1_one_aquifer_setting_plays_against_any_field_size(double poreVolume)
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> prospect) = Field(poreVolume);

        Session session = new Operator(engine, prospect, wellTarget: 3, hurdle: Money.Zero)
            .Play(months: 240);

        // THE ASSERTION IS THAT THIS RAN. A field the shipped aquifer was wrong
        // for did not produce a poor result — it threw, because a compartment
        // pushed above its own discovery pressure is a state the material
        // balance will not solve for. Drilling is the evidence the run got
        // somewhere rather than falling over on tick one.
        Assert.True(session.WellsDrilled > 0,
            $"a field of {poreVolume} m³ pore volume never got a well down");
    }

    /// <summary>
    /// AND NOW THE SIZE IS THE STORY. A field ten times bigger is worth
    /// materially more to develop — which it was not before finding 165: both
    /// spent every month against one constant export line and earned within
    /// 0.3% of each other, the smaller one fractionally ahead.
    ///
    /// <para>Not ten times more, and it should not be. The line is bought with
    /// money the field has already made and takes months to lay, so a big
    /// accumulation spends its early years at the same rate a small one does and
    /// only pulls away once it has paid for the capacity to. That lag is the
    /// development decision — and it exists because what is in the ground and
    /// what lifts it are now two different things.</para>
    /// </summary>
    [Fact]
    public void R20d8V1_a_bigger_reservoir_is_a_bigger_business()
    {
        Money big = Earned(500.0e6), small = Earned(50.0e6);

        Assert.True(big > small,
            $"ten times the oil earned no more: {big} against {small}");
    }

    private static Money Earned(double poreVolume)
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> prospect) = Field(poreVolume);

        return new Operator(engine, prospect, wellTarget: 3, hurdle: Money.Zero)
            .Play(months: 240)
            .Cash;
    }
}
