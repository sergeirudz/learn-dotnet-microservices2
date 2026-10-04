using Articles.Abstractions;
using Articles.Abstractions.Enums;

namespace Auth.Domain.Users;

public interface IUserCreationInfo : IPersonCreationInfo
{
    string? PhoneNumber { get; }
    IReadOnlyList<IUserRole> UserRoles { get; }
}

public interface IUserRole
{
    DateTime? ExpiringDate { get; }
    DateTime? StartDate { get; }
    UserRoleType RoleId { get; }
}