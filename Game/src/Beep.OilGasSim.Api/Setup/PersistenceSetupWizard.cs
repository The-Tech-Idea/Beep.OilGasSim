using System.Text.Json;
using Beep.OilGasSim.Infrastructure.Persistence;

namespace Beep.OilGasSim.Api.Setup;
public static class PersistenceSetupWizard
{
    public static async Task<int> RunAsync(string? contentRoot = null)
    {
        contentRoot ??= ResolveApiContentRoot();
        Console.WriteLine();
        Console.WriteLine("Beep Oil and Gas Sim — persistence setup");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("The web client talks to the API. The API talks to a session store.");
        Console.WriteLine("You do NOT need PostgreSQL to play locally.");
        Console.WriteLine();
        Console.WriteLine("Choose a session store:");
        Console.WriteLine("  1) InMemory  — no database (default, games lost when API stops)");
        Console.WriteLine("  2) SQLite    — single file, no server install (recommended locally)");
        Console.WriteLine("  3) SQL Server — connection string (planned; falls back to InMemory today)");
        Console.WriteLine("  4) PostgreSQL — connection string (planned; falls back to InMemory today)");
        Console.WriteLine();
        Console.Write("Selection [1-4] (default 2): ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(choice))
        {
            choice = "2";
        }

        var options = choice switch
        {
            "1" => new PersistenceOptions { Provider = PersistenceProvider.InMemory },
            "3" => await PromptConnectionStringAsync(PersistenceProvider.SqlServer),
            "4" => await PromptConnectionStringAsync(PersistenceProvider.PostgreSQL),
            _ => await PromptSqliteAsync()
        };

        var targetPath = Path.Combine(contentRoot, "appsettings.Development.local.json");
        var payload = new Dictionary<string, object>
        {
            ["Persistence"] = new Dictionary<string, object?>
            {
                ["Provider"] = options.Provider.ToString(),
                ["SqlitePath"] = options.SqlitePath,
                ["ConnectionString"] = options.ConnectionString
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(targetPath, json);

        Console.WriteLine();
        Console.WriteLine($"Saved {targetPath}");
        Console.WriteLine($"Provider: {options.Provider}");
        if (options.Provider == PersistenceProvider.Sqlite)
        {
            Console.WriteLine($"SQLite file: {options.SqlitePath}");
        }

        Console.WriteLine();
        Console.WriteLine("Start the API:");
        Console.WriteLine("  dotnet run --project src/Beep.OilGasSim.Api");
        Console.WriteLine();
        return 0;
    }

    private static Task<PersistenceOptions> PromptSqliteAsync()
    {
        Console.Write("SQLite file path [data/beepoilgas.db]: ");
        var path = Console.ReadLine()?.Trim();
        return Task.FromResult(new PersistenceOptions
        {
            Provider = PersistenceProvider.Sqlite,
            SqlitePath = string.IsNullOrWhiteSpace(path) ? "data/beepoilgas.db" : path
        });
    }

    private static async Task<PersistenceOptions> PromptConnectionStringAsync(PersistenceProvider provider)
    {
        Console.WriteLine();
        Console.WriteLine($"Enter {provider} connection string (stored locally in appsettings.Development.local.json):");
        var connectionString = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("No connection string entered — using SQLite instead.");
            return await PromptSqliteAsync();
        }

        return new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = connectionString
        };
    }

    private static string ResolveApiContentRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            cwd,
            Path.Combine(cwd, "src", "Beep.OilGasSim.Api"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "Beep.OilGasSim.Api.csproj")))
            {
                return candidate;
            }
        }

        return cwd;
    }
}