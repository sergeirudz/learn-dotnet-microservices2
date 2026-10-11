using Articles.Abstractions.Enums;

namespace Articles.Abstractions.Events.Dtos;

public record AssetDto(
    int Id,
    string Name,
    int Number,
    // AssetState State,
    AssetType Type,
    FileDto File);