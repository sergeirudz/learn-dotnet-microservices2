using Blocks.Core.Cache;
using Blocks.Domain.Entities;

namespace Submission.Domain.Entities;

public partial class AssetTypeDefinition : EnumEntity<AssetType>, ICacheable
{
    public required byte MaxFileSizeInMB { get; init; }
    public byte MaxAssetCount { get; init; }

    public int MaxFileSizeInBytes => (MaxFileSizeInMB * 1024 * 1024);
    public bool AllowMultipleAssets => MaxAssetCount > 1;

    public required string DefaultFileExtension { get; init; } = default!;
    public required FileExtensions AllowedFileExtensions { get; init; }
}