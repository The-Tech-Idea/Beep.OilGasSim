// R20c.6 — the ledger as an owned, saved fact (SDD-001 §10, SDD-009 §1).
//
// THE LEDGER IS REBUILT BY REPLAY, not by restoring balances. Every movement is
// re-posted through the same Post that validated it originally, so a save cannot
// load a ledger that the running engine would have refused — a restored balance
// would trust the file, and a file is exactly the thing that might be wrong.
// INV2 therefore holds after a load for the same reason it held before one.

using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// Owner of <c>company.ledger</c>. Holds the ledger because law L5 wants one
/// owner per fact and the module needs somewhere for that owner to be.
/// </summary>
public sealed class CompanyState : IStateOwner
{
    private readonly Func<AuditId, bool> _isCustodyTransfer;

    public CompanyState(Money openingCash, Func<AuditId, bool> isCustodyTransfer)
    {
        ArgumentNullException.ThrowIfNull(isCustodyTransfer);

        _isCustodyTransfer = isCustodyTransfer;
        Ledger = new CostLedger(openingCash, isCustodyTransfer);
    }

    /// <summary>
    /// Replaced wholesale on restore rather than mutated, because a ledger has
    /// no un-post: replaying onto a fresh one is the only way to arrive at the
    /// saved position through the rules that produced it.
    /// </summary>
    public CostLedger Ledger { get; private set; }

    public StateKey Key { get; } = new("company.ledger");

    public int SchemaVersion => 1;

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("opening-cash", Ledger.OpeningCash.Cents);

        IReadOnlyList<Movement> movements = Ledger.Movements;
        writer.WriteInt64("movement-count", movements.Count);

        for (int i = 0; i < movements.Count; i++)
        {
            Movement movement = movements[i];
            string at = Prefix(i);

            writer.WriteInt64(at + "tick", movement.Tick.Value);
            writer.WriteInt64(at + "debit", (long)movement.Debit);
            writer.WriteInt64(at + "credit", (long)movement.Credit);

            // Money as integer cents (SDD-013 §3). Never a double: cash
            // conservation is exact and a round-trip through binary floating
            // point is the one way to lose a cent.
            writer.WriteInt64(at + "amount", movement.Amount.Cents);
            writer.WriteInt64(at + "category", (long)movement.Category);
            writer.WriteInt64(at + "cause", (long)movement.Cause.Value);

            // A null asset is written as a FLAG rather than as a sentinel id:
            // there is no id value that means "no asset", and picking one would
            // make entity 0 unsaveable the day ids start at 0.
            bool hasAsset = movement.Asset is not null;
            writer.WriteInt64(at + "has-asset", hasAsset ? 1 : 0);

            if (hasAsset)
            {
                EntityRef asset = movement.Asset!.Value;
                writer.WriteInt64(at + "asset-kind", (long)asset.Kind);
                writer.WriteInt64(at + "asset-id", (long)asset.Value);
            }
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var rebuilt = new CostLedger(
            new Money(reader.ReadInt64("opening-cash")), _isCustodyTransfer);

        long count = reader.ReadInt64("movement-count");
        if (count < 0)
            throw new SaveDataFault("SDD-009 §1", null,
                $"The ledger declares {count} movements.");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);

            EntityRef? asset = reader.ReadInt64(at + "has-asset") == 1
                ? new EntityRef(
                    (EntityKind)reader.ReadInt64(at + "asset-kind"),
                    (ulong)reader.ReadInt64(at + "asset-id"))
                : null;

            // REPLAYED, not posted (SDD-009 §1's R20d.12 amendment). Re-asking a
            // saved revenue credit to name a custody transfer asks the audit
            // trail a question it cannot answer: a freshly composed engine's
            // trail is empty, and a running one keeps detail on a window.
            rebuilt.Replay(new Movement(
                new Tick((int)reader.ReadInt64(at + "tick")),
                (Account)reader.ReadInt64(at + "debit"),
                (Account)reader.ReadInt64(at + "credit"),
                new Money(reader.ReadInt64(at + "amount")),
                (MovementCategory)reader.ReadInt64(at + "category"),
                asset,
                new AuditId((ulong)reader.ReadInt64(at + "cause"))));
        }

        // The replayed ledger must balance, or the save was wrong and the fault
        // belongs here rather than at the next close (INV2).
        rebuilt.AssertBalanced();

        Ledger = rebuilt;
    }

    /// <summary>
    /// Zero-padded so that ordinal key sorting and numeric order agree —
    /// otherwise movement 10 sorts before movement 2 and the canonical bytes
    /// stop matching the replay order (SDD-013 §3).
    /// </summary>
    private static string Prefix(long index) =>
        "movement." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";
}
