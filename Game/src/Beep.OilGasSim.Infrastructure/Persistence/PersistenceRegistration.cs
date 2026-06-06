using Beep.OilGasSim.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Beep.OilGasSim.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddGamePersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
                        ?? new PersistenceOptions();

        services.AddSingleton(options);

        switch (options.Provider)
        {
            case PersistenceProvider.Sqlite:
            {
                var dbPath = ResolveSqlitePath(environment, options.SqlitePath);
                services.AddSingleton<IGameSessionStore>(_ => new SqliteGameSessionStore(dbPath));
                break;
            }
            case PersistenceProvider.SqlServer:
            case PersistenceProvider.PostgreSQL:
                services.AddSingleton<IGameSessionStore>(sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Persistence");
                    logger.LogWarning(
                        "{Provider} persistence is not implemented yet. Using in-memory store. " +
                        "Use Provider=Sqlite for file-based saves without installing a database server.",
                        options.Provider);
                    return new InMemoryGameSessionStore();
                });
                break;
            default:
                services.AddSingleton<IGameSessionStore, InMemoryGameSessionStore>();
                break;
        }

        return services;
    }

    public static string ResolveSqlitePath(IHostEnvironment environment, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }
}
