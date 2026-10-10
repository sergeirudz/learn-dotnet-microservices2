using Blocks.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFrameworkCore;

public static class MediatorExtensions
{
    public static async Task<int> DispatchDomainEventsAsync(this IMediator mediator, DbContext dbContext,
        CancellationToken ct)
    {
        // Changetracker is collecting all the entities that are being tracked by the DbContext.
        var domainAggregates = dbContext.ChangeTracker.Entries<IAggregateEntity>();

        // Unify all the domain events from all the aggregates into a single list.
        var domainEvents = domainAggregates
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        domainAggregates.ToList().ForEach(x => x.Entity.ClearDomainEvents());


        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, ct);
        }

        return domainEvents.Count;
    }
}