// R4.8 — synthetic flow elements (SDD-002 §5, R4 §2).
//
// The whole argument for R4 preceding R5 is here: the solver is proven against
// elements with NO domain meaning, so its correctness is established
// independently of any reservoir or well modelling. If the solver were only ever
// exercised through a real completion, a solver bug and a PVT bug would be
// indistinguishable.
//
// Two materials throughout — ordinal 0 "oil", ordinal 1 "gas" — because one
// material cannot catch a per-material conservation error and three add nothing.

using OGSim.Contracts;
using OGSim.Flow;
using OGSim.Kernel;

namespace OGSim.Flow.Tests;

internal static class Synthetic
{
    public const int MaterialCount = 2;

    public static Composition Comp(double oil, double gas) =>
        Composition.Validated([oil, gas]);

    public static Composition Zero => Composition.Zero(MaterialCount);

    public static DisposedMass NoDisposal => new(Zero, Zero, Zero);

    public static Allocation OneCompartment { get; } =
        Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1));

    public static Allocation From(ulong compartment) =>
        Allocation.FromSingle(new EntityRef(EntityKind.Compartment, compartment));

    public static MaterialStream Stream(double oil, double gas, double bar = 40.0) =>
        new(Comp(oil, gas), Pressure.FromBar(bar), Temperature.FromCelsius(60.0), OneCompartment);

    public static PortSpec Inlet(int index, PortRole role = PortRole.Main) =>
        new(new PortId(index), PortDirection.Inlet, role);

    public static PortSpec Outlet(int index, PortRole role = PortRole.Main) =>
        new(new PortId(index), PortDirection.Outlet, role);
}

/// <summary>Mass enters the network here. No inlets, so its output is Sourced.</summary>
internal sealed class Source(ulong id, double oil, double gas, double bar = 40.0) : IFlowElement
{
    public EntityId<IFlowElement> Id { get; } = new(id);
    public double Oil { get; set; } = oil;
    public double Gas { get; set; } = gas;

    /// <summary>Which compartment this mass is credited to — FV10 needs two
    /// distinct ones or a blend cannot be told from a pass-through.</summary>
    public ulong Compartment { get; init; } = 1;

    public IReadOnlyList<PortSpec> Ports { get; } = [Synthetic.Outlet(0)];

