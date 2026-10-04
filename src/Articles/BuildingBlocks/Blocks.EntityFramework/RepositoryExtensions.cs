using Blocks.Domain.Entities;
using Blocks.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFrameworkCore;

public static class RepositoryExtensions
{
    public static async Task<TEntity> FindByIdOrThrowAsync<TEntity, TContext>(
        // Because Static method FindByIdOrThrowAsync injects `this Repository`
        // I can use this method in `var journal = await _journalRepository.FindByIdOrThrowAsync(command.JournalId)` 
        this Repository<TContext, TEntity> repository, int id)
        where TContext : DbContext
        where TEntity : class, IEntity
    {
        var entity = await repository.FindByIdAsync(id);
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
        this Repository<TContext, TEntity> repository, int id)
        where TContext : DbContext
        where TEntity : class, IEntity
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new NotFoundException($"{typeof(TEntity).Name} not found");
        }

        return entity!;
    }
}