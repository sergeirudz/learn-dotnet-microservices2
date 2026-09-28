using Articles.Abstractions.Enums;
using Blocks.Domain.ValueObjects;

namespace Submission.Domain.ValueObjects;

public class AssetName : StringValueObject
{
    private AssetName(string value) => Value = value;

    // Will control how the name will be created through the factory method
    public static AssetName FromAssetType(AssetTypeDefinition assetType)
    {
        return new AssetName(assetType.Name.ToString());
    }
}