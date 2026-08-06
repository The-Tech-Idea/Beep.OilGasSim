// SDD-002 §2–4 — the one currency every module trades in.
//
// R2.5/R2.6 implement the algebra the contract stage declared as data shapes.
// Two properties do all the work downstream: mass is what the engine conserves
// (04 §2.1), so the arithmetic here is the arithmetic INV1 checks; and
// provenance rides along with it, so allocation, royalty and depletion never
// need a separate subsystem to reconstruct where a barrel came from.

using System.Collections.Immutable;

namespace OGSim.Kernel;

/// <summary>Catalogue-ordinal material index. Ordinals NEVER persist (SDD-004 §6).</summary>
public readonly record struct MaterialId(int Ordinal);

/// <summary>
/// Immutable mass-flow-per-material, kg/s, dense by material ordinal
/// (SDD-002 §2). Values are never negative.
///
/// Dense array rather than a dictionary: the catalogue is small (~12 shipped
/// materials), streams are created constantly, and ordinal indexing gives
/// deterministic iteration for free — rule D-5 satisfied by the data structure
/// instead of by remembering to sort.
/// </summary>
public readonly record struct Composition(ImmutableArray<double> KgPerSecondByOrdinal)
{
    /// <summary>Absent material is 0.0 — an ordinal past the end reads as absent
    /// rather than throwing, so a stream built against a smaller catalogue is
    /// still readable. A NEGATIVE value is not tolerated anywhere (see Validated).</summary>
    public MassRate this[MaterialId material]
    {
        get
        {
            ImmutableArray<double> values = Guarded();
            int ordinal = material.Ordinal;
            if (ordinal < 0)
                throw new InvariantFault("SDD-002 §2", null,
                    $"Material ordinal {ordinal} is negative.");
            return new MassRate(ordinal < values.Length ? values[ordinal] : 0.0);
        }
    }

    public int Length => Guarded().Length;

    public MassRate Total
    {
        get
        {
            ImmutableArray<double> values = Guarded();
            double total = 0.0;
            // Ascending ordinal, always: the summation order is part of the
            // determinism guarantee, not an implementation detail (D-5).
            for (int i = 0; i < values.Length; i++) total += values[i];
            return new MassRate(total);
        }
    }

    /// <summary>
    /// Validates and returns. Negative mass flow is an invariant fault rather
    /// than a clamp: a negative here means an element's transform produced mass
    /// from nothing in reverse, and clamping would hide exactly the defect
    /// INV1 exists to catch.
    /// </summary>
    public static Composition Validated(ImmutableArray<double> kgPerSecondByOrdinal)
    {
        if (kgPerSecondByOrdinal.IsDefault)
            throw new InvariantFault("SDD-002 §2", null, "Composition requires a backing array.");

        for (int i = 0; i < kgPerSecondByOrdinal.Length; i++)
        {
            double value = kgPerSecondByOrdinal[i];
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvariantFault("INV6", null,
                    $"Composition ordinal {i} is {value}; NaN and infinity never reach a commit.");
            if (value < 0.0)
                throw new InvariantFault("SDD-002 §2", null,
                    $"Composition ordinal {i} is negative ({value} kg/s).");
        }

        return new Composition(kgPerSecondByOrdinal);
    }

    public static Composition Zero(int materialCount) =>
        new([.. new double[materialCount]]);

    /// <summary>Mixing, per material. Lengths must match — two compositions from
    /// different catalogues cannot be added, because ordinal <i>i</i> would mean
    /// two different materials.</summary>
    public Composition Plus(in Composition other)
    {
        ImmutableArray<double> mine = Guarded();
        ImmutableArray<double> theirs = other.Guarded();

        if (mine.Length != theirs.Length)
            throw new InvariantFault("SDD-002 §2", null,
                $"Composition lengths differ ({mine.Length} vs {theirs.Length}); " +
                "ordinals would refer to different materials.");

        var sum = new double[mine.Length];
        for (int i = 0; i < mine.Length; i++) sum[i] = mine[i] + theirs[i];
        return new Composition([.. sum]);
    }

    /// <summary>
    /// Scale by a fraction. SDD-002 §2 pins factor ∈ [0,1]: this exists for
    /// SPLITS, and a factor above 1 would be creating mass through an operation
    /// whose name promises the opposite.
    /// </summary>
    public Composition Scaled(double factor)
    {
        if (double.IsNaN(factor) || factor < 0.0 || factor > 1.0)
            throw new InvariantFault("SDD-002 §2", null,
                $"Composition.Scaled requires a factor in [0,1]; got {factor}.");

        ImmutableArray<double> values = Guarded();
        var scaled = new double[values.Length];
        for (int i = 0; i < values.Length; i++) scaled[i] = values[i] * factor;
        return new Composition([.. scaled]);
    }

    /// <summary>
    /// Split into two parts that sum back to the original EXACTLY per material:
    /// b is computed as the remainder rather than as (1−f)·x, so mix(split(x))
    /// round-trips to the bit and FV-class conservation checks do not have to
    /// carry a tolerance for the split itself.
    /// </summary>
    public (Composition A, Composition B) Split(double fractionToA)
    {
        if (double.IsNaN(fractionToA) || fractionToA < 0.0 || fractionToA > 1.0)
            throw new InvariantFault("SDD-002 §2", null,
                $"Composition.Split requires a fraction in [0,1]; got {fractionToA}.");

        ImmutableArray<double> values = Guarded();
        var a = new double[values.Length];
        var b = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            a[i] = values[i] * fractionToA;
            double remainder = values[i] - a[i];
            b[i] = remainder < 0.0 ? 0.0 : remainder;   // rounding can only undershoot
        }
        return (new Composition([.. a]), new Composition([.. b]));
    }

    /// <summary>Structural, not by array reference: two compositions carrying the
    /// same masses ARE the same composition, and conservation checks compare them.</summary>
    public bool Equals(Composition other)
    {
        if (KgPerSecondByOrdinal.IsDefault || other.KgPerSecondByOrdinal.IsDefault)
            return KgPerSecondByOrdinal.IsDefault && other.KgPerSecondByOrdinal.IsDefault;
        if (KgPerSecondByOrdinal.Length != other.KgPerSecondByOrdinal.Length) return false;
        for (int i = 0; i < KgPerSecondByOrdinal.Length; i++)
            if (KgPerSecondByOrdinal[i] != other.KgPerSecondByOrdinal[i]) return false;
        return true;
    }

    public override int GetHashCode()
    {
        if (KgPerSecondByOrdinal.IsDefault) return 0;
        var hash = new HashCode();
        for (int i = 0; i < KgPerSecondByOrdinal.Length; i++) hash.Add(KgPerSecondByOrdinal[i]);
        return hash.ToHashCode();
    }

    private ImmutableArray<double> Guarded() =>
        KgPerSecondByOrdinal.IsDefault
            ? throw new InvariantFault("SDD-002 §2", null,
                "default(Composition) has no backing array; a composition must be constructed.")
            : KgPerSecondByOrdinal;
}

