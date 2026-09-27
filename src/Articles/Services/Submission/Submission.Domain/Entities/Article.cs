using Articles.Abstractions.Enums;
using Blocks.Domain.Entities;

namespace Submission.Domain.Entities;

public partial class Article : Entity
{
    public required string Title { get; set; }
    public required string Scope { get; set; }

    public required ArticleType Type { get; set; }

    // internal set, meaning can only be set inside the domain, once assigned
    public ArticleStage Stage { get; internal set; }
    public int JournalId { get; init; }
    public Journal Journal { get; init; }

    private readonly List<Asset> _assets = new();
    public IReadOnlyList<Asset> Assets => _assets.AsReadOnly();

    // Navigation collection for the Article-Actor link entity
    public List<ArticleActor> Actors { get; set; } = new();
}