using Articles.Abstractions;
using Articles.Abstractions.Enums;
using FluentValidation;
using MediatR;

namespace Submission.Application.Features.CreateArticle;

public record CreateArticleCommand(int JournalId, string Title, string Scope, ArticleType ArticleType)
    : IRequest<IdResponse>
{
}

public class CreateArticleCommandValidator : AbstractValidator<CreateArticleCommand>
{
    public CreateArticleCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title cant be empty");
        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Cope cant be empty");
        RuleFor(x => x.JournalId)
            .GreaterThan(0).WithMessage("Invalid journal id");
    }
}