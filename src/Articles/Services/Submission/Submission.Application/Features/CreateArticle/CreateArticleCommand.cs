using System.Text.Json.Serialization;
using Articles.Abstractions;
using Articles.Abstractions.Enums;
using Blocks.Core.FluentValidation;
using Blocks.Domain;
using FluentValidation;
using MediatR;

namespace Submission.Application.Features.CreateArticle;

public record CreateArticleCommand(int JournalId, string Title, ArticleType Type, string Scope)
    : ArticleCommand
{
    public override ArticleActionType ActionType => ArticleActionType.CreateArticle;
}

public class CreateArticleCommandValidator : AbstractValidator<CreateArticleCommand>
{
    public CreateArticleCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmptyWithMessage(nameof(CreateArticleCommand.Title));
        RuleFor(x => x.Scope)
            .NotEmptyWithMessage(nameof(CreateArticleCommand.Scope));
        RuleFor(x => x.JournalId)
            .GreaterThan(0).WithMessageForInvalidId(nameof(CreateArticleCommand.JournalId));
    }
}