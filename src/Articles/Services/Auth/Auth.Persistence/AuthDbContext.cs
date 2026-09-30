using Auth.Domain.Roles;
using Auth.Domain.Users;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Persistence;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) :
    IdentityDbContext<Users, Role, int>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
    }
}