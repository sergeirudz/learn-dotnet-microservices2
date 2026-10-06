using FastEndpoints.Swagger;
using Auth.Api;
using Auth.API.Features.Persons;
using Auth.Application;
using Auth.Persistence;

var builder = WebApplication.CreateBuilder(args);

#region Add

builder.Services
    .ConfigureApiOptions(builder.Configuration); // Configure Options

builder.Services
    .AddApiServices(builder.Configuration) // Register API-specific services
    .AddApplicationServices(builder.Configuration) // Register Applicaiton-specific services
    .AddPersistenceServices(builder.Configuration); // Register Persistence-specific services

#endregion

var app = builder.Build();


#region Use

app
    .UseHttpsRedirection() // Redirects HTTP to HTTPS; safe early
    .UseRouting() // Enables endpoint routing; must come before UseAuthorization
    .UseFastEndpoints()
    .UseSwaggerGen();

app.MapGrpcService<PersonGrpcService>(); // We will expose this GRPC service to external clients

#endregion

app.Run();