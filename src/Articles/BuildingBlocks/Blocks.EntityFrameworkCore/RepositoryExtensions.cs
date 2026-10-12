using System.Linq.Expressions;
using Blocks.Core;
using Blocks.Domain.Entities;
using Blocks.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFrameworkCore;

public static class RepositoryExtensions
{
    public static async Task<TEntity> FindByIdOrThrowAsync<TEntity, TContext>(
        // Because Static method FindByIdOrThrowAsync injects `this RepositoryBase`
        // I can use this method in `var journal = await _journalRepository.FindByIdOrThrowAsync(command.JournalId)` 
        this RepositoryBase<TContext, TEntity> repositoryBase, int id)
        where TContext : DbContext
        where TEntity : class, IEntity
    {
        var entity = await repositoryBase.FindByIdAsync(id);
        if (entity is null)
        {
            throw new NotFoundException($"{typeof(TEntity).Name} not found");
        }

        return entity;
    }

    public static async Task<TEntity> FindByIdOrThrowAsync<TEntity>(this DbSet<TEntity> dbSet, int id)
        where TEntity : class, IEntity
    {
        var entity = await dbSet.FindAsync(id);
        if (entity is null)
        {
            throw new NotFoundException($"{typeof(TEntity).Namespace} not found");
        }

        return entity;
    }

    //insight about the difference between FindByIdAsync and GetByIdAsync
    public static async Task<TEntity> GetByIdOrThrowAsync<TEntity, TContext>(
        this RepositoryBase<TContext, TEntity> repositoryBase, int id)
        where TContext : DbContext
        where TEntity : class, IEntity
    {
        var entity = await repositoryBase.GetByIdAsync(id);
        if (entity is null)
        {
            throw new NotFoundException($"{typeof(TEntity).Name} not found");
        }

        return entity!;
    }

    public static async Task<TEntity> SingleOrThrowAsync<TEntity>(this IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        where TEntity : class, IEntity<int>
        => Guard.NotFound(await source.SingleOrDefaultAsync(predicate, ct));
}