/// <summary>
/// Provenance: sorted (compartment, fraction) pairs summing to 1 (SDD-002 §3).
///
/// Carrying this in the stream costs one small array and removes the need for a
/// separate allocation subsystem later — royalties, working interests and
/// reserves depletion all need the answer, and reconstructing it after the fact
/// is the industry's own hardest bookkeeping problem.
/// </summary>
public readonly record struct Allocation(
    ImmutableArray<(EntityRef Compartment, double Fraction)> Shares)
{
    private const double SumTolerance = 1e-12;
    private const double RenormalisationLimit = 1e-9;

    /// <summary>
    /// Validates, renormalises and returns. A drift beyond 1e-9 is an INVARIANT
    /// FAULT rather than a silent fix: small drift is floating-point, but a
    /// large one means mass was attributed to a compartment that did not supply
    /// it, and renormalising would launder that into a plausible-looking split.
    /// </summary>
    public static Allocation Validated(ImmutableArray<(EntityRef Compartment, double Fraction)> shares)
    {
        if (shares.IsDefault || shares.Length == 0)
            throw new InvariantFault("SDD-002 §3", null, "Allocation requires at least one share.");

        double sum = 0.0;
        for (int i = 0; i < shares.Length; i++)
        {
            (EntityRef compartment, double fraction) = shares[i];

            if (double.IsNaN(fraction) || fraction <= 0.0)
                throw new InvariantFault("SDD-002 §3", compartment,
                    $"Allocation share {fraction} is not positive; a zero share is an ABSENT share.");

            if (i > 0 && shares[i - 1].Compartment.CompareTo(compartment) >= 0)
                throw new InvariantFault("SDD-002 §3", compartment,
                    "Allocation shares must be sorted ascending by compartment, without duplicates.");

            sum += fraction;
        }

        double drift = Math.Abs(sum - 1.0);
        if (drift > RenormalisationLimit)
            throw new InvariantFault("SDD-002 §3", null,
                $"Allocation fractions sum to {sum}; drift {drift} exceeds {RenormalisationLimit}.");

        if (drift <= SumTolerance) return new Allocation(shares);

        var normalised = new (EntityRef, double)[shares.Length];
        for (int i = 0; i < shares.Length; i++)
            normalised[i] = (shares[i].Compartment, shares[i].Fraction / sum);
        return new Allocation([.. normalised]);
    }

    public static Allocation FromSingle(EntityRef compartment) =>
        new([(compartment, 1.0)]);

    /// <summary>
    /// Mass-weighted merge of the sorted arrays (SDD-002 §3) — what a tank does
    /// on every receipt and a manifold on every commingle. Parts with zero mass
    /// contribute nothing: an empty delivery must not shift the blend.
    /// </summary>
    public static Allocation Blend(ReadOnlySpan<(Allocation Part, Mass Weight)> parts)
    {
        if (parts.Length == 0)
            throw new InvariantFault("SDD-002 §3", null, "Blend requires at least one part.");

        double totalWeight = 0.0;
        for (int i = 0; i < parts.Length; i++)
        {
            double weight = parts[i].Weight.Kilograms;
            if (double.IsNaN(weight) || weight < 0.0)
                throw new InvariantFault("SDD-002 §3", null,
                    $"Blend weight {weight} kg is not a non-negative mass.");
            totalWeight += weight;
        }

        if (totalWeight <= 0.0)
            throw new InvariantFault("SDD-002 §3", null,
                "Blend has no mass; the resulting provenance would be undefined.");

        // Accumulate by compartment. SortedDictionary, not Dictionary: the
        // result must come out in ascending compartment order (D-5), and that
        // ordering is the Allocation invariant rather than a nicety.
        var weighted = new SortedDictionary<EntityRef, double>();
        for (int i = 0; i < parts.Length; i++)
        {
            double weight = parts[i].Weight.Kilograms;
            if (weight <= 0.0) continue;

            ImmutableArray<(EntityRef Compartment, double Fraction)> shares = parts[i].Part.Guarded();
            for (int s = 0; s < shares.Length; s++)
            {
                (EntityRef compartment, double fraction) = shares[s];
                double contribution = fraction * weight / totalWeight;
                weighted[compartment] = weighted.TryGetValue(compartment, out double running)
                    ? running + contribution
                    : contribution;
            }
        }

        var merged = new (EntityRef, double)[weighted.Count];
        int index = 0;
        foreach (KeyValuePair<EntityRef, double> pair in weighted)
            merged[index++] = (pair.Key, pair.Value);

        return Validated([.. merged]);
    }

    public bool Equals(Allocation other)
    {
        if (Shares.IsDefault || other.Shares.IsDefault)
            return Shares.IsDefault && other.Shares.IsDefault;
        if (Shares.Length != other.Shares.Length) return false;
        for (int i = 0; i < Shares.Length; i++)
            if (Shares[i].Compartment != other.Shares[i].Compartment
                || Shares[i].Fraction != other.Shares[i].Fraction) return false;
        return true;
    }

    public override int GetHashCode()
    {
        if (Shares.IsDefault) return 0;
        var hash = new HashCode();
        for (int i = 0; i < Shares.Length; i++)
        {
            hash.Add(Shares[i].Compartment);
            hash.Add(Shares[i].Fraction);
        }
        return hash.ToHashCode();
    }

    private ImmutableArray<(EntityRef Compartment, double Fraction)> Guarded() =>
        Shares.IsDefault
            ? throw new InvariantFault("SDD-002 §3", null,
                "default(Allocation) has no shares; an allocation must be constructed.")
            : Shares;
}

/// <summary>
/// Material in motion (SDD-002 §4; renamed from `Stream` — the first rule-F-4
/// event: the pinned name collides with System.IO.Stream under implicit
/// usings). Deliberately NO cached phase split —
/// elements ask the fluid model at (composition, P, T).
/// </summary>
public readonly record struct MaterialStream(
    Composition MassRates,
    Pressure P,
    Temperature T,
    Allocation Provenance)
{
    /// <summary>
    /// Split a flow in two. Pressure, temperature and provenance are UNCHANGED
    /// in both parts: splitting a stream does not change what it is or where it
    /// came from, only how much of it goes each way (SDD-002 §4, R2-V3).
    /// </summary>
    public (MaterialStream A, MaterialStream B) Split(double fractionToA)
    {
        (Composition a, Composition b) = MassRates.Split(fractionToA);
        return (this with { MassRates = a }, this with { MassRates = b });
    }
}
