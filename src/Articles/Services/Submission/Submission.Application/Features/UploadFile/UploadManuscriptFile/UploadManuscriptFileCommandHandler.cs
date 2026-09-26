using Blocks.EntityFramework;

namespace Submission.Application.Features.UploadFile.UploadManuscriptFile;

public class UploadManuscriptFileCommandHandler(ArticleRepository _articleRepository)
    : IRequestHandler<UploadManuscriptFileCommand, IdResponse>
{
    public async Task<IdResponse> Handle(UploadManuscriptFileCommand command, CancellationToken cancellationToken)
    {
        var article = await _articleRepository.GetByIdOrThrowAsync(command.ArticleId);

        // dont have id yet
        return new IdResponse(0);
    }
}