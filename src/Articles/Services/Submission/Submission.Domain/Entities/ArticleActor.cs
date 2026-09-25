using Articles.Abstractions.Enums;

namespace Submission.Domain.Entities;

// Link between Article & Person
public class ArticleActor
{
    public int ArticleId { get; init; }
    public Article Article { get; init; } = null!;
    public int PersonId { get; set; }
    public Person Person { get; init; } = null!;


    public UserRoleType Role { get; init; }

    public string TypeDiscriminator { get; init; } = null!; // EF discriminator to manage inheritance
}