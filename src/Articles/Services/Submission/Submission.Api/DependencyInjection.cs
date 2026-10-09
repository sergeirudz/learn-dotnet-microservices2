using Auth.Grpc;
using Blocks.AspNetCore.Grpc;
using Blocks.Core;
using FileStorage.MongoGridFS;
using Journals.Grpc;

namespace Submission.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddMemoryCache() // Basic caching
            .AddEndpointsApiExplorer() // Minimal API Docs
            .AddSwaggerGen() // Swagger setup
            ;

        services.AddMongoFileStorage(config);

        //grpc Services
        var grpcOptions = config.GetSectionByTypeName<GrpcServicesOptions>();
        services.AddCodeFirstGrpcClient<IPersonService>(grpcOptions, "Person");
        services.AddCodeFirstGrpcClient<IJournalService>(grpcOptions, "Journal");
        return services;
    }
}