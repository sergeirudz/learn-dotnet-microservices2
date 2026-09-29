using Articles.Abstractions.Enums;
using Microsoft.AspNetCore.Identity;

namespace Auth.Domain.Roles;

public class Role : IdentityRole<int>
{
    public required UserRoleType Type { get; init; }
    public required string Description { get; set; }
}