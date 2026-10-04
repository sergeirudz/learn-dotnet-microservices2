using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;
using Blocks.EntityFrameworkCore;
using Blocks.EntityFrameworkCore.EntityConfigurations;

namespace Submission.Persistence.EntityConfigurations;

internal class ArticleEntityConfiguration : EntityConfiguration<Article>
{
    public override void Configure(EntityTypeBuilder<Article> builder)
    {
        base.Configure(builder); // Use this base to generate the Id
        // 256 2048 binary number. DB handler binary aligned numbers more efficiently
        builder.Property(e => e.Title).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Scope).HasMaxLength(2048).IsRequired();

        // Instead of manually repeating the code. Use a generic HasEnumConversion extension method
        builder.Property(e => e.Stage).HasEnumConversion();
        builder.Property(e => e.Type).HasEnumConversion();
        // Specify one-to-many relations
        builder.HasOne(e => e.Journal).WithMany(e => e.Articles)
            .HasForeignKey(e => e.JournalId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Assets).WithOne(e => e.Article)
            .HasForeignKey(e => e.ArticleId)
            .IsRequired()
            // If allowed to article, then need to delete all its assets
            .OnDelete(DeleteBehavior.Cascade);
    }
}