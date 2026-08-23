// SDD-007 §4.1's finding-265 amendment — the crew effect, unit-tested against
// the model directly rather than only through a played field.

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public sealed class CrewTests
{
    private static CrewState Fresh() => new(
        baseCompetency: 0.75, trainedCompetency: 0.95,
        baseDurationFactor: 1.15, trainedDurationFactor: 0.95,
        trainingCost: Money.FromMillions(2.0));

    [Fact] // An untrained crew starts weaker and slower than the trained figures
    public void An_untrained_crew_reads_the_base_figures()
    {
        CrewState crew = Fresh();

        Assert.False(crew.Trained);
        Assert.Equal(0.75, crew.Competency, precision: 12);
        Assert.Equal(1.15, crew.DurationFactor, precision: 12);
    }

    [Fact] // Training moves BOTH terms at once — the whole of R12-V9's claim
    public void Training_raises_competency_and_lowers_the_duration_factor_together()
    {
        CrewState crew = Fresh();

        crew.Train();

        Assert.True(crew.Trained);
        Assert.Equal(0.95, crew.Competency, precision: 12);
        Assert.Equal(0.95, crew.DurationFactor, precision: 12);
    }

    [Fact] // One-way, like a technology acquisition — a crew does not forget
    public void Training_twice_is_refused_at_the_model()
    {
        CrewState crew = Fresh();
        crew.Train();

        Assert.Throws<InvariantFault>(() => crew.Train());
    }

    [Theory] // A competency outside [0, 1] is not a strength on the barrier's own scale
    [InlineData(-0.1, 0.9)]
    [InlineData(0.5, 1.1)]
    public void An_incoherent_competency_is_refused_at_construction(double b, double t)
    {
        Assert.Throws<ContentFault>(() => new CrewState(
            b, t, baseDurationFactor: 1.15, trainedDurationFactor: 0.95,
            trainingCost: Money.FromMillions(1.0)));
    }

    [Fact] // Training that does not improve competency is not training
    public void Trained_competency_not_above_base_is_refused()
    {
        Assert.Throws<ContentFault>(() => new CrewState(
            baseCompetency: 0.9, trainedCompetency: 0.9,
            baseDurationFactor: 1.15, trainedDurationFactor: 0.95,
            trainingCost: Money.FromMillions(1.0)));
    }

    [Fact] // Training that does not improve duration is not training either
    public void Trained_duration_factor_not_below_base_is_refused()
    {
        Assert.Throws<ContentFault>(() => new CrewState(
            baseCompetency: 0.75, trainedCompetency: 0.95,
            baseDurationFactor: 1.0, trainedDurationFactor: 1.0,
            trainingCost: Money.FromMillions(1.0)));
    }

    [Fact] // A free improvement is not an investment decision
    public void A_zero_or_negative_training_cost_is_refused()
    {
        Assert.Throws<ContentFault>(() => new CrewState(
            0.75, 0.95, 1.15, 0.95, trainingCost: Money.Zero));
    }

    [Fact] // The one fact this owns survives a save
    public void Training_survives_a_capture_and_restore()
    {
        CrewState captured = Fresh();
        captured.Train();

        CrewState restored = Fresh();
        OGSim.Persistence.StateBlock.Restore(
            restored, OGSim.Persistence.StateBlock.Capture(captured).Written());

        Assert.True(restored.Trained);
        Assert.Equal(captured.Competency, restored.Competency, precision: 12);
    }
}
