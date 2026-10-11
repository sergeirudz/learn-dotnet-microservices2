using Articles.Abstractions.Events;
using Articles.Abstractions.Events.Dtos;
using Mapster;
using MassTransit;
using Submission.Domain.Events;

namespace Submission.Application.Features.ApproveArticle;

public class PublishArticleApprovedEventHandler(
    ArticleRepository _articleRepository,
    IPublishEndpoint _publishEndpoint,
    CancellationToken ct)
    : INotificationHandler<ArticleApproved>
{
    public async Task Handle(ArticleApproved notification, CancellationToken ct)
    {
        var article = await _articleRepository.GetFullArticleByIdAsync(notification.Article.Id, ct);

        var articleDto = article.Adapt<ArticleDto>();

        await _publishEndpoint.Publish(new ArticleApprovedForReviewEvent(articleDto));
    }
}