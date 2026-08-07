// SDD-013 §5 — save migrations.
//
// `IMigrationStep` is NOT here. It was, over System.Text.Json's JsonNode, with
// no implementations and no callers, while OGSim.Persistence declared a second
// one over the engine's own JsonValue — the one MigrationChain uses and every
// migration test implements. Two declarations of one concept is what glossary
// rule N1 forbids (finding 134).
//
// The live one is right on the merits rather than merely by being used: the
// canonical form is JsonValue, and SDD-013 §3 requires that writer and reader
// live in ONE class. Migrating through JsonNode would have been the second
// serialisation path that rule exists to prevent — a block parsed by one library
// and rewritten by another, with the canonical byte rules (ordinal key order,
// shortest-round-trip doubles) holding on one side of a migration and not the
// other.
//
// It cannot move here: JsonValue belongs to the persistence module, which sits
// above Contracts, and a contract cannot name a type from the layer above it.
// So the declaration lives with the canonical form it operates on, and this file
// records why nothing here duplicates it.

namespace OGSim.Contracts;
