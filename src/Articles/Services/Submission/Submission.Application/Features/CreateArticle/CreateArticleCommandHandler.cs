using Articles.Abstractions;
using Articles.Abstractions.Enums;
using Blocks.Exceptions;
using MediatR;
using Submission.Domain.Entities;
using Submission.Persistence.Repositories;
using Blocks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Submission.Application.Features.CreateArticle;

internal class CreateArticleCommandHandler(
    RepositoryBase<Journal> journalRepositoryBase)
    : IRequestHandler<CreateArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateArticleCommand command, CancellationToken ct)
    {
        var journal = await journalRepositoryBase.FindByIdOrThrowAsync(command.JournalId);

        // TODO: throw not found exception if journal not found
        var article = journal.CreateArticle(command.Title, command.Type, command.Scope);
        await AssignCurrentUserAsAuthor(article, command);
        await journalRepositoryBase.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }

    private async Task AssignCurrentUserAsAuthor(Article article, CreateArticleCommand command)
    {
        var author =
            await journalRepositoryBase.Context.Authors.SingleOrDefaultAsync(t => t.UserId == command.CreatedById);

        if (author is not null)
            article.AssignAuthor(author, [ContributionArea.OriginalDraft], isCorrespondingAuthor: true);
    }
}