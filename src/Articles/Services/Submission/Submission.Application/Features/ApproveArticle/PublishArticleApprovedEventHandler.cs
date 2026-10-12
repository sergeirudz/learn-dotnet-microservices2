using Articles.Abstractions.Events;
using Articles.Abstractions.Events.Dtos;
using Mapster;
using MassTransit;
using Submission.Domain.Events;

namespace Submission.Application.Features.ApproveArticle;

public class PublishArticleApprovedEventHandler(
    ArticleRepositoryBase articleRepositoryBase,
    IPublishEndpoint _publishEndpoint,
    CancellationToken ct)
    : INotificationHandler<ArticleApproved>
{
    public async Task Handle(ArticleApproved notification, CancellationToken ct)
    {
        var article = await articleRepositoryBase.GetFullArticleByIdAsync(notification.Article.Id, ct);

        var articleDto = article.Adapt<ArticleDto>();

        await _publishEndpoint.Publish(new ArticleApprovedForReviewEvent(articleDto));
    }
}