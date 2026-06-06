using System.Text.Json;
using System.Text.Json.Serialization;
using Beep.OilGasSim.Domain.GameSessions;

namespace Beep.OilGasSim.Infrastructure.Persistence;

internal static class GameSessionAggregateJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(GameSessionAggregate aggregate) =>
        JsonSerializer.Serialize(aggregate, Options);

    public static GameSessionAggregate? Deserialize(string json) =>
        JsonSerializer.Deserialize<GameSessionAggregate>(json, Options);
}
