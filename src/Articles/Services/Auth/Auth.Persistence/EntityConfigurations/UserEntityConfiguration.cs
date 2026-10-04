using Auth.Domain.Users;
using Blocks.EntityFrameworkCore.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Persistence.EntityConfigurations;

internal class UserEntityConfiguration : EntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        // If allowed to delete user, need to allow to delete user roles
        builder.HasMany(e => e.UserRoles).WithOne().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        // user has multiple refresh tokens, refresh token has one user, foreign key is user id inside refresh token, if user is deleted, delete all refresh tokens
        builder.HasMany(e => e.RefreshTokens).WithOne().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

        // Person has One User, User has One Person
        builder.HasOne(u => u.Person).WithOne(p => p.User)
            .HasForeignKey<User>(u => u.PersonId)
            .IsRequired()
            // If we delete user, we should not Delete Person. Set User Inactive instead
            .OnDelete(DeleteBehavior.Restrict);
    }
}