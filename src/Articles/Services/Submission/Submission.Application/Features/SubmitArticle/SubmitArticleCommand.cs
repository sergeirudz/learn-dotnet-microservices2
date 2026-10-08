namespace Submission.Application.Features.SubmitArticle;

public record SubmitArticleCommand : ArticleCommand
{
    public ArticleActionType ActionType => ArticleActionType.SubmitDraft;
}

public class SubmitArticleCommandValidator : ArticleCommandValidator<SubmitArticleCommand>;