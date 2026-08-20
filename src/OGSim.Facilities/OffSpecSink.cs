// SDD-006 §7d — R20d.29 (finding 252). What the custody spec gate's Reject
// leg goes to.
//
// A rejection is a real, permanent loss, not a retry: SpecificationGate.cs's
// own header says the point is "a rejection with a reason", and a stream
// that quietly found its way back to the meter would remove exactly the
// pressure that is meant to make a treater or a dehydrator a decision.
// Reprocessing would also need an edge back INTO the network, which this
// composition's flow graph — a DAG, walked in topological order — does not
// express.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// A terminal sink for mass a spec gate refused. Same shape as <see
/// cref="Flare"/> — one inlet, no outlets, everything that arrives leaves the
/// network as <see cref="DisposedMass.Discharged"/>, the category <see
/// cref="Treater"/> already uses for "left the network, was never sold, is
/// accounted for."
/// </summary>
public sealed class OffSpecSink : IFlowElement
{
    private readonly int _materialCount;

    public OffSpecSink(EntityId<IFlowElement> id, int materialCount)
    {
        Id = id;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>The one port, named so a caller wiring the chain never writes
    /// a bare index (see <see cref="Separator.Inlet"/>).</summary>
    public static PortId Inlet { get; } = new(0);

    /// <summary>Inlet only — a terminal sink, exactly as <see cref="Flare"/>
    /// is for the gas leg.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
        [new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main)];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Composition arriving = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates
            : Composition.Zero(_materialCount);

        return new TransformResult(
            [],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Flared: Composition.Zero(_materialCount),
                Vented: Composition.Zero(_materialCount),
                Discharged: arriving),
            PowerDraw: new Power(0.0));
    }

    /// <summary>NONE. A rejection is sized by what the spec refused, never by
    /// a capacity of its own — the gate has already decided how much arrives
    /// here, and a limit at this end would be a second decision about the
    /// same mass.</summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return [];
    }
}
