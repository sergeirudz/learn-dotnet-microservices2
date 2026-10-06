using Blocks.Redis;
using Redis.OM.Modeling;

namespace Journals.Domain.Journals;

[Document(StorageType = StorageType.Json, Prefixes = new[] { nameof(Journal) })]
public class Journal : Entity
{
    [Searchable] // Can do full-text search on this property
    public string Name { get; set; }

    [Indexed] public required string Abbreviation { get; set; }
    [Searchable] public string Description { get; set; }
    public string ISSN { get; init; }

    public int ChiefEditorId { get; set; }

    // Telling Redis we want to index the Section based on the Section.Name property
    [Indexed(JsonPath = "$.Name")] public List<Section> Sections { get; set; } = new();
}