    public TransformResult Transform(TransformInput input)
    {
        Composition produced = Synthetic.Comp(Oil, Gas);
        return new TransformResult(
            [new MaterialStream(produced, Pressure.FromBar(bar),
                                Temperature.FromCelsius(60.0), Synthetic.From(Compartment))],
            Sourced: produced,                     // conservation: 0 in + Sourced = out
            FuelConsumed: Synthetic.Zero,
            Disposed: Synthetic.NoDisposal,
            PowerDraw: new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}

/// <summary>Mass leaves here. One inlet, no outlets — everything is Disposed.</summary>
internal sealed class Sink(ulong id) : IFlowElement
{
    public EntityId<IFlowElement> Id { get; } = new(id);
    public Composition Received { get; private set; } = Synthetic.Zero;

    public IReadOnlyList<PortSpec> Ports { get; } = [Synthetic.Inlet(0)];

    public TransformResult Transform(TransformInput input)
    {
        Composition received = input.Inlets.Count > 0 ? input.Inlets[0].MassRates : Synthetic.Zero;
        Received = received;

        // A sink conserves by DISCHARGING what it took: mass leaving the network
        // is still accounted for, which is what makes 04 §7's balance closeable.
        return new TransformResult(
            [], Synthetic.Zero, Synthetic.Zero,
            new DisposedMass(Synthetic.Zero, Synthetic.Zero, received),
            new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}

/// <summary>
/// Passes everything through but reports a capacity. The element the solver
/// throttles against — FV5's backpressure and FV7's bottleneck both run on this.
/// </summary>
internal sealed class Restrictor(ulong id, double capacityKgPerSecond) : IFlowElement
{
    public EntityId<IFlowElement> Id { get; } = new(id);
    public double Capacity { get; set; } = capacityKgPerSecond;
    public double DropBar { get; init; } = 5.0;

    public IReadOnlyList<PortSpec> Ports { get; } = [Synthetic.Inlet(0), Synthetic.Outlet(1)];

    public TransformResult Transform(TransformInput input)
    {
        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : Synthetic.Stream(0.0, 0.0);

        // Pass mass through unchanged; drop the pressure. Throttling is the
        // SOLVER's job via the constraint below, never the element's — an
        // element that silently discarded mass could not conserve.
        var outlet = inlet with
        {
            P = new Pressure(Math.Max(0.0, inlet.P.Pascals - DropBar * 1e5)),
        };

        return new TransformResult(
            [outlet], Synthetic.Zero, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        double load = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates.Total.KgPerSecond
            : 0.0;
        return [new ConstraintEvaluation(ConstraintKind.TotalCapacity, Capacity, load)];
    }
}

/// <summary>Splits one inlet into two outlets by a fixed fraction.</summary>
internal sealed class Splitter(ulong id, double fractionToFirst) : IFlowElement
{
    public EntityId<IFlowElement> Id { get; } = new(id);

    public IReadOnlyList<PortSpec> Ports { get; } =
        [Synthetic.Inlet(0), Synthetic.Outlet(1), Synthetic.Outlet(2)];

    public TransformResult Transform(TransformInput input)
    {
        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : Synthetic.Stream(0.0, 0.0);

        // Composition.Split computes the second part as the REMAINDER, so the
        // two outlets sum back to the inlet exactly and this element's own
        // conservation check cannot fail on rounding.
        (MaterialStream a, MaterialStream b) = inlet.Split(fractionToFirst);

        return new TransformResult(
            [a, b], Synthetic.Zero, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}

/// <summary>Merges two inlets. The one place provenance blends (SDD-002 §3).</summary>
internal sealed class Manifold(ulong id) : IFlowElement
{
    public EntityId<IFlowElement> Id { get; } = new(id);

    public IReadOnlyList<PortSpec> Ports { get; } =
        [Synthetic.Inlet(0), Synthetic.Inlet(1), Synthetic.Outlet(2)];

    public TransformResult Transform(TransformInput input)
    {
        Composition total = Synthetic.Zero;
        var parts = new List<(Allocation Part, Mass Weight)>(input.Inlets.Count);

        for (int i = 0; i < input.Inlets.Count; i++)
        {
            total = total.Plus(input.Inlets[i].MassRates);
            parts.Add((input.Inlets[i].Provenance,
                       new Mass(input.Inlets[i].MassRates.Total.KgPerSecond)));
        }

        Allocation blended = total.Total.KgPerSecond > 0.0
            ? Allocation.Blend(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(parts))
            : Synthetic.OneCompartment;

        // Commingling takes the LOWEST inlet pressure: a manifold cannot push a
        // stream above the pressure of the line feeding it.
        double pressure = double.MaxValue;
        for (int i = 0; i < input.Inlets.Count; i++)
            pressure = Math.Min(pressure, input.Inlets[i].P.Pascals);
        if (input.Inlets.Count == 0) pressure = Pressure.FromBar(40.0).Pascals;

        return new TransformResult(
            [new MaterialStream(total, new Pressure(pressure),
                                Temperature.FromCelsius(60.0), blended)],
            Synthetic.Zero, Synthetic.Zero, Synthetic.NoDisposal, new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}

/// <summary>
/// A completion whose IPR is a straight line from a declared reservoir pressure —
/// enough to exercise S1's damping, S4's backpressure coupling and the DEAD
/// outcome, with no PVT anywhere near it.
///
/// <para>It is BOTH an <see cref="IFlowElement"/> and an
/// <see cref="ICompletionTarget"/>, because in the engine those are one object:
/// S1 solves a rate for it and S2 asks the same element to turn that rate into a
/// stream. Splitting them across two objects sharing an id would let a cap S3
/// computed apply to one while the network's mass came from the other — the
/// exact defect FV7 exposed.</para>
/// </summary>
internal sealed class SyntheticCompletion(
    ulong id, double productivityIndex, double reservoirBar)
    : IFlowElement, ICompletionTarget
{
    /// <summary>kg of oil per m³/s of reservoir rate. A stand-in for PVT, which
    /// R4 deliberately does not have — any positive constant proves the coupling.</summary>
    public const double DensityKgPerCubicMetre = 850.0;

    public EntityId<IFlowElement> Id { get; } = new(id);
    public bool IsPressureDecoupled { get; init; }
    public ulong Compartment { get; init; } = 1;

    public IReadOnlyList<PortSpec> Ports { get; } = [Synthetic.Outlet(0)];

    public OperatingPoint OperatingPointAt(Pressure wellheadBackpressure)
    {
        double drawdownPa = reservoirBar * 1e5 - wellheadBackpressure.Pascals;

        // DEAD is a distinct outcome, not a zero rate: the well cannot flow at
        // ANY rate against this backpressure (R6-V6).
        if (drawdownPa <= 0.0) return new Dead();

        return new Flowing(
            new ReservoirRate(productivityIndex * drawdownPa),
            new Pressure(reservoirBar * 1e5));
    }

    public TransformResult Transform(TransformInput input)
    {
        // Null would mean the solver holds no rate for this element, which for a
        // completion is impossible — S1 solves one every iteration.
        double rate = input.SolvedRate?.CubicMetresPerSecond ?? 0.0;
        Composition produced = Synthetic.Comp(rate * DensityKgPerCubicMetre, 0.0);

        return new TransformResult(
            [new MaterialStream(produced, new Pressure(reservoirBar * 1e5),
                                Temperature.FromCelsius(60.0), Synthetic.From(Compartment))],
            Sourced: produced,
            FuelConsumed: Synthetic.Zero,
            Disposed: Synthetic.NoDisposal,
            PowerDraw: new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}
