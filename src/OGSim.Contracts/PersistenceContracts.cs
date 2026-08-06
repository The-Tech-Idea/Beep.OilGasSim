// SDD-013 §5 — save migrations. Chain composition v→v+1; a gap in the chain is
// a composition fault at startup; every step ships with a real fixture save of
// version From (PV5). JsonNode because migration operates on the canonical
// block BEFORE the typed readers exist for that shape.

using System.Text.Json.Nodes;

namespace OGSim.Contracts;

public interface IMigrationStep
{
    int From { get; }
    JsonNode Migrate(JsonNode block, string module);
}
