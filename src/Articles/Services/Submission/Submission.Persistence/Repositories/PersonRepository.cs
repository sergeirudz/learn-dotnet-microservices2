using Microsoft.EntityFrameworkCore;
using Submission.Domain.Entities;

namespace Submission.Persistence.Repositories;

public class PersonRepository(SubmissionDbContext dbContext)
    : Repository<Person>(dbContext)
{
    public async Task<Person> GetByUserIdAsync(int userId, bool throwIfNotFound = true)
        => await Entity.SingleOrDefaultAsync(e => e.UserId == userId);
    //
    // public async Task<Person> GetByEmail(string email, bool throwIfNotFound = true)
    // {
    //     var person = await Query()
    //         .SingleOrDefaultAsync(e => e.Email.Value.ToLower() == email.ToLower()); //not all databases are case-insensitive
    //
    //     return ReturnOrThrow(person, throwIfNotFound);
    // }
}