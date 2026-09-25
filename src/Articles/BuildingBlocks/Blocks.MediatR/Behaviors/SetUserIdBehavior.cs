using Blocks.Domain;
using MediatR;

namespace Blocks.MediatR.Behaviors;

public class SetUserIdBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableAction
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        request.CreatedById = 1;
        return await next();
    }
}