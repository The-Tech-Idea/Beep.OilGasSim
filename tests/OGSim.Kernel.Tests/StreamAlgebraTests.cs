// R2.5 / R2.6 — the stream algebra (SDD-002 §2–4).
// R2-V3 mix/split round-trip preserves mass AND provenance exactly;
// R2-V4 randomised operation sequences conserve mass.
//
// R2-V1 (material agnosticism) and R2-V2 (no identity branching) are assertions
// about the ABSENCE of a branch and belong to the architecture suite — nothing
// here can prove a branch is missing.

using System.Collections.Immutable;
using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class StreamAlgebraTests
{
    private static Composition Comp(params double[] kgPerSecond) =>
        Composition.Validated([.. kgPerSecond]);

    private static EntityRef Compartment(ulong id) => new(EntityKind.Compartment, id);

    // ------------------------------------------------------------- composition

    [Fact] // Ascending-ordinal iteration and the absent-material rule
    public void R2V3_composition_indexes_by_ordinal_and_totals_in_order()
    {
        Composition oil = Comp(100.0, 20.0, 5.0);

        Assert.Equal(100.0, oil[new MaterialId(0)].KgPerSecond, 12);
        Assert.Equal(5.0, oil[new MaterialId(2)].KgPerSecond, 12);
        Assert.Equal(125.0, oil.Total.KgPerSecond, 12);

        // An ordinal past the end is ABSENT, not an error: a stream built
        // against a smaller catalogue is still readable.
        Assert.Equal(0.0, oil[new MaterialId(9)].KgPerSecond);
    }

    [Fact] // Negative mass means an element ran backwards — never clamped
    public void R2V4_negative_or_nonfinite_mass_is_refused()
    {
        var negative = Assert.Throws<InvariantFault>(() => Comp(10.0, -0.001));
        Assert.Contains("negative", negative.Fault.Detail);

        Assert.Throws<InvariantFault>(() => Comp(double.NaN));
        var infinite = Assert.Throws<InvariantFault>(() => Comp(double.PositiveInfinity));
        Assert.Equal("INV6", infinite.Fault.Rule);
    }

    [Fact] // Two catalogues means ordinal i is two different materials
    public void R2V4_compositions_of_different_lengths_cannot_be_added()
    {
        Assert.Throws<InvariantFault>(() => Comp(1.0, 2.0).Plus(Comp(1.0, 2.0, 3.0)));
    }

    [Fact] // R2-V3: split then mix returns the original EXACTLY, per material
    public void R2V3_split_then_mix_round_trips_to_the_bit()
    {
        foreach (double fraction in new[] { 0.0, 0.1, 1.0 / 3.0, 0.5, 0.7777777, 1.0 })
        {
            Composition original = Comp(137.035, 0.0, 2.718281828459045, 6.02e5);
            (Composition a, Composition b) = original.Split(fraction);

            Composition rejoined = a.Plus(b);
            for (int i = 0; i < original.Length; i++)
                Assert.Equal(original[new MaterialId(i)].KgPerSecond,
                             rejoined[new MaterialId(i)].KgPerSecond);

            // Structural equality, not array identity.
            Assert.Equal(original, rejoined);
        }
    }

    [Fact] // Scaled is for splits: above 1 it would create mass
    public void R2V4_scaled_refuses_a_factor_outside_zero_to_one()
    {
        Assert.Equal(50.0, Comp(100.0).Scaled(0.5).Total.KgPerSecond, 12);
        Assert.Throws<InvariantFault>(() => Comp(100.0).Scaled(1.5));
        Assert.Throws<InvariantFault>(() => Comp(100.0).Scaled(-0.1));
        Assert.Throws<InvariantFault>(() => Comp(100.0).Scaled(double.NaN));
    }

    [Fact] // R2-V4: a long randomised sequence of splits and mixes conserves mass
    public void R2V4_randomised_operation_sequences_conserve_mass()
    {
        var stream = new RandomSource(0xC0FFEE).Stream(StreamId.Measurement);
        Composition total = Comp(1000.0, 250.0, 60.0, 3.0);
        double expected = total.Total.KgPerSecond;

        var pieces = new List<Composition> { total };
        for (int step = 0; step < 500; step++)
        {
            int index = stream.NextInt(pieces.Count);
            (Composition a, Composition b) = pieces[index].Split(stream.NextUnit());
            pieces[index] = a;
            pieces.Add(b);
        }

        // Re-mix everything back together.
        Composition rejoined = pieces[0];
        for (int i = 1; i < pieces.Count; i++) rejoined = rejoined.Plus(pieces[i]);

        Assert.Equal(expected, rejoined.Total.KgPerSecond, 9);
    }

    [Fact] // default(Composition) must fault rather than read as an empty stream
    public void R2V4_default_composition_faults()
    {
        Composition uninitialised = default;
        Assert.Throws<InvariantFault>(() => uninitialised.Total);
        Assert.Throws<InvariantFault>(() => uninitialised[new MaterialId(0)]);
        Assert.Throws<InvariantFault>(() => uninitialised.Plus(Comp(1.0)));
    }

    // ------------------------------------------------------------- allocation

    [Fact] // Sorted, positive, summing to one — the invariant in three parts
    public void R2V3_allocation_enforces_its_invariant()
    {
        Allocation valid = Allocation.Validated(
            [(Compartment(1), 0.25), (Compartment(2), 0.75)]);
        Assert.Equal(2, valid.Shares.Length);

        // Out of order.
        Assert.Throws<InvariantFault>(() => Allocation.Validated(
            [(Compartment(2), 0.5), (Compartment(1), 0.5)]));

        // A zero share is an ABSENT share, not a share of nothing.
        Assert.Throws<InvariantFault>(() => Allocation.Validated(
            [(Compartment(1), 1.0), (Compartment(2), 0.0)]));

        // Duplicated compartment.
        Assert.Throws<InvariantFault>(() => Allocation.Validated(
            [(Compartment(1), 0.5), (Compartment(1), 0.5)]));
    }

    [Fact] // Small drift is floating point; large drift is misattributed mass
    public void R2V3_allocation_renormalises_small_drift_and_faults_on_large()
    {
        // Within 1e-9: renormalised silently.
        Allocation drifted = Allocation.Validated(
            [(Compartment(1), 0.5), (Compartment(2), 0.5 + 1e-11)]);
        double sum = 0.0;
        for (int i = 0; i < drifted.Shares.Length; i++) sum += drifted.Shares[i].Fraction;
        Assert.Equal(1.0, sum, 12);

        // Beyond it: a fault, never a quiet fix.
        var fault = Assert.Throws<InvariantFault>(() => Allocation.Validated(
            [(Compartment(1), 0.5), (Compartment(2), 0.4)]));
        Assert.Contains("drift", fault.Fault.Detail);
    }

    [Fact] // R2-V3: blending is mass-weighted, and it is the tank-receipt case
    public void R2V3_blend_is_mass_weighted()
    {
        Allocation fromA = Allocation.FromSingle(Compartment(1));
        Allocation fromB = Allocation.FromSingle(Compartment(2));

        Allocation blended = Allocation.Blend(
        [
            (fromA, new Mass(750.0)),
            (fromB, new Mass(250.0)),
        ]);

        Assert.Equal(2, blended.Shares.Length);
        Assert.Equal(Compartment(1), blended.Shares[0].Compartment);   // ascending order
        Assert.Equal(0.75, blended.Shares[0].Fraction, 12);
        Assert.Equal(0.25, blended.Shares[1].Fraction, 12);
    }

    [Fact] // An empty delivery must not shift an existing blend
    public void R2V3_a_zero_mass_part_does_not_move_the_blend()
    {
        Allocation existing = Allocation.Validated(
            [(Compartment(1), 0.6), (Compartment(2), 0.4)]);

        Allocation after = Allocation.Blend(
        [
            (existing, new Mass(1000.0)),
            (Allocation.FromSingle(Compartment(3)), new Mass(0.0)),
        ]);

        Assert.Equal(existing, after);
    }

    [Fact] // Blending overlapping provenances merges rather than duplicates
    public void R2V3_blend_merges_shared_compartments()
    {
        Allocation first = Allocation.Validated([(Compartment(1), 0.5), (Compartment(2), 0.5)]);
        Allocation second = Allocation.Validated([(Compartment(2), 0.5), (Compartment(3), 0.5)]);

        Allocation blended = Allocation.Blend(
            [(first, new Mass(100.0)), (second, new Mass(100.0))]);

        Assert.Equal(3, blended.Shares.Length);
        Assert.Equal(0.25, blended.Shares[0].Fraction, 12);   // compartment 1
        Assert.Equal(0.50, blended.Shares[1].Fraction, 12);   // compartment 2, from both
        Assert.Equal(0.25, blended.Shares[2].Fraction, 12);   // compartment 3
    }

    [Fact] // Blending nothing has no defined answer
    public void R2V3_blend_requires_mass()
    {
        Assert.Throws<InvariantFault>(() => Allocation.Blend(
            [(Allocation.FromSingle(Compartment(1)), new Mass(0.0))]));

        Assert.Throws<InvariantFault>(() =>
            Allocation.Blend(ReadOnlySpan<(Allocation, Mass)>.Empty));
    }

    // ------------------------------------------------------------- stream

    [Fact] // R2-V3: splitting a stream changes how much, never what or whence
    public void R2V3_splitting_a_stream_preserves_conditions_and_provenance()
    {
        var provenance = Allocation.Validated([(Compartment(1), 0.3), (Compartment(2), 0.7)]);
        var stream = new MaterialStream(
            Comp(80.0, 20.0), Pressure.FromBar(45.0), Temperature.FromCelsius(60.0), provenance);

        (MaterialStream a, MaterialStream b) = stream.Split(0.25);

        Assert.Equal(stream.P, a.P);
        Assert.Equal(stream.T, b.T);
        Assert.Equal(provenance, a.Provenance);
        Assert.Equal(provenance, b.Provenance);

        Assert.Equal(25.0, a.MassRates.Total.KgPerSecond, 9);
        Assert.Equal(75.0, b.MassRates.Total.KgPerSecond, 9);
        Assert.Equal(stream.MassRates, a.MassRates.Plus(b.MassRates));
    }
}
