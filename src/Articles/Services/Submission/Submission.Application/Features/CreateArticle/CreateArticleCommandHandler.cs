using Articles.Abstractions;
using Blocks.Exceptions;
using MediatR;
using Submission.Domain.Entities;
using Submission.Persistence.Repositories;
using Blocks.EntityFramework;

namespace Submission.Application.Features.CreateArticle;

internal class CreateArticleCommandHandler(
    Repository<Journal> _journalRepository)
    : IRequestHandler<CreateArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateArticleCommand command, CancellationToken ct)
    {
        var journal = await _journalRepository.FindByIdOrThrowAsync(command.JournalId);

        // TODO: throw not found exception if journal not found
        var article = journal.CreateArticle(command.Title, command.Type, command.Scope);
        await _journalRepository.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }
}