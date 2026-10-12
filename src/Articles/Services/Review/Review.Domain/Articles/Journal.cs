using Blocks.Domain.Entities;

namespace Review.Domain.Articles;

public class Journal : Entity
{
    public required string Name { get; set; }
    public required string Abbreviation { get; set; }
}