using Review.Domain.Shared;

namespace Review.Domain.Articles;

public class Editor : Reviewer
{
    public override string TypeDiscriminator => nameof(Editor);
}