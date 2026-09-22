using Blocks.EntityFramework.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Submission.Domain.Entities;

namespace Submission.Persistence.EntityConfigurations;

internal class JournalEntityConfiguration : EntityConfiguration<Journal>
{
    public override void Configure(EntityTypeBuilder<Journal> builder)
    {
        base.Configure(builder); // Use this base to generate the Id
        builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Abbreviation).HasMaxLength(8).IsRequired();
    }
}