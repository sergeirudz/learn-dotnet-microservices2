namespace Submission.Domain.Entities;

public partial class Asset
{
    // Declare an empty private constructor so only Create factory can invoke constructor
    private Asset()
    {
    }

    // Method is internal, we cant to create asset through the article
    // Article is part of the Domain
    // Only Entities from Domain can create asset through this factory method
    internal static Asset Create(Article article, AssetTypeDefinition type)
    {
        return new Asset()
        {
            ArticleId = article.Id,
            Article = article,
            Name = AssetName.FromAssetType(type),
            AssetType = type.Name
        };
    }
}