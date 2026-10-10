using Articles.Abstractions.Enums;
using Blocks.EntityFrameworkCore;
using Blocks.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submission.Domain.Entities;
using Submission.Persistence.Repositories;

namespace Submission.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<SubmissionDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString);
            options.AddInterceptors(provider.GetServices<ISaveChangesInterceptor>());
        });

        // Repository from Persistance 
        services.AddScoped(typeof(Repository<>));
        services.AddScoped(typeof(Repository<>)); // only Repository<>

        services.AddScoped<CachedRepository<SubmissionDbContext, AssetTypeDefinition, AssetType>>();

        return services;
    }
}