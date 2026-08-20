# PersistenceContracts

Source: `src\OGSim.Contracts\PersistenceContracts.cs` · Lines: 22

## File intent

> SDD-013 §5 — save migrations.
> 
> `IMigrationStep` is NOT here. It was, over System.Text.Json's JsonNode, with
> no implementations and no callers, while OGSim.Persistence declared a second
> one over the engine's own JsonValue — the one MigrationChain uses and every
> migration test implements. Two declarations of one concept is what glossary
> rule N1 forbids (finding 134).
> 

## Namespaces

- `OGSim.Contracts`

## Type declarations

_No top-level/nested type declarations matched the extractor._

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

