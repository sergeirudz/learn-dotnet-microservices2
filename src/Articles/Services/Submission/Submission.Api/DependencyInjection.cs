using System.Text.Json.Serialization;
using Auth.Grpc;
using Blocks.AspNetCore.Grpc;
using Blocks.Core;
using Blocks.Messaging;
using FileStorage.MongoGridFS;
using Journals.Grpc;
using Microsoft.AspNetCore.Http.Json;

namespace Submission.Api;

public static class DependencyInjection
{
    public static void ConfigureApiOptions(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddAndValidateOptions<RabbitMqOptions>(config)
            .Configure<JsonOptions>(opt =>
            {
                opt.SerializerOptions.PropertyNameCaseInsensitive = true;
                opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
    }

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