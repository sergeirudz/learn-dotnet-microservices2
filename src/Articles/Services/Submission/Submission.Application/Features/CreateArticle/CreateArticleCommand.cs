using System.Text.Json.Serialization;
using Articles.Abstractions;
using Articles.Abstractions.Enums;
using Blocks.Domain;
using FluentValidation;
using MediatR;

namespace Submission.Application.Features.CreateArticle;

public record CreateArticleCommand(int JournalId, string Title, ArticleType Type, string Scope)
    : IAuditableAction, IRequest<IdResponse>
{
    [JsonIgnore] public DateTime CreatedOn => DateTime.UtcNow;
    [JsonIgnore] public int CreatedById { get; set; }
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