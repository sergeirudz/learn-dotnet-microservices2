using Blocks.EntityFrameworkCore;
using Blocks.Domain.Entities;

namespace Submission.Persistence.Repositories;

// Shortcut that allows to avoid adding
// `SubmissionDbContext` every time when extending `: Repository<SubmissionDbContext, Article>(dbContext)

public class Repository<TEntity>(SubmissionDbContext dbContext)
    : Repository<SubmissionDbContext, TEntity>(dbContext)
    where TEntity : class, IEntity
{
}