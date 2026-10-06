using FastEndpoints;
using FastEndpoints.Swagger;
using Journals.API;
using Journals.Persistence;

var builder = WebApplication.CreateBuilder(args);

#region Add

builder.Services
    .ConfigureApiOptions(builder.Configuration); // Configure Options

builder.Services
    .AddApiServices(builder.Configuration) // Register API-specific services
    .AddPersistenceServices(builder.Configuration); // Register Persistence-specific services

#endregion

var app = builder.Build();


#region Use

app
    .UseHttpsRedirection() // Redirects HTTP to HTTPS; safe early
    .UseRouting() // Enables endpoint routing; must come before UseAuthorization
    .UseFastEndpoints()
    .UseSwaggerGen();

#endregion

app.Run();