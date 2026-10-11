using System.Reflection;
using System.Runtime.CompilerServices;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.Mapster;

public static class MapsterConfiguration
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddMapsterConfigsFromCurrentAssembly(this IServiceCollection services,
        Assembly? assembly = null)
    {
        if (assembly is null)
            assembly = Assembly.GetCallingAssembly()!;

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        return services;
    }
}