using Blocks.EntityFrameworkCore;
using Blocks.Domain.Entities;

namespace Submission.Persistence.Repositories;

// Shortcut that allows to avoid adding
// `SubmissionDbContext` every time when extending `: RepositoryBase<SubmissionDbContext, Article>(dbContext)

public class RepositoryBase<TEntity>(SubmissionDbContext dbContext)
    : RepositoryBase<SubmissionDbContext, TEntity>(dbContext)
    where TEntity : class, IEntity
{
}