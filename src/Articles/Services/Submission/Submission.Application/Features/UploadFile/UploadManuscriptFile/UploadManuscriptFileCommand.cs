using System.ComponentModel.DataAnnotations;
using Blocks.Core.FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Submission.Application.Features.UploadFile;

public record UploadManuscriptFileCommand : ArticleCommand
{
    /// <summary>
    ///     The asset type of the file
    /// </summary>
    [Required]
    public AssetType AssetType { get; init; }

    /// <summary>
    ///     The file to be uploaded
    /// </summary>
    [Required]
    public IFormFile File { get; init; } = null!;


    public override ArticleActionType ActionType => ArticleActionType.UploadAsset;
}

public class UploadManuscriptCommandValidator : ArticleCommandValidator<UploadManuscriptFileCommand>
{
    public UploadManuscriptCommandValidator()

    {
        RuleFor(x => x.File)
            .NotNullWithMessage();
        // TODO: validate filesize file extension
        RuleFor(r => r.AssetType).Must(IsAssetTypeAllowed);
    }

    public IReadOnlyCollection<AssetType> AllowedAssetTypes => new HashSet<AssetType> { AssetType.Manuscript };


    private bool IsAssetTypeAllowed(AssetType assetType)
    {
        return AllowedAssetTypes.Contains(assetType);
    }
}