using Submission.Domain.ValueObjects;

namespace Submission.Domain.Entities;

public partial class Author
{
    public static Author Create(string email, string firstName, string lastName, string? title, string affiliation)
    {
        var author = new Author
        {
            Email = EmailAddress.Create(email),
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Affiliation = affiliation,
        };

        // TODO add AuthorCreated domain event
        return author;
    }
}