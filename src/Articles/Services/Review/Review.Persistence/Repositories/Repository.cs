using Blocks.Domain.Entities;

namespace Review.Persistence.Repositories;

public class Repository<TEntity>(ReviewDbContext dbContext)
    : RepositoryBase<ReviewDbContext, TEntity>(dbContext)
    where TEntity : class, IEntity;