using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blocks.EntityFramework;

public static class BuilderExtensions
{
    public static PropertyBuilder<TEnum> HasEnumConversion<TEnum>(this PropertyBuilder<TEnum> builder)
        // You need to define what type can this Generic argument be
        where TEnum : Enum
    {
        return builder.HasConversion(
            e => e.ToString(),
            value => (TEnum)Enum.Parse(typeof(TEnum), value)
        );
    }
}