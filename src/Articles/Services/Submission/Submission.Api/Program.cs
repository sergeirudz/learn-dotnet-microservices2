using Submission.Api;
using Submission.Api.Endpoints;
using Submission.Application;
using Submission.Persistence;

var builder = WebApplication.CreateBuilder(args);

#region Add Services

builder.Services
    .AddApiServices(builder.Configuration)
    .AddApplicationServices(builder.Configuration)
    .AddPersistenceServices(builder.Configuration);

#endregion

var app = builder.Build();

#region Use Services

app
    .UseSwagger()
    .UseSwaggerUI()
    .UseRouting();

app.MapAllEndpoints();
// TODO migrate - create first migrations

if (app.Environment.IsDevelopment())
{
    // TODO seed test data
}

#endregion


app.Run();