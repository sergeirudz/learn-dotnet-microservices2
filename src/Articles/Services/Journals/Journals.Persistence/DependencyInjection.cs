using Blocks.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Redis.OM;
using StackExchange.Redis;

namespace Journals.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Database")!;

        services.AddSingleton(new RedisConnectionProvider(connectionString));
        var redisConnectionString = connectionString.StartsWith("redis://") || connectionString.StartsWith("rediss://")
            ? connectionString.Substring(connectionString.IndexOf("://") + 3)
            : connectionString;
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redis);

        services.AddSingleton<JournalDbContext>();
        // register all repositories which have as a base class the generic Repository<T> class
        services.AddScoped(typeof(Repository<>));

        return services;
    }
}