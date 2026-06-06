namespace Beep.OilGasSim.Infrastructure.Persistence;

public enum PersistenceProvider
{
    InMemory,
    Sqlite,
    SqlServer,
    PostgreSQL
}

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public PersistenceProvider Provider { get; set; } = PersistenceProvider.InMemory;

    /// <summary>SQLite file path (relative to API content root or absolute).</summary>
    public string SqlitePath { get; set; } = "data/beepoilgas.db";

    /// <summary>SQL Server or PostgreSQL ADO.NET connection string.</summary>
    public string ConnectionString { get; set; } = "";
}
