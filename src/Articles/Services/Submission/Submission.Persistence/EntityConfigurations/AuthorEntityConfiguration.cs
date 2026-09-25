using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;

namespace Submission.Persistence.EntityConfigurations;

// Since Author inherits Person, its not a separate Entity with its own Table
// Cant use BaseEntityConfiguration, it will create a new Table
public class AuthorEntityConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.Property(e => e.Discipline).HasMaxLength(64)
            .HasComment("The author's main field of study or research (e.g., Biology, Computer Science).");
        builder.Property(e => e.Degree).HasMaxLength(64)
            .HasComment("The author's highest academic qualification (e.g., PhD in Mathematics, MSc in Chemistry).");
    }
}