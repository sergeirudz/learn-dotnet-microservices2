using Articles.Abstractions.Enums;
using Blocks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;

namespace Submission.Persistence.EntityConfigurations;

// Link between two Entities. Article-Actor
public class ArticleActorEntityConfiguration : IEntityTypeConfiguration<ArticleActor>
{
    public void Configure(EntityTypeBuilder<ArticleActor> builder)
    {
        builder.HasKey(e => new { e.ArticleId, e.PersonId, e.Role });
        builder.Property(e => e.Role).HasEnumConversion().HasDefaultValue(UserRoleType.AUT);

        builder.HasDiscriminator(e => e.TypeDiscriminator)
            .HasValue<ArticleActor>(nameof(ArticleActor)) // Base Class
            .HasValue<ArticleAuthor>(nameof(ArticleAuthor)); // Inherited Class


        // This is a Link Entity, configure the relation on Both Sides
        builder.HasOne(aa => aa.Article) // This entity has one Article
            .WithMany(a => a.Actors) // has many Actors
            .HasForeignKey(aa => aa.ArticleId)
            .OnDelete(DeleteBehavior.Cascade); // When delete, delete connected Actors

        builder.HasOne(aa => aa.Person)
            .WithMany(a => a.ArticleActors)
            .HasForeignKey(aa => aa.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}