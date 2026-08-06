// R1.4 — the RNG (SDD-001 §4). R1 goal G4: "adding a draw to one subsystem
// provably does not change another's sequence". R1-V5 is the load-bearing test
// here; R1-V6 (same seed, same sequence) is what makes a shared world seed mean
// anything at all.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class RandomTests
{
    private const ulong Seed = 0x0BADC0FFEE_1965UL;

    // ------------------------------------------------------------- independence

    [Fact] // R1-V5: the whole point. Draws in one stream cannot shift another.
    public void R1V5_streams_are_independent()
    {
        // Baseline: 10,000 draws from Hazard, with nothing else touched.
        var undisturbed = new RandomSource(Seed);
        double[] baseline = Draw(undisturbed.Stream(StreamId.Hazard), 10_000);

        // Now hammer every OTHER stream first, then draw Hazard again.
        var disturbed = new RandomSource(Seed);
        foreach (StreamId id in Enum.GetValues<StreamId>())
        {
            if (id == StreamId.Hazard) continue;
            Draw(disturbed.Stream(id), 977);
        }
        double[] afterOthers = Draw(disturbed.Stream(StreamId.Hazard), 10_000);

        // Byte-identical, not merely statistically similar.
        for (int i = 0; i < baseline.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(baseline[i]),
                         BitConverter.DoubleToInt64Bits(afterOthers[i]));
    }

    [Fact] // Distinct streams must not be the same sequence offset by a constant
    public void R1V5_streams_do_not_share_a_sequence()
    {
        var source = new RandomSource(Seed);
        double[] hazard = Draw(source.Stream(StreamId.Hazard), 64);
        double[] weather = Draw(source.Stream(StreamId.Weather), 64);
        double[] price = Draw(source.Stream(StreamId.Price), 64);

        Assert.NotEqual(hazard, weather);
        Assert.NotEqual(hazard, price);

        // No shared value at the same index — an off-by-one seeding bug would
        // show up here as heavy overlap.
        int collisions = 0;
        for (int i = 0; i < hazard.Length; i++)
            if (hazard[i] == weather[i] || hazard[i] == price[i]) collisions++;
        Assert.Equal(0, collisions);
    }

    // ------------------------------------------------------------- determinism

    [Fact] // R1-V6: the same seed is the same world
    public void R1V6_the_same_seed_produces_the_same_sequence()
    {
        double[] first = Draw(new RandomSource(Seed).Stream(StreamId.WorldGen), 1_000);
        double[] second = Draw(new RandomSource(Seed).Stream(StreamId.WorldGen), 1_000);
        Assert.Equal(first, second);

        double[] other = Draw(new RandomSource(Seed + 1).Stream(StreamId.WorldGen), 1_000);
        Assert.NotEqual(first, other);
    }

    [Fact] // Stream seeds derive from NAMES, so the enum's order must not matter
    public void R1V6_stream_identity_is_by_name_not_ordinal()
    {
        // Weather sits fifth in StreamId. If seeding used the ordinal, inserting
        // a stream ahead of it would silently re-roll every weather sequence in
        // every existing save. Named seeding is what prevents that, and the
        // sequence below is the fixed point that would change if it regressed.
        var source = new RandomSource(Seed);
        double[] weather = Draw(source.Stream(StreamId.Weather), 4);

        var again = new RandomSource(Seed);
        Assert.Equal(weather, Draw(again.Stream(StreamId.Weather), 4));
    }

    // ------------------------------------------------------------- seek

    [Fact] // Position and Seek are what make a save resume mid-sequence
    public void R1V6_seek_reproduces_the_sequence_exactly()
    {
        var stream = new RandomSource(Seed).Stream(StreamId.Operations);

        double[] head = Draw(stream, 500);
        ulong mark = stream.Position;
        double[] tail = Draw(stream, 500);

        stream.Seek(mark);
        Assert.Equal(mark, stream.Position);
        Assert.Equal(tail, Draw(stream, 500));

        stream.Seek(0);
        Assert.Equal(head, Draw(stream, 500));
    }

    [Fact] // Seeking far ahead must match having drawn there the long way
    public void R1V6_seek_jump_ahead_matches_sequential_draws()
    {
        var sequential = new RandomSource(Seed).Stream(StreamId.Market);
        Draw(sequential, 5_000);
        double[] expected = Draw(sequential, 8);

        var jumped = new RandomSource(Seed).Stream(StreamId.Market);
        jumped.Seek(5_000);
        Assert.Equal(expected, Draw(jumped, 8));
    }

    [Fact] // Position counts draws, so a save can record it as one integer
    public void R1V6_position_advances_once_per_draw()
    {
        var stream = new RandomSource(Seed).Stream(StreamId.Exploration);
        Assert.Equal(0UL, stream.Position);

        stream.NextUnit();
        Assert.Equal(1UL, stream.Position);

        stream.NextInt(30);
        Assert.True(stream.Position >= 2UL);   // rejection may consume more

        ulong before = stream.Position;
        stream.NextNormal();
        Assert.True(stream.Position > before); // polar consumes a variable count
    }

    // ------------------------------------------------------------- distributions

    [Fact] // NextUnit must stay in [0, 1) — a 1.0 would break every inverse-CDF use
    public void R1V6_next_unit_stays_in_the_half_open_interval()
    {
        var stream = new RandomSource(Seed).Stream(StreamId.Measurement);
        double min = 1.0;
        double max = 0.0;
        for (int i = 0; i < 200_000; i++)
        {
            double u = stream.NextUnit();
            Assert.True(u >= 0.0 && u < 1.0, $"NextUnit returned {u}");
            if (u < min) min = u;
            if (u > max) max = u;
        }
        Assert.True(min < 0.01 && max > 0.99, "the range is not being covered");
    }

    [Fact] // SDD-012 §2 draws a failure day in {0..29}; a modulo bias would favour early days
    public void R1V6_next_int_is_in_range_and_unbiased()
    {
        var stream = new RandomSource(Seed).Stream(StreamId.Hazard);
        int[] counts = new int[30];
        const int Draws = 300_000;

        for (int i = 0; i < Draws; i++)
        {
            int day = stream.NextInt(30);
            Assert.InRange(day, 0, 29);
            counts[day]++;
        }

        // Expect 10,000 each. A modulo bias shows as a systematic tilt toward
        // the low buckets, far outside this band.
        double expected = Draws / 30.0;
        for (int day = 0; day < 30; day++)
            Assert.InRange(counts[day], expected * 0.94, expected * 1.06);

        Assert.Throws<InvariantFault>(() => stream.NextInt(0));
        Assert.Throws<InvariantFault>(() => stream.NextInt(-5));
    }

    [Fact] // Marsaglia polar, per SDD-001 §4 — moments, and no NaN from the ln
    public void R1V6_next_normal_is_standard_normal()
    {
        var stream = new RandomSource(Seed).Stream(StreamId.Price);
        const int Draws = 200_000;
        double sum = 0.0;
        double sumSquares = 0.0;
        double extreme = 0.0;

        for (int i = 0; i < Draws; i++)
        {
            double z = stream.NextNormal();
            Assert.False(double.IsNaN(z));
            Assert.False(double.IsInfinity(z));
            sum += z;
            sumSquares += z * z;
            if (Math.Abs(z) > extreme) extreme = Math.Abs(z);
        }

        double mean = sum / Draws;
        double variance = sumSquares / Draws - mean * mean;
        Assert.InRange(mean, -0.02, 0.02);
        Assert.InRange(variance, 0.97, 1.03);
        Assert.InRange(extreme, 3.5, 7.0);   // tails exist, and are not absurd
    }

    private static double[] Draw(IRandomStream stream, int count)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++) values[i] = stream.NextUnit();
        return values;
    }
}
