using System.Reflection;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.Mapster;

public static class AddMapster
{
    public static IServiceCollection AddMapsterConfigsFromCurrentAssembly(this IServiceCollection services,
        Assembly? assembly = null)
    {
        if (assembly is null)
            assembly = Assembly.GetCallingAssembly()!;

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        return services;
    }
}