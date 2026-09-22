using Articles.Abstractions.Enums;

namespace Submission.Domain.Entities;

public class Article
{
    public int Id { get; init; }
    public required string Title { get; set; }
    public required string Scope { get; set; }

    public required ArticleType Type { get; set; }

    // internal set, meaning can only be set inside the domain, once assigned
    public ArticleStage Stage { get; internal set; }
    public int JournalId { get; init; }
    public Journal Journal { get; init; }
}