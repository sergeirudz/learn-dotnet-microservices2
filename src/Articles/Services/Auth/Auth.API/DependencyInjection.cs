using System.Security.Claims;
using Articles.Security;
using Auth.API;
using Auth.Domain.Users;
using EmailService.Smtp;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api;

public static class DependencyInjection
{
    public static IServiceCollection ConfigureApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddFastEndpoints()
            .SwaggerDocument()
            .AddEndpointsApiExplorer()
            .AddSwaggerGen()
            .AddJwtAuthentication(config)
            .AddJwtIdentity(config)
            .AddAuthorization();

        services.AddSmtpEmailService(config);

        return services;
    }

    public static IServiceCollection AddJwtIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<Users>(options =>
            {
                // Lockout settings
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;

                //options.User.RequireUniqueEmail = false; //to-do - change back to true after test training users not needed anymore
                //options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/ ";
            })
            .AddRoles<Auth.Domain.Roles.Role>()
            // .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager<SignInManager<Users>>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options => { options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role; });

        return services;
    }
}