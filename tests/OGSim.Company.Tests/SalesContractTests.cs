// R13-V10: take-or-pay penalty (SDD-009 §7's R13.3 amendment, finding 250).

using OGSim.Company;
using OGSim.Kernel;

namespace OGSim.Company.Tests;

public class TakeOrPayTests
{
    private static TakeOrPayTerms Terms(
        double committedM3 = 10_000.0, int windowMonths = 12, long penaltyRateCents = 5_000) =>
        new(new SurfaceVolume(committedM3), windowMonths, new Money(penaltyRateCents));

    private static TakeOrPayContract New(TakeOrPayTerms? terms = null, int startTick = 0) =>
        new(terms ?? Terms(), new Tick(startTick));

    [Fact] // Nothing is assessed before the window closes
    public void R13V10_the_window_is_not_assessed_before_it_closes()
    {
        TakeOrPayContract contract = New();

        Assert.Null(contract.AssessAt(new Tick(11)));
    }

    [Fact] // Delivering the full commitment costs nothing
    public void R13V10_meeting_the_commitment_costs_nothing()
    {
        TakeOrPayContract contract = New(Terms(committedM3: 10_000.0));

        contract.RecordDelivery(new SurfaceVolume(6_000.0));
        contract.RecordDelivery(new SurfaceVolume(4_000.0));

        TakeOrPayAssessment assessment = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(12)));

        Assert.Equal(10_000.0, assessment.Delivered.CubicMetres, 6);
        Assert.Equal(0.0, assessment.Shortfall.CubicMetres, 6);
        Assert.Equal(Money.Zero, assessment.Penalty);
    }

    [Fact] // Delivering MORE than the commitment is not a negative shortfall
    public void R13V10_over_delivery_is_not_a_negative_shortfall()
    {
        TakeOrPayContract contract = New(Terms(committedM3: 10_000.0));

        contract.RecordDelivery(new SurfaceVolume(15_000.0));

        TakeOrPayAssessment assessment = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(12)));

        Assert.Equal(0.0, assessment.Shortfall.CubicMetres, 6);
        Assert.Equal(Money.Zero, assessment.Penalty);
    }

    [Fact] // R13-V10's own claim: under-delivery triggers the contractual penalty
    public void R13V10_under_delivery_triggers_the_penalty_shortfall_times_rate()
    {
        TakeOrPayContract contract = New(Terms(
            committedM3: 10_000.0, windowMonths: 12, penaltyRateCents: 5_000));

        contract.RecordDelivery(new SurfaceVolume(3_000.0));

        TakeOrPayAssessment assessment = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(12)));

        Assert.Equal(3_000.0, assessment.Delivered.CubicMetres, 6);
        Assert.Equal(7_000.0, assessment.Shortfall.CubicMetres, 6);

        // shortfall (m3) x rate (cents/m3), recomputed independently.
        Assert.Equal(new Money(7_000L * 5_000L), assessment.Penalty);
    }

    [Fact] // Delivering NOTHING at all is the whole commitment as shortfall
    public void R13V10_delivering_nothing_owes_the_whole_commitment()
    {
        TakeOrPayContract contract = New(Terms(committedM3: 10_000.0, penaltyRateCents: 100));

        TakeOrPayAssessment assessment = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(12)));

        Assert.Equal(10_000.0, assessment.Shortfall.CubicMetres, 6);
        Assert.Equal(new Money(10_000L * 100L), assessment.Penalty);
    }

    [Fact] // Each window is independent — no latch, unlike a licence's bond
    public void R13V10_a_penalised_window_does_not_stop_the_next_one_being_assessed()
    {
        TakeOrPayContract contract = New(Terms(committedM3: 10_000.0, windowMonths: 12));

        contract.RecordDelivery(new SurfaceVolume(2_000.0));
        TakeOrPayAssessment first = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(12)));
        Assert.True(first.Penalty.Cents > 0);

        // Nothing more until the SECOND window closes.
        Assert.Null(contract.AssessAt(new Tick(13)));
        Assert.Null(contract.AssessAt(new Tick(23)));

        // The second window starts fresh at zero delivered — the first
        // window's shortfall does not carry forward, and meeting the second
        // window's commitment in full costs nothing even though the first
        // one was penalised.
        contract.RecordDelivery(new SurfaceVolume(10_000.0));
        TakeOrPayAssessment second = Assert.IsType<TakeOrPayAssessment>(
            contract.AssessAt(new Tick(24)));

        Assert.Equal(0.0, second.Shortfall.CubicMetres, 6);
        Assert.Equal(Money.Zero, second.Penalty);
    }

    [Theory] // Content errors are refused where the terms are still in hand
    [InlineData(0.0, 12, 100L, "commitment")]
    [InlineData(-1.0, 12, 100L, "commitment")]
    [InlineData(10_000.0, 0, 100L, "window")]
    [InlineData(10_000.0, -1, 100L, "window")]
    [InlineData(10_000.0, 12, -1L, "penalty rate")]
    public void R13V10_an_unusable_contract_is_a_model_fault(
        double committedM3, int windowMonths, long penaltyRateCents, string expected)
    {
        var fault = Assert.Throws<ModelFault>(
            () => New(Terms(committedM3, windowMonths, penaltyRateCents)));

        Assert.Contains(expected, fault.Fault.Detail);
    }

    [Fact] // A custody transfer cannot be un-recorded
    public void R13V10_a_negative_delivery_is_refused()
    {
        TakeOrPayContract contract = New();

        Assert.Throws<InvariantFault>(() => contract.RecordDelivery(new SurfaceVolume(-1.0)));
    }

    [Fact] // The window boundary is STATE (SDD-013 §4) — round-tripped exactly
    public void R13V10_capture_and_restore_round_trip_the_window_and_progress()
    {
        TakeOrPayContract contract = New(Terms(committedM3: 10_000.0));
        contract.RecordDelivery(new SurfaceVolume(4_000.0));

        var fields = new Dictionary<string, object>();
        contract.Capture(new RecordingWriter(fields));

        TakeOrPayContract restored = New();
        restored.Restore(new RecordingReader(fields));

        // The restored contract remembers the 4,000 already delivered: only
        // 6,000 more closes the window with nothing owed.
        restored.RecordDelivery(new SurfaceVolume(6_000.0));
        TakeOrPayAssessment assessment = Assert.IsType<TakeOrPayAssessment>(
            restored.AssessAt(new Tick(12)));

        Assert.Equal(0.0, assessment.Shortfall.CubicMetres, 6);
    }

    // ------------------------------------------------- minimal state fixture

    private sealed class RecordingWriter(Dictionary<string, object> fields) : IStateWriter
    {
        public void WriteDouble(string key, double value) => fields[key] = value;
        public void WriteInt64(string key, long value) => fields[key] = value;
        public void WriteString(string key, string value) => fields[key] = value;
    }

    private sealed class RecordingReader(Dictionary<string, object> fields) : IStateReader
    {
        public double ReadDouble(string key) => (double)fields[key];
        public long ReadInt64(string key) => (long)fields[key];
        public string ReadString(string key) => (string)fields[key];
    }
}
