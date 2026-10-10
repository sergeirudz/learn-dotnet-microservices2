using Blocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blocks.EntityFrameworkCore.EntityConfigurations;

public abstract class EntityConfiguration<T> : IEntityTypeConfiguration<T>
// Extend IEntity because need to say that it contains the `Id` prop 
    where T : class, IEntity
{
    // Use this when creating entities so you dont need to declare Id inside JournalEntityConfiguration
    // To allow override declare virtual
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        // Id is the primary key
        builder.HasKey(e => e.Id);
        // Id should be generated on creation. Id is the first in the order
        builder.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnOrder(0);
    }
}