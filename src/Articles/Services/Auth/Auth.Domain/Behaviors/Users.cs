using Auth.Domain.Users.ValueObjects;
using Blocks.Core.Extensions;

namespace Auth.Domain.Users;

public partial class Users
{
    public static Users Create(IUserCreationInfo userInfo)
    {
        if (userInfo.UserRoles.IsNullOrEmpty())
            throw new ArgumentException("User must have at least one role.", nameof(userInfo.UserRoles));

        var user = new Users
        {
            UserName = userInfo.Email,
            Email = userInfo.Email,
            FirstName = userInfo.FirstName,
            LastName = userInfo.LastName,
            Gender = userInfo.Gender,
            PhoneNumber = userInfo.PhoneNumber,
            PictureUrl = userInfo.PictureUrl,
            Honorific = HonorificTitle.FromEnum(userInfo.Honorific),
            ProfessionalProfile =
                ProfessionalProfile.Create(userInfo.Position, userInfo.CompanyName, userInfo.Affiliation),
            _userRoles = userInfo.UserRoles.Select(r => UserRole.Create(r)).ToList(),
        };

        //insight - UserCreated is published from CreateUserEndpoint, not from the aggregate: the reset-password token it carries only exists there
        return user;
    }

    public void AssignRefreshToken(RefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }
}