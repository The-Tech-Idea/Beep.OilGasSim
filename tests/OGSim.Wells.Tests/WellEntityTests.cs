// R20d.9 — IWell and IWellbore, against the reachable subset (SDD-003 §5's
// R20d.9 amendment).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public class WellEntityTests
{
    private static Completion Well(EntityId<IReservoirCompartmentEntity> drains, double lengthM = 20.0) =>
        new(new EntityId<ICompletion>(1),
            new EntityId<IWellbore>(1),
            [Fixtures.Perf(lengthM: lengthM)],
            new CompositeInflowModel(Fixtures.Conditions()),
            new HydrostaticFrictionOutflowModel(
                Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift: null),
            new CompletionFluid(
                Density.FromSpecificGravity(0.85),
                new FormationVolumeFactor(1.2),
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, drains.Value)),
                new Pressure(30.0e6),
                Temperature.FromCelsius(93.3),
                Fx.GasDensity,
                Fx.NoSolutionGas,
                Fx.WaterDensity,
                Fx.Dry),
            ChokeSetting.Open,
            oilOrdinal: 0,
            gasOrdinal: 1,
            waterOrdinal: 2,
            materialCount: 3,
            lift: null);

    /// <summary>
    /// <b>Well is a pure value object</b> — everything a caller hands it comes
    /// back unchanged, which is the whole of what SDD-003 §5's identity half
    /// promises: no physics lives here.
    /// </summary>
    [Fact]
    public void A_well_carries_exactly_what_it_was_built_with()
    {
        var id = new EntityId<IWell>(7);
        var licence = new EntityId<ILicence>(1);
        var wellbore = new EntityId<IWellbore>(7);
        var surface = new Coordinate(0.0, 0.0);

        var well = new Well(
            id, WellStatus.Producing, WellClassification.Development, licence, surface,
            [wellbore]);

        Assert.Equal(id, well.Id);
        Assert.Equal(WellStatus.Producing, well.Status);
        Assert.Equal(WellClassification.Development, well.Classification);
        Assert.Equal(licence, well.Licence);
        Assert.Equal(surface, well.Surface);
        Assert.Equal([wellbore], well.Wellbores);
    }

    /// <summary>
    /// <b>A wellbore's path is what it was given</b>, and its <c>Well</c>
    /// back-reference is the id it was built against — the two facts a caller
    /// needs and nothing derived incorrectly from either.
    /// </summary>
    [Fact]
    public void A_wellbore_carries_its_path_and_its_well()
    {
        EntityId<IReservoirCompartmentEntity> drains = new(1);
        Completion completion = Well(drains);

        var wellId = new EntityId<IWell>(1);
        var wellboreId = new EntityId<IWellbore>(1);

        var path = new Trajectory(
        [
            new TrajectoryStation(new Length(0.0), new Length(0.0), new Coordinate(0.0, 0.0)),
            new TrajectoryStation(new Length(2020.0), new Length(2020.0), new Coordinate(0.0, 0.0)),
        ]);

        var wellbore = new Wellbore(wellboreId, wellId, path, completion);

        Assert.Equal(wellboreId, wellbore.Id);
        Assert.Equal(wellId, wellbore.Well);
        Assert.Equal(path, wellbore.Path);
        Assert.Equal([completion.CompletionId], wellbore.Completions);
    }

    /// <summary>
    /// <b>Contact length reads the completion's own perforation</b>, not a
    /// geometry-only derivation from <c>Path</c> — SDD-003 §5's R20d.9
    /// amendment states why: a compartment carries no depth interval of its
    /// own to check a Path-only answer against, so the perforation's
    /// <c>TopMd</c>/<c>BottomMd</c> is the one owner (law L5).
    /// </summary>
    [Fact]
    public void Contact_length_sums_the_perforations_draining_that_compartment()
    {
        EntityId<IReservoirCompartmentEntity> drains = new(1);
        Completion completion = Well(drains, lengthM: 35.0);

        var wellbore = new Wellbore(
            new EntityId<IWellbore>(1), new EntityId<IWell>(1),
            new Trajectory(
            [
                new TrajectoryStation(new Length(0.0), new Length(0.0), new Coordinate(0.0, 0.0)),
            ]),
            completion);

        Assert.Equal(35.0, wellbore.ContactLengthIn(drains).Metres, precision: 9);
    }

    /// <summary>A compartment this well does NOT drain reports zero contact —
    /// the sum over an empty match, not a fault.</summary>
    [Fact]
    public void Contact_length_in_an_unrelated_compartment_is_zero()
    {
        EntityId<IReservoirCompartmentEntity> drains = new(1);
        EntityId<IReservoirCompartmentEntity> elsewhere = new(2);
        Completion completion = Well(drains);

        var wellbore = new Wellbore(
            new EntityId<IWellbore>(1), new EntityId<IWell>(1),
            new Trajectory(
            [
                new TrajectoryStation(new Length(0.0), new Length(0.0), new Coordinate(0.0, 0.0)),
            ]),
            completion);

        Assert.Equal(0.0, wellbore.ContactLengthIn(elsewhere).Metres, precision: 9);
    }
}
