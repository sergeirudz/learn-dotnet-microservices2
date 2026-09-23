using Submission.Domain.ValueObjects;

namespace Submission.Domain.Entities;

public class Person
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string FullName => FirstName + " " + LastName;
    public string? Title { get; set; }
    public required EmailAddress EmailAddress { get; init; }
    public required string Affiliation { get; init; }
    public int? UserId { get; init; }
}