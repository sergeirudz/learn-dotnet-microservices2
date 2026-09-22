using Articles.Abstractions.Enums;
using Blocks.Domain.Entities;

namespace Submission.Domain.Entities;

/*
 * One class for Journal properties and one for behaviors
 * Partial class in the same namespace
 *
 */
public partial class Journal : IEntity
{
    public Article CreateArticle(string title, ArticleType Type, string scope)
    {
        var article = new Article()
        {
            Title = title,
            Type = Type,
            Scope = scope,
            Journal = this,
            Stage = ArticleStage.Created
        };
        _articles.Add(article);
        // TODO add domain event later
        return article;
    }
}