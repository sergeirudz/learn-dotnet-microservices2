using Blocks.Domain.Entities;
using Blocks.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFramework;

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
}