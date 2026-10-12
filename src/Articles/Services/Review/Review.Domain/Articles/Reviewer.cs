using Review.Domain.Shared;

namespace Review.Domain.Articles;

public class Reviewer : Person
{
    private HashSet<ReviewerSpecialization> _specializations = new();
    public IReadOnlyCollection<ReviewerSpecialization> Specializations => _specializations;


    public override string TypeDiscriminator => nameof(Reviewer);
}