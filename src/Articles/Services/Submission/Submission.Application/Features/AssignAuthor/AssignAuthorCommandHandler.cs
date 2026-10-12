using Blocks.EntityFrameworkCore;
using Submission.Persistence;

namespace Submission.Application.Features.AssignAuthor;

public class AssignAuthorCommandHandler(ArticleRepositoryBase articleRepositoryBase, SubmissionDbContext _dbContext)
    : IRequestHandler<AssignAuthorCommand, IdResponse>
{
    public async Task<IdResponse> Handle(AssignAuthorCommand command, CancellationToken cancellationToken)
    {
        var author = await _dbContext.Authors.FindByIdOrThrowAsync(command.AuthorId);

        var article = await articleRepositoryBase.GetByIdOrThrowAsync(command.ArticleId);

        article.AssignAuthor(author, command.ContributionAreas, command.IsCorrespondingAuthor);

        await articleRepositoryBase.SaveChangesAsync();

        return new IdResponse(article.Id);
    }
}