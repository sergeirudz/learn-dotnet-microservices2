using Auth.Grpc;
using Blocks.EntityFrameworkCore;
using Blocks.Exceptions;
using Journals.Grpc;

namespace Submission.Application.Features.ApproveArticle;

public class ApproveArticleCommandHandler(
    ArticleRepository _articleRepository,
    PersonRepository _personRepository
)
    : IRequestHandler<ApproveArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(ApproveArticleCommand command, CancellationToken ct)
    {
        var article = await _articleRepository.FindByIdOrThrowAsync(command.ArticleId);

        // if (!await IsEditorAssignedToJournal(article.JournalId, command.CreatedById))
        //     throw new BadRequestException($"Editor is not assigned to the article's Journal (Id: {article.JournalId})");

        var editor = await _personRepository.GetByUserIdAsync(command.CreatedById);

        article.Approve(editor);

        await _articleRepository.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }

    // private async Task<bool> IsEditorAssignedToJournal(int journalId, int userId)
    // {
    //     var response = await _journalClient.IsEditorAssignedToJournalAsync(new IsEditorAssignedToJournalRequest
    //     {
    //         JournalId = journalId,
    //         UserId = userId
    //     });
    //
    //     return response.IsAssigned;
    // }
}