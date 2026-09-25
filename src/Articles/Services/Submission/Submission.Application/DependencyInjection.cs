using System.Reflection;
using Blocks.MediatR.Behaviors;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submission.Application.Features.CreateArticle;

namespace Submission.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            // register all fluent validation
            .AddValidatorsFromAssemblyContaining<
                CreateArticleCommandValidator>() // Register Fluent validators as transient
            .AddMediatR(config =>
            {
                // register all MediatR handlers automatically
                config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

                // Plug in fluent validation into Mediatr
                config.AddOpenBehavior(typeof(ValidationBehavior<,>));
                config.AddOpenBehavior(typeof(SetUserIdBehavior<,>));
            });
        return services;
    }
}