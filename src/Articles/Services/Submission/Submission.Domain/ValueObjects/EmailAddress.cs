using System.Text.RegularExpressions;
using Blocks.Core;
using Blocks.Domain.ValueObjects;

namespace Submission.Domain.ValueObjects;

public class EmailAddress : StringValueObject
{
    private EmailAddress(string value) => Value = value;

    // Factory method how Email is created
    public static EmailAddress Create(string value)
    {
        Guard.ThrowIfNullOrWhiteSpace(value);

        if (!IsValidEmail(value))
        {
            throw new ArgumentException("Invalid email format");
        }

        return new EmailAddress(value);
    }

    private static bool IsValidEmail(string email)
    {
        // Basic email regex for validation
        const string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailRegex, RegexOptions.IgnoreCase);
    }
}