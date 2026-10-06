using Journals.Domain.Enums;

namespace Journals.API.Features.Shared;

public record EditorDto(
    int Id,
    string FullName,
    string Affiliation)
{
    public EditorRole Role { get; set; }
}