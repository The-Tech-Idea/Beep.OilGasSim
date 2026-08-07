// SDD-013 §5 — save migrations. Chain composition v→v+1; a gap in the chain is
// a composition fault at startup; every step ships with a real fixture save of
// version From (PV5). JsonNode because migration operates on the canonical
// block BEFORE the typed readers exist for that shape.

using System.Text.Json.Nodes;

namespace OGSim.Contracts;

/// <summary>
/// <b>SUPERSEDED, and deliberately still here (finding 134).</b>
///
/// <para><c>OGSim.Persistence</c> declares a second <c>IMigrationStep</c> —
/// <c>int From</c> and <c>JsonValue Migrate(JsonValue, string)</c> — and that is
/// the one <c>MigrationChain</c> uses and the one every migration test
/// implements. This one has no implementations and no callers. Two declarations
/// of one concept is exactly what glossary rule N1 forbids.</para>
///
/// <para>The live one is right on the merits, not merely by being used: the
/// canonical form is the engine's own <c>JsonValue</c>, and SDD-013 §3 requires
/// that writer and reader live in ONE class. Migrating through
/// <c>System.Text.Json.Nodes.JsonNode</c> would be the second serialisation path
/// that rule exists to prevent — the block would be parsed by one library,
/// rewritten by another, and the canonical byte rules (ordinal key order,
/// shortest-round-trip doubles) would hold on one side of a migration and not
/// the other.</para>
///
/// <para>It is left in place rather than deleted because removing a contract
/// type is the owner's call. The resolution, when taken, is to delete this
/// declaration — <c>OGSim.Contracts</c> cannot host the live one, since
/// <c>JsonValue</c> belongs to the persistence module above it.</para>
/// </summary>
public interface IMigrationStep
{
    int From { get; }
    JsonNode Migrate(JsonNode block, string module);
}
