using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Blocks.Core;
using Blocks.Domain.ValueObjects;

namespace Review.Domain.Shared.ValueObjects;

public class EmailAddress : StringValueObject
{
    [JsonConstructor]
    private EmailAddress(string value) => Value = value;

    public static EmailAddress Create(string value)
    {
        Guard.ThrowIfNullOrWhiteSpace(value);
        Guard.ThrowIfFalse(IsValidEmail(value), "Invalid email format.");

        return new EmailAddress(value.ToLower());
    }

    private static bool IsValidEmail(string email)
    {
        // Basic email regex for validation
        const string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailRegex, RegexOptions.IgnoreCase);
    }

    public static implicit operator EmailAddress(string value) => Create(value);

    public static implicit operator string(EmailAddress email) => email.Value;

    public static bool operator ==(EmailAddress a, string b)
    {
        if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) return true;
        if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
        return string.Equals(a.Value, b, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator !=(EmailAddress a, string b) => !(a == b);

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
}