using Blocks.EntityFramework.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;

namespace Submission.Persistence.EntityConfigurations;

public class PersonEntityConfiguration : EntityConfiguration<Person>
{
    public override void Configure(EntityTypeBuilder<Person> builder)
    {
        base.Configure(builder);
        builder.HasIndex(e => e.UserId).IsUnique();

        // Tell EF that there is a type discriminator
        builder.HasDiscriminator(e => e.TypeDiscriminator)
            // Specify all the classes from the Hierarchy
            .HasValue<Person>(nameof(Person))
            .HasValue<Author>(nameof(Author));

        builder.Property(e => e.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(64);
        builder.Property(e => e.Affiliation).HasMaxLength(512).IsRequired()
            .HasComment("Institution or organization they are associated with when they conduct their research");

        // For value objects use ComplexProperty
        builder.ComplexProperty(
            o => o.EmailAddress, builder =>
            {
                // Configure Value of the Valueobject
                builder.Property(n => n.Value)
                    // Specify column name or EF will generate itself
                    .HasColumnName(builder.Metadata.PropertyInfo!.Name)
                    .HasMaxLength(64);
            });
    }
}