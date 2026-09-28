using Blocks.Domain.Entities;

namespace Submission.Domain.Entities;

public class AssetTypeDefinition : EnumEntity<AssetType>
{
    public required byte MaxFileSizeInMB { get; init; }
    public byte MaxAssetCount { get; init; }

    public int MaxFileSizeInBytes => (MaxFileSizeInMB * 1024 * 1024);
    public bool AllowMultipleAssets => MaxAssetCount > 1;

    public required string DefaultFileExtension { get; init; } = default!;
    public required FileExtensions AllowedFileExtensions { get; init; }
}