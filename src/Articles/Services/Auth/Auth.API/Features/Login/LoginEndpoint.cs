using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Blocks.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text;
using Articles.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Auth.API.Features.Login;

[AllowAnonymous]
[HttpPost("/tokens")]
[Tags("Auth")]
public class LoginEndpoint(
    UserManager<Users> _userManager,
    SignInManager<Users> _signInManager,
    IOptions<JwtOptions> _jwtOptions
)
    : Endpoint<LoginCommand, LoginResponse>
{
    public override async Task HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        if (user is null)
        {
            ThrowError("User not found", (int)HttpStatusCode.BadRequest);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new BadRequestException($"Invalid credentials for {command.Email}");
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        var jwtToken = GenerateJWTToken(user.Id.ToString(), user.FullName, command.Email, userRoles,
            Array.Empty<Claim>());
    }

    public string GenerateJWTToken(string userId, string fullName, string email, IEnumerable<string> roles,
        IEnumerable<Claim> additionalClaims)
    {
        var jwtSettings = _jwtOptions.Value;


        var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.UtcNow.ToUnixEpochDate().ToString(),
                    ClaimValueTypes.Integer64),

                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Email, email),
            }
            .Concat(roles.Select(r => new Claim(ClaimTypes.Role, r)))
            .Concat(additionalClaims);

        var secretKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(jwtSettings.Secret));
        var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            notBefore: DateTime.UtcNow,
            claims: claims,
            expires: jwtSettings.Expiration);

        // Take everything in jwtToken, and encode it into a string that can be sent to the client
        var encodedJwtToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);
        return encodedJwtToken;
    }
}