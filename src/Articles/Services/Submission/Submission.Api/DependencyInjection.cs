namespace Submission.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddMemoryCache() // Basic caching
            .AddEndpointsApiExplorer() // Minimal API Docs
            .AddSwaggerGen() // Swagger setup
            ;
        return services;
    }
}