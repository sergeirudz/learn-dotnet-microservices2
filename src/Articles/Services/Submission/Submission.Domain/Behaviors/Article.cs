using Articles.Abstractions.Enums;
using Blocks.Domain;

namespace Submission.Domain.Entities;

public partial class Article
{
    public void AssignAuthor(Author author, HashSet<ContributionArea> contributionAreas, bool isCorrespondingAuthor)
    {
        var role = isCorrespondingAuthor ? UserRoleType.CORAUT : UserRoleType.AUT;

        if (Actors.Exists(a => a.PersonId == author.Id && a.Role == role))
        {
            throw new DomainException($"Author {author.EmailAddress} is already assigned to the article");
        }

        Actors.Add(new ArticleAuthor()
        {
            ContributionAreas = contributionAreas,
            Person = author,
            Role = role
        });

        // TODO add DomainEvent
    }


    public Asset CreateAsset(AssetTypeDefinition type)
    {
        var assetCount = _assets
            .Where(a => a.AssetType == type.Id)
            .Count();

        if (assetCount >= type.MaxAssetCount)
            throw new DomainException(
                $"The maximum number of files, {type.MaxAssetCount}, allowed for {type.Name.ToString()} was already reached");

        var asset = Asset.Create(this, type);
        _assets.Add(asset);

        return asset;
    }

    public void Submit(IArticleAction<ArticleActionType> action, ArticleStateMachineFactory _stateMachineFactory)
    {
        // check if all mandatory contribution areas are covered
        // var contributionAreas = _actors.OfType<ArticleAuthor>()
        //     .SelectMany(author => author.ContributionAreas)
        //     .ToHashSet();
        //
        // var missingMandatoryAreas = ContributionAreaCategories.MandatoryAreas
        //     .Except(contributionAreas)
        //     .ToList();
        //
        // if (missingMandatoryAreas.Count > 0)
        //     throw new DomainException(
        //         $"Cannot submit article: Missing mandatory contribution areas: {string.Join(", ", missingMandatoryAreas)}");

        SubmittedById = action.CreatedById;
        SubmittedOn = action.CreatedOn;

        // todo add an ArticleSubmitted domain event
        SetStage(ArticleStage.Submitted, action, _stateMachineFactory);
    }
}