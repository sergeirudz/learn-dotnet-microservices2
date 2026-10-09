using Blocks.Domain.Entities;
using Submission.Domain.ValueObjects;

namespace Submission.Domain.Entities;

public partial class Person : IEntity
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string FullName => FirstName + " " + LastName;
    public string? Title { get; set; }
    public required EmailAddress Email { get; init; }
    public required string Affiliation { get; init; }
    public int? UserId { get; init; }
    public IReadOnlyList<ArticleActor> Actors { get; private set; } = new List<ArticleActor>();
    public string TypeDiscriminator { get; init; } = null!; // EF discriminator
}