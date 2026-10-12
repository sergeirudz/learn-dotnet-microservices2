using Blocks.EntityFrameworkCore;

namespace Submission.Application.Features.SubmitArticle;

public class SubmitArticleCommandHandler(
    ArticleRepositoryBase articleRepositoryBase,
    ArticleStateMachineFactory _stateMachineFactory)
    : IRequestHandler<SubmitArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(SubmitArticleCommand command, CancellationToken ct)
    {
        var article = await articleRepositoryBase.GetByIdOrThrowAsync(command.ArticleId, ct);

        article.Submit(command, _stateMachineFactory);

        await articleRepositoryBase.SaveChangesAsync(ct);

        return new IdResponse(article.Id);
    }
}