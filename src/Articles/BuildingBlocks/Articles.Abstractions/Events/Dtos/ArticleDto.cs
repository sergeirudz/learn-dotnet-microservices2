using Articles.Abstractions.Enums;

namespace Articles.Abstractions.Events.Dtos;

public record ArticleDto(
    int Id,
    string Title,
    string Scope,
    string? Doi,
    ArticleType Type,
    ArticleStage Stage,
    JournalDto Journal,
    DateTime? SubmittedOn,
    PersonDto? SubmittedBy,
    List<ActorDto> Actors,
    List<AssetDto> Assets
);