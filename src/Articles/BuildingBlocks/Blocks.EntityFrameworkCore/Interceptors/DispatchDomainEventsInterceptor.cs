using Blocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Blocks.EntityFrameworkCore.Interceptors;

public class DispatchDomainEventsInterceptor(IMediator _mediator) : SaveChangesInterceptor
{
    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }


    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        return await base.SavingChangesAsync(eventData, result, ct);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken ct = default)
    {
        result = await base.SavedChangesAsync(eventData, result, ct);

        if (eventData.Context is not null)
            await _mediator.DispatchDomainEventsAsync(eventData.Context, ct);

        return result;
    }
}