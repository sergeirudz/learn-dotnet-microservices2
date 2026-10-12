using Auth.Grpc;
using Blocks.EntityFrameworkCore;
using Blocks.Exceptions;
using Grpc.Core;
using Journals.Grpc;

namespace Submission.Application.Features.ApproveArticle;

public class ApproveArticleCommandHandler(
    ArticleRepositoryBase articleRepositoryBase,
    PersonRepositoryBase personRepositoryBase,
    IPersonService _personClient,
    IJournalService _journalService
)
    : IRequestHandler<ApproveArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(ApproveArticleCommand command, CancellationToken ct)
    {
        var article = await articleRepositoryBase.FindByIdOrThrowAsync(command.ArticleId);


        if (!await IsEditorAssignedToJournal(article.JournalId, command.CreatedById))
        {
            throw new BadRequestException($"Editor is not assigned to the article's Journal (Id: {article.JournalId})");
        }

        Person editor = await GetOrCreatePersonByUserId(command.CreatedById, ct);

        article.Approve(editor);

        await articleRepositoryBase.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }

    private async Task<Person> GetOrCreatePersonByUserId(int userId, CancellationToken ct)
    {
        var person = await personRepositoryBase.GetByUserIdAsync(userId);

        if (person is null)
        {
            var response = await _personClient.GetPersonByUserIdAsync(new GetPersonByUserIdRequest
                {
                    UserId = userId,
                },
                new CallOptions(cancellationToken: ct));
            person = Person.Create(response.PersonInfo);
            await personRepositoryBase.AddAsync(person, ct);
        }

        return person;
    }

    private async Task<bool> IsEditorAssignedToJournal(int journalId, int userId)
    {
        var response = await _journalService.IsEditorAssignedToJournalAsync(new IsEditorAssignedToJournalRequest
        {
            JournalId = journalId,
            UserId = userId
        });

        return response.IsAssigned;
    }
}