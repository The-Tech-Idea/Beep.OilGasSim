// R20d.28 — beliefs go stale (SDD-008 §2, §2d; finding 200).
//
// `BeliefStore.Age` implemented §2's staleness from the day R14 landed, had a
// unit test, and was called by NOTHING. So no belief in any game has ever gone
// stale: a pressure reading from month four was exactly as trustworthy in month
// four hundred, and the whole reason a player re-tests a well was missing.
//
// IT IS NOT AN L3 BREACH, which is why nothing caught it. The member has real
// behaviour and real coverage; what it lacked was a caller, and the law catches
// a member with no behaviour and has nothing to say about behaviour with no
// caller. This is the fourth time this repository has produced that shape —
// `ProspectRisk` unused for four phases, `ObservationSampler` provided by
// nobody, the flow solver bypassed by a second production path, and now this.
//
// DRIFT IS CHARGED TO PRODUCTION, NOT TO THE CALENDAR (SDD-008 §2d.2, closing
// S008-2). A belief about pressure goes stale because the pressure MOVED, and
// what moves it is withdrawal. Charging it to the clock instead would make
// WAITING a source of uncertainty, and the optimal play would become re-testing
// on a timer rather than when something changed.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;

namespace OGSim.Composition;

/// <summary>
/// Stage 11. Widens what the company believes about every compartment it
/// produced from this tick.
/// </summary>
internal sealed class StalenessStage(
    IBeliefStore beliefs,
    TickProduction produced,
    Func<ContentId, double> driftPerYearFor,
    IReadOnlyList<ContentId> dynamicKinds) : ITickStage
{
    public StageId Id => StageId.Information;

    /// <summary>
    /// A tick's share of a year on the 30/360 calendar — exactly one twelfth,
    /// because every month is <see cref="Duration.DaysPerTick"/> days long.
    /// </summary>
    private static readonly double YearsPerTick =
        Duration.DaysPerTick / PhysicalConstants.DaysPerYear;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // WHAT THE FIELD ACTUALLY TOOK, which is the list stage 6 committed.
        IReadOnlyList<CompartmentWithdrawal> withdrawals = produced.Withdrawals;

        for (int i = 0; i < withdrawals.Count; i++)
        {
            // AND ZERO IS NOT PRODUCTION. Being in this list means a compartment
            // was SOLVED, not that anything came out of it: shutting every well
            // on a field leaves it here with a withdrawal of nothing. Written
            // after V16 caught it — the first version treated the list as
            // self-filtering and aged a shut-in field at the full rate, which is
            // exactly the calendar-charged drift §2d.2 decided against.
            if (withdrawals[i].ReservoirVolume.CubicMetres <= 0.0) continue;

            // The subject a belief about this compartment is filed against —
            // the same reference `WellTestActivity` delivers a pressure reading
            // against, because it has to be the same fact or the update and the
            // drift would be about two different things (law L5).
            var subject = new EntityRef(
                EntityKind.Compartment, withdrawals[i].Compartment.Value);

            for (int k = 0; k < dynamicKinds.Count; k++)
                beliefs.Age(
                    subject, dynamicKinds[k], driftPerYearFor(dynamicKinds[k]), YearsPerTick);
        }
    }
}
