using Blocks.Core.Constraints;
using Blocks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;

namespace Submission.Persistence.EntityConfigurations;

internal class AssetTypeDefinitionEntityConfiguration : IEntityTypeConfiguration<AssetTypeDefinition>
{
    public void Configure(EntityTypeBuilder<AssetTypeDefinition> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Name).IsUnique();

        builder.Property(e => e.Name).HasEnumConversion().HasMaxLength(MaxLength.C64).IsRequired().HasColumnOrder(1);

        builder.Property(e => e.MaxAssetCount).HasDefaultValue(0);

        builder.Property(e => e.DefaultFileExtension).HasMaxLength(MaxLength.C8).HasDefaultValue("pdf").IsRequired();

        builder.ComplexProperty(e => e.AllowedFileExtensions, builder =>
        {
            var convertor = BuilderExtensions.BuildJsonReadOnlyListConvertor<string>();
            builder.Property(e => e.Extensions)
                .HasConversion(convertor)
                .HasColumnName(builder.Metadata.PropertyInfo!.Name)
                .IsRequired();
        });
    }
}