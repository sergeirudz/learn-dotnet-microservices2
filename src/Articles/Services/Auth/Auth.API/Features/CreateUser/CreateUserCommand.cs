using Articles.Abstractions.Enums;

namespace Auth.API.Features.CreateUser;

public class CreateUserCommand
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int Gender { get; init; }

    public string? Honorific { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PictureUrl { get; init; }
    public string? CompanyName { get; init; }
    public string? Position { get; init; }
    public string? Affiliation { get; init; }

    public required IReadOnlyList<UserRoleDto> UserRoles { get; init; } = new List<UserRoleDto>();
}

public record UserRoleDto(
    UserRoleType RoleType,
    DateTime? StartDate,
    DateTime? ExpiringDate
)
{
    // public UserRoleType RoleId => RoleType;
}

public record CreateUserResponse(
    string Email,
    int UserId,
    string Token
);