namespace Blocks.Domain.ValueObjects;

public abstract class StringValueObject : IEquatable<StringValueObject>, IEquatable<string>
{
    public string Value { get; protected set; } = default!;

    public override string ToString() => Value.ToString();
    public override int GetHashCode() => Value.GetHashCode();

    public bool Equals(StringValueObject? other) => other is not null && Value.Equals(other.Value);
    public bool Equals(string? other) => Value.Equals(other);

    // Cast StringValueObject into a string
    public static implicit operator string(StringValueObject @object) => @object.Value;
}