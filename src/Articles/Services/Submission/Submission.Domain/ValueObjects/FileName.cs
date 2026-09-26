using Blocks.Domain.ValueObjects;
using Submission.Domain.Entities;

namespace Submission.Domain.ValueObjects;

public class FileName : StringValueObject
{
    private FileName(string value) => Value = value;


    // Will control how the name will be created through the factory method
    public static FileName Create(Asset asset, string extension)
    {
        var assetName = asset.Name.Value;

        return new FileName($"{assetName}.{extension}");
    }
}