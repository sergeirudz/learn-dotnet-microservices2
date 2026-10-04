using Auth.Domain.Persons;
using Blocks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Persistence.Repositories;

public class PersonRepository(AuthDbContext dbContext)
    : Repository<AuthDbContext, Person>(dbContext)
{
    public async Task<Person?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => await Entity
            .Include(e => e.User)
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);
}