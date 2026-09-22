using Blocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFramework;

public interface IRepository<TEntity>
    where TEntity : class, IEntity
{
    Task<TEntity?> FindByIdAsync(int id);
    Task<TEntity?> GetByIdAsync(int id);
    Task<TEntity?> AddAsync(TEntity entity);
    TEntity Updated(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> DeleteByIdAsync(int id);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public class Repository<TContext, TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity
{
    protected readonly TContext _dbContext;
    protected readonly DbSet<TEntity> _entity;
    public string TableName => _dbContext.Model.FindEntityType(typeof(TEntity))?.GetTableName()!;

    public Repository(TContext dbContext)
    {
        _dbContext = dbContext;
        _entity = _dbContext.Set<TEntity>();
    }

    public TContext Context => _dbContext;
    public virtual DbSet<TEntity> Entity => _entity;

    // Gives derived repositories way to build queries and include related entities
    protected virtual IQueryable<TEntity> Query() => _entity;

    public virtual async Task<TEntity?> FindByIdAsync(int id)
        => await _entity.FindAsync(id);

    public virtual async Task<TEntity?> GetByIdAsync(int id)
        => await Query().SingleOrDefaultAsync(e => e.Id.Equals(id));

    public virtual async Task<TEntity> AddAsync(TEntity entity)
        => (await _entity.AddAsync(entity)).Entity;

    public virtual TEntity Update(TEntity entity)
        => _entity.Update(entity).Entity;

    public virtual void Remove(TEntity entity)
        => _entity.AddAsync(entity);

    public virtual async Task<bool> DeleteByIdAsync(int id)
    {
        var rowsAffected =
// Query faster. But needs to create relations between objects with DeleteCascade behavior, or it will Error
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM {TableName} WHERE Id = {id}");
        return rowsAffected > 0;
    }

    // Have a proxy method for saving
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _dbContext.SaveChangesAsync(ct);
}