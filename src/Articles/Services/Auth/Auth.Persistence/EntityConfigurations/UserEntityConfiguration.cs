using Auth.Domain.Users;
using Blocks.Core.Constraints;
using Blocks.EntityFramework;
using Blocks.EntityFramework.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Persistence.EntityConfigurations;

internal class UserEntityConfiguration : EntityConfiguration<Users>
{
    public override void Configure(EntityTypeBuilder<Users> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(MaxLength.C64);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(MaxLength.C64);
        builder.Property(e => e.Gender).IsRequired().HasEnumConversion();

        // ComplexProperty cant be nullable, you need to use OwnsOne instead for Enums
        // builder.ComplexProperty(
        //     e => e.Honorific, b =>
        //     {
        //         b.Property(e => e.Value)
        //             .HasMaxLength(MaxLength.C32)
        //             .HasColumnName(nameof(Users.Honorific));
        //     }
        // );
        // OwnsOne instead of ComplexProperty because EF.Core doesnt support yet indexes on ComplexxProperty
        builder.OwnsOne(
            e => e.Honorific, b =>
            {
                b.Property(e => e.Value)
                    .HasMaxLength(MaxLength.C32)
                    .HasColumnName(nameof(Users.Honorific));
                b.WithOwner(); // required to avoid navigation issues
            });


        // OwnsOne istead of ComplexProperty because EF.Core doesnt support yet optional proparties with ComplexProperty
        builder.OwnsOne(
            e => e.Honorific, b =>
            {
                b.Property(e => e.Value).HasMaxLength(MaxLength.C32).HasColumnName(nameof(Users.Honorific));

                b.WithOwner(); // required to avoid navigation issues
            });

        builder.OwnsOne(e => e.ProfessionalProfile, b =>
        {
            b.Property(e => e.Position).HasMaxLength(MaxLength.C64).HasColumnNameSameAsProperty();
            b.Property(e => e.CompanyName).HasMaxLength(MaxLength.C32).HasColumnNameSameAsProperty();
            b.Property(e => e.Affiliation).HasMaxLength(MaxLength.C32).HasColumnNameSameAsProperty();

            b.WithOwner(); // required to avoid navigation issues
        });

        builder.Property(e => e.PictureUrl).HasMaxLength(MaxLength.C2048);
        // If allowed to delete user, need to allow to delete user roles
        builder.HasMany(e => e.UserRoles).WithOne().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}