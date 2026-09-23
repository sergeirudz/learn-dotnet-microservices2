using Articles.Abstractions;
using MediatR;
using Submission.Domain.Entities;
using Submission.Persistence.Repositories;

namespace Submission.Application.Features.CreateArticle;

internal class CreateArticleCommandHandler(
    Repository<Journal> _journalRepository)
    : IRequestHandler<CreateArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateArticleCommand command, CancellationToken ct)
    {
        var journal = await _journalRepository.FindByIdAsync(command.JournalId);
        // TODO: throw not found exception if journal not found
        var article = journal.CreateArticle(command.Title, command.Type, command.Scope);
        await _journalRepository.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }
}