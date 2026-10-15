using Articles.Abstractions.Events.Dtos;
using Review.Domain.Assets;

namespace Review.Domain.Articles.Behaviors;

public partial class Article
{
    public static Article ArticleSubmitted(ArticleDto articleDto,
        IEnumerable<ArticleActor> actors,
        IEnumerable<ArticleActor> assets)
    {
        var article = new Article
        {
            Id = articleDto.Id,
            JournalId = articleDto.Journal.Id,
            Title = articleDto.Title,
            Type = articleDto.Type,
            Scrope = articleDto.Scope,
            SubmittedById = articleDto.SubmittedBy.UserId,
            SubmittedOn = articleDto.SubmittedBy.Id,
            SubmittedOn = articleDto.SubmittedOn,
            Stage = articleDto.Stage
        };
        article._actors.Addrange(actors);
        article._assets.Addrange(assets);

        return article;
    }
}