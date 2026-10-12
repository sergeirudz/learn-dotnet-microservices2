namespace Review.Domain.Articles;

public class ReviewerSpecialization
{
    public required int ReviewerId { get; init; }
    public Reviewer Reviewer { get; init; } = null!;
    public required int JournalId { get; init; }
    public Journal Journal { get; init; } = null!;

    public override int GetHashCode()
    {
        return HashCode.Combine(JournalId, ReviewerId);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ReviewerSpecialization other)
            return false;

        return JournalId == other.JournalId && ReviewerId == other.ReviewerId;
    }
}