# Ledger

Source: `src\OGSim.Company\Ledger.cs` · Lines: 193

## File intent

> R13.1 — the cost ledger (SDD-009 §1, INV2).
> 
> DOUBLE-ENTRY, ALWAYS, AND EXACT. INV2 is the trial balance — Σ debits =
> Σ credits per tick, and Cash equals opening plus movements — in integers, with
> NO TOLERANCE TERM. A one-cent error is a bug by definition, which is only a
> meaningful statement because the double→Money boundary is pinned:
> 
> Physical quantities are doubles. The ledger is integer cents. Every crossing

## Namespaces

- `OGSim.Company`

## Type declarations

- `L23` `public enum Account`
- `L30` `public enum MovementCategory`
- `L37` `public sealed record Movement(`
- `L50` `public sealed class CostLedger`

## Accessible members

- `L52` `private readonly List<Movement> _movements = [];`
- `L53` `private readonly Dictionary<Account, long> _balances = [];`
- `L64` `private readonly Func<AuditId, bool> _isCustodyTransfer;`
- `L66` `public CostLedger(Money openingCash, Func<AuditId, bool> isCustodyTransfer)`
- `L90` `public Money OpeningCash { get; }`
- `L92` `public IReadOnlyList<Movement> Movements => _movements;`
- `L94` `public Money BalanceOf(Account account) =>`
- `L97` `public Money Cash => BalanceOf(Account.Cash);`
- `L104` `public void Post(Movement movement)`
- `L142` `public void AssertBalanced()`
- `L165` `public void AssertCashReconciles()`
- `L182` `private void Add(Account account, long cents) =>`
- `L186` `private static readonly Account[] AllAccounts =`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

