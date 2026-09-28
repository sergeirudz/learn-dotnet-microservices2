using Blocks.EntityFramework;

namespace Submission.Application.Features.UploadFile.UploadManuscriptFile;

public class UploadManuscriptFileCommandHandler(
    ArticleRepository _articleRepository,
    AssetTypeDefinitionRepository _assetTypeRepository)
    : IRequestHandler<UploadManuscriptFileCommand, IdResponse>
{
    public async Task<IdResponse> Handle(UploadManuscriptFileCommand command, CancellationToken cancellationToken)
    {
        var article = await _articleRepository.GetByIdOrThrowAsync(command.ArticleId);

        var assetType = _assetTypeRepository.GetById(command.AssetType);

        Asset? asset = null;
        if (!assetType.AllowMultipleAssets)
        {
            asset = article.Assets.SingleOrDefault(e => e.AssetType == assetType.Id);
        }

        if (asset is null)
        {
            asset = article.CreateAsset(assetType);
        }

        // TODO: upload the file
        await _articleRepository.SaveChangesAsync();

        return new IdResponse(asset.Id);
    }
}