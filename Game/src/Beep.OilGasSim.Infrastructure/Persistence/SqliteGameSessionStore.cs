using Beep.OilGasSim.Application.Interfaces;
using Beep.OilGasSim.Domain.GameSessions;
using Microsoft.Data.Sqlite;

namespace Beep.OilGasSim.Infrastructure.Persistence;

public sealed class SqliteGameSessionStore : IGameSessionStore, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteGameSessionStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        EnsureSchema();
    }

    public async Task<GameSessionAggregate?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = Open();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload_json FROM game_sessions WHERE id = $id";
            command.Parameters.AddWithValue("$id", sessionId.ToString());
            var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
            return json is null ? null : GameSessionAggregateJson.Deserialize(json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GameSessionAggregate?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        var normalized = joinCode.Trim().ToUpperInvariant();
        var sessions = await ListAsync(cancellationToken);
        return sessions.FirstOrDefault(a =>
            a.Session.JoinCode.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(GameSessionAggregate aggregate, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = GameSessionAggregateJson.Serialize(aggregate);
            await using var connection = Open();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO game_sessions (id, join_code, payload_json, updated_utc)
                VALUES ($id, $joinCode, $payload, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    join_code = excluded.join_code,
                    payload_json = excluded.payload_json,
                    updated_utc = excluded.updated_utc
                """;
            command.Parameters.AddWithValue("$id", aggregate.Session.Id.ToString());
            command.Parameters.AddWithValue("$joinCode", aggregate.Session.JoinCode ?? "");
            command.Parameters.AddWithValue("$payload", json);
            command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<GameSessionAggregate>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var results = new List<GameSessionAggregate>();
            await using var connection = Open();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload_json FROM game_sessions ORDER BY updated_utc DESC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var json = reader.GetString(0);
                var aggregate = GameSessionAggregateJson.Deserialize(json);
                if (aggregate is not null)
                {
                    results.Add(aggregate);
                }
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS game_sessions (
                id TEXT NOT NULL PRIMARY KEY,
                join_code TEXT NOT NULL DEFAULT '',
                payload_json TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_game_sessions_join_code ON game_sessions(join_code);
            """;
        command.ExecuteNonQuery();
    }
}
