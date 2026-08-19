// R13 / R20d.9 — abandonment obligations (SDD-007 §6, design 02 §3.4).
//
// REGISTRATION IS UNCONDITIONAL. Every asset gets an obligation the moment it is
// created, because no path skips abandonment: a well that is drilled will one day
// be plugged whatever else happens to it, and a company that could create one
// without the liability would be able to walk away from the cost by never
// recording it.
//
// ONLY A COMPLETED ABANDONMENT DISCHARGES. Not shutting the well in, not selling
// the field, not the licence expiring — the plug has to go in the hole. That is
// what makes the end of a field's life a decision with a price rather than a
// state a player drifts into.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Operations;

/// <summary>
/// SDD-007 §6's registry — the single source the financial provision (SDD-009
/// §2) and the licence rules (SDD-011) both read.
/// </summary>
public sealed class ObligationRegistry : IObligationRegistry, IStateOwner
{
    private readonly Func<ContentId, Money> _costOf;

    // Insertion-ordered, because a save walks it and two runs of one game must
    // write the obligations in the same order (rule D-5).
    private readonly List<EntityRef> _order = [];
    private readonly Dictionary<EntityRef, Obligation> _outstanding = [];

    public ObligationRegistry(Func<ContentId, Money> costOf)
    {
        ArgumentNullException.ThrowIfNull(costOf);
        _costOf = costOf;
    }

    public StateKey Key { get; } = new("company.obligations");

    public int SchemaVersion => 1;

    /// <summary>
    /// AFTER THE WELLS, and this is the dependency that proved the mechanism
    /// (SDD-013 §2b). Reopening a completion registers an abandonment obligation
    /// exactly as drilling one does, so the rebuild puts one on every well it
    /// brings back — and the SAVE's record is the one that must stand, because a
    /// company that had already discharged an obligation must not get it back by
    /// reloading.
    ///
    /// <para>The old loader knew this as "obligations land in the third phase ON
    /// PURPOSE", a sentence in a comment. Key order does not agree —
    /// <c>company.obligations</c> sorts before <c>wells.completions</c> — so the
    /// first walk of the declared order restored them early and the rebuild
    /// faulted on an obligation that already existed. Which is the mechanism
    /// working: an ordering that had been prose became a claim a sort enforces,
    /// and the one place it was wrong said so immediately.</para>
    /// </summary>
    public IReadOnlyList<StateKey> RestoreAfter { get; } = [new StateKey("wells.completions")];

    /// <summary>How many assets still carry one. What a company owes the future,
    /// and what the read model reports.</summary>
    public int Outstanding => _outstanding.Count;

    /// <summary>Everything still owed, in the order it was incurred.</summary>
    public IReadOnlyList<EntityRef> Assets => _order;

    public void Register(EntityRef asset, ContentId abandonmentTemplate)
    {
        // A second registration of one asset would let a caller double the
        // liability by opening the same well twice — and would leave the second
        // one outstanding after the first was discharged.
        if (_outstanding.ContainsKey(asset))
            throw new InvariantFault("SDD-007 §6", asset,
                $"asset {asset.Kind}:{asset.Value} already carries an abandonment obligation");

        _outstanding.Add(asset, new Obligation(abandonmentTemplate, _costOf(abandonmentTemplate)));
        _order.Add(asset);
    }

    /// <summary>
    /// What it would cost to discharge this one. Zero for an asset that carries
    /// none — either never registered or already abandoned, and both are
    /// legitimately nothing owed.
    /// </summary>
    public Money EstimatedCost(EntityRef asset) =>
        _outstanding.TryGetValue(asset, out Obligation obligation) ? obligation.Cost : Money.Zero;

    /// <summary>Everything still owed, at today's estimate. The number a
    /// player is really deciding against when they consider walking away.</summary>
    public Money TotalOutstanding
    {
        get
        {
            Money total = Money.Zero;
            for (int i = 0; i < _order.Count; i++) total += EstimatedCost(_order[i]);
            return total;
        }
    }

    public bool IsOutstanding(EntityRef asset) => _outstanding.ContainsKey(asset);

    /// <summary>
    /// Discharged by a COMPLETED abandonment and by nothing else (SDD-007 §6).
    ///
    /// <para>Discharging one that is not outstanding is a fault rather than a
    /// no-op: it means an abandonment completed against an asset that never
    /// carried a liability, which is either a double abandonment or a well
    /// nobody registered — and both are defects worth finding at the moment they
    /// happen.</para>
    /// </summary>
    public void Discharge(EntityRef asset, EntityId<IOperation> completedAbandonment)
    {
        if (!_outstanding.Remove(asset))
            throw new InvariantFault("SDD-007 §6", asset,
                $"abandonment {completedAbandonment.Value} discharged asset " +
                $"{asset.Kind}:{asset.Value}, which carries no obligation");

        _order.Remove(asset);
    }

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _order.Count);

        for (int i = 0; i < _order.Count; i++)
        {
            string at = Prefix(i);
            EntityRef asset = _order[i];

            writer.WriteInt64(at + "kind", (long)asset.Kind);
            writer.WriteInt64(at + "id", (long)asset.Value);
            writer.WriteString(at + "template", _outstanding[asset].Template.Value);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _order.Clear();
        _outstanding.Clear();

        long count = reader.ReadInt64("count");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);

            var asset = new EntityRef(
                (EntityKind)reader.ReadInt64(at + "kind"),
                (ulong)reader.ReadInt64(at + "id"));

            // The COST is re-read from content rather than saved: an estimate is
            // what today's content says a plug costs, and a save that pinned it
            // would carry a price the game no longer charges (SDD-013 §4).
            Register(asset, new ContentId(reader.ReadString(at + "template")));
        }
    }

    private static string Prefix(long index) =>
        "obligation." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";

    private readonly record struct Obligation(ContentId Template, Money Cost);
}
