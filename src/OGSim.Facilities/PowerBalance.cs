// R8.8 — the facility power balance (SDD-006 §3, R8 §2.5, design 03 §6.1).
//
// Units declare demand; sources declare supply. A shortfall takes units OFFLINE
// by a declared priority order — at tick stage 4, BEFORE the flow solve at stage
// 5. That ordering is the point: a power shortfall is decided before the solve
// rather than discovered inside it, and an element taken offline is simply
// absent from the segment's network (design 04 §4).
//
// It is also the coupling R7 §2.4 was written for. A field that puts ESPs on
// twenty wells needs generation capacity, and if it has not got it, SOMETHING
// ELSE GOES OFFLINE. The player who solved a lift problem discovers they have
// created a processing one.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// R8 §2.5's declared order. Safety-critical is shed last because shedding it is
/// how a facility stops being safe.
/// </summary>
public enum PowerPriority
{
    SafetyCritical = 0,
    ExportCritical = 1,
    Processing = 2,
    Discretionary = 3,
}

/// <summary>
/// A unit's claim on power, at NAMEPLATE duty.
///
/// <para>Declared duty rather than solved rate, per design 03 §6.1's lag rule:
/// an ESP's or compressor's real demand depends on this tick's rate, which stage
/// 5 has not solved yet. Balancing on nameplate is conservative and
/// deterministic; balancing on last tick's rate would make the shed order depend
/// on history in a way nobody could predict from the screen.</para>
/// </summary>
public sealed record PowerDemand(
    EntityId<IFlowElement> Unit,
    Power Declared,
    PowerPriority Priority);

/// <summary>What the balance decided.</summary>
public sealed record PowerBalanceResult(
    Power Supply,
    Power DemandBefore,
    Power DemandAfter,
    IReadOnlyList<EntityId<IFlowElement>> Offline)
{
    public bool Shortfall => Offline.Count > 0;
}

/// <summary>
/// SDD-006 §3's stage-4 balance.
/// </summary>
public static class PowerBalance
{
    /// <summary>
    /// Sheds units until demand fits supply, lowest priority first and — within
    /// a priority — LARGEST DEMAND first.
    ///
    /// <para>Largest-first because shedding is meant to end quickly: taking the
    /// biggest consumer offline clears the shortfall in the fewest units, and
    /// shedding smallest-first would black out a dozen small units to save one
    /// large one. Ties break on ascending id so the result is total (D-1).</para>
    ///
    /// <para>A shortfall that persists after everything sheddable is offline is
    /// NOT a fault: a facility whose safety-critical load alone exceeds supply is
    /// in a bad state the player must fix, and refusing to tick would deny them
    /// the chance. The result says so and the caller raises
    /// <c>power.shortfall</c>.</para>
    /// </summary>
    public static PowerBalanceResult Balance(
        IReadOnlyList<IPowerSource> sources, IReadOnlyList<PowerDemand> demands)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(demands);

        // Merit order: grid → waste-heat → turbine → genset. Every source in
        // merit order contributes, because a source not run is a source not
        // costed and the balance is about capacity, not dispatch.
        var ordered = new List<IPowerSource>(sources);
        ordered.Sort((a, b) => a.MeritRank.CompareTo(b.MeritRank));

        double supply = 0.0;
        for (int i = 0; i < ordered.Count; i++)
        {
            double max = ordered[i].MaxSupply.Watts;
            if (double.IsNaN(max) || max < 0.0)
                throw new ModelFault("SDD-006 §3", null,
                    $"power source at merit rank {ordered[i].MeritRank} declares " +
                    $"{Format(max)} W");

            supply += max;
        }

        var live = new List<PowerDemand>(demands);
        double demand = 0.0;
        for (int i = 0; i < live.Count; i++) demand += live[i].Declared.Watts;

        double demandBefore = demand;
        var offline = new List<EntityId<IFlowElement>>();

        if (demand <= supply)
            return new PowerBalanceResult(
                new Power(supply), new Power(demandBefore), new Power(demand), offline);

        // Shed order: lowest priority first, then largest draw, then lowest id.
        live.Sort((a, b) =>
        {
            int byPriority = ((int)b.Priority).CompareTo((int)a.Priority);
            if (byPriority != 0) return byPriority;

            int bySize = b.Declared.Watts.CompareTo(a.Declared.Watts);
            if (bySize != 0) return bySize;

            return a.Unit.Value.CompareTo(b.Unit.Value);
        });

        for (int i = 0; i < live.Count && demand > supply; i++)
        {
            // Safety-critical is shed only when nothing else is left, which the
            // ordering already guarantees — but a facility can still end up
            // shedding it, and pretending otherwise would hide a real state.
            offline.Add(live[i].Unit);
            demand -= live[i].Declared.Watts;
        }

        // Reported in id order so the event and the read model are stable
        // regardless of the order shedding happened to reach them.
        offline.Sort((a, b) => a.Value.CompareTo(b.Value));

        return new PowerBalanceResult(
            new Power(supply), new Power(demandBefore), new Power(Math.Max(0.0, demand)), offline);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
