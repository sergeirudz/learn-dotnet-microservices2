using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Blocks.EntityFrameworkCore;

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

    public static PropertyBuilder<T> HasJsonCollectionConversion<T>(this PropertyBuilder<T> builder)
        => builder.HasConversion(BuildJsonListConvertor<T>());

    public static ValueConverter<TCollection, string> BuildJsonListConvertor<TCollection>()
    {
        Func<TCollection, string> serializeFunc = v => JsonSerializer.Serialize(v);
        Func<string, TCollection> deserializeFunc = v => JsonSerializer.Deserialize<TCollection>(v ?? "[]")!;

        return new ValueConverter<TCollection, string>(
            v => serializeFunc(v), // serialize list into a column in the DB
            v => deserializeFunc(v)); // Deserialize list into .NET
    }

    public static ValueConverter<IReadOnlyList<T>, string> BuildJsonReadOnlyListConvertor<T>()
    {
        Func<IReadOnlyList<T>, string> serializeFunc = v => JsonSerializer.Serialize(v);
        Func<string, IReadOnlyList<T>> deserializeFunc = v => JsonSerializer.Deserialize<IReadOnlyList<T>>(v ?? "[]")!;

        return new ValueConverter<IReadOnlyList<T>, string>(
            v => serializeFunc(v),
            v => deserializeFunc(v));
    }

    public static PropertyBuilder<IReadOnlyList<T>> HasJsonReadOnlyListConversion<T>(
        this PropertyBuilder<IReadOnlyList<T>> builder)
        => builder.HasConversion(BuildJsonReadOnlyListConvertor<T>());

    public static ComplexTypePropertyBuilder<T> HasJsonListConversion<T>(this ComplexTypePropertyBuilder<T> builder)
        => builder.HasConversion(BuildJsonReadOnlyListConvertor<T>());

    public static PropertyBuilder<TCollection> HasJsonListConversion<TCollection, T>(
        this PropertyBuilder<TCollection> builder)
        where TCollection : IList<T>
        => builder.HasConversion(BuildJsonListConvertor<TCollection>());

    public static PropertyBuilder<TProperty> HasColumnNameSameAsProperty<TProperty>(
        this PropertyBuilder<TProperty> builder)
        => builder.HasColumnName(builder.Metadata.PropertyInfo?.Name);

    public static ComplexTypePropertyBuilder<TProperty> HasColumnNameSameAsProperty<TProperty>(
        this ComplexTypePropertyBuilder<TProperty> builder)
        => builder.HasColumnName(builder.Metadata.PropertyInfo?.Name);
}