# CompanyState

Source: `src\OGSim.Company\CompanyState.cs` · Lines: 126

## File intent

> R20c.6 — the ledger as an owned, saved fact (SDD-001 §10, SDD-009 §1).
> 
> THE LEDGER IS REBUILT BY REPLAY, not by restoring balances. Every movement is
> re-posted through the same Post that validated it originally, so a save cannot
> load a ledger that the running engine would have refused — a restored balance
> would trust the file, and a file is exactly the thing that might be wrong.
> INV2 therefore holds after a load for the same reason it held before one.
> <summary>

## Namespaces

- `OGSim.Company`

## Type declarations

- `L17` `public sealed class CompanyState : IStateOwner`

## Accessible members

- `L19` `private readonly Func<AuditId, bool> _isCustodyTransfer;`
- `L21` `public CompanyState(Money openingCash, Func<AuditId, bool> isCustodyTransfer)`
- `L34` `public CostLedger Ledger { get; private set; }`
- `L36` `public StateKey Key { get; } = new("company.ledger");`
- `L38` `public int SchemaVersion => 1;`
- `L40` `public void Capture(IStateWriter writer)`
- `L80` `public void Restore(IStateReader reader)`
- `L124` `private static string Prefix(long index) =>`

## Imports

- `using OGSim.Kernel;`

