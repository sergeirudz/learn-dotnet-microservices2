using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;
using Blocks.EntityFramework;
using Blocks.EntityFramework.EntityConfigurations;

namespace Submission.Persistence.EntityConfigurations;

internal class ArticleEntityConfiguration : EntityConfiguration<Article>
{
    public override void Configure(EntityTypeBuilder<Article> builder)
    {
        base.Configure(builder); // Use this base to generate the Id
        // 256 2048 binary number. DB handler binary aligned numbers more efficiently
        builder.Property(e => e.Title).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Scope).HasMaxLength(2048).IsRequired();

        // builder.Property(e => e.Stage)
        //     // How we will store the value in DB
        //     .HasConversion(
        //         v => v.ToString(), // convert enum to string
        //         v => (ArticleStage)Enum.Parse(typeof(ArticleStage), v) // string -> enum
        //     );
        //
        // builder.Property(e => e.Type)
        //     .HasConversion(
        //         v => v.ToString(),
        //         v => (ArticleType)Enum.Parse(typeof(ArticleType), v)
        //     );
        // Instead of manually repeating the code. Use a generic HasEnumConversion extension method
        builder.Property(e => e.Stage).HasEnumConversion();
        builder.Property(e => e.Type).HasEnumConversion();
        // Specify one-to-many relations
        builder.HasOne(e => e.Journal).WithMany(e => e.Articles)
            .HasForeignKey(e => e.JournalId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}