using Articles.Abstractions;
using MediatR;

namespace Submission.Application.Features.CreateArticle;

internal class CreateArticleCommandHandler : IRequestHandler<CreateArticleCommand, IdResponse>
{
    public Task<IdResponse> Handle(CreateArticleCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}