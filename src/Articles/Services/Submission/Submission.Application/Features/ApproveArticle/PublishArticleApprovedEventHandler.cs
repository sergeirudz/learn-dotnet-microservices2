using Articles.Abstractions.Events;
using Articles.Abstractions.Events.Dtos;
using Mapster;
using MassTransit;
using Submission.Domain.Events;

namespace Submission.Application.Features.ApproveArticle;

public class PublishArticleApprovedEventHandler(
    ArticleRepository _articleRepository,
    IPublishEndpoint _publishEndpoint,
    CancellationToken cancellationToken)
    : INotificationHandler<ArticleApproved>
{
    public async Task Handle(ArticleApproved notification, CancellationToken cancellationToken)
    {
        var article = await _articleRepository.GetByIdAsync(notification.Article.Id);

        var articleDto = article.Adapt<ArticleDto>();

        await _publishEndpoint.Publish(new ArticleApprovedForReviewEvent(articleDto));
    }
}