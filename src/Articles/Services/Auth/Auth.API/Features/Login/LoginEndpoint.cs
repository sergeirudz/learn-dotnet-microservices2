using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Blocks.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Articles.Security;
using Microsoft.AspNetCore.Identity.Data;
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
        var refreshToken = GenerateRefreshToken(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        user.RefreshTokens.Add(refreshToken);

        var userResult = await _userManager.UpdateAsync(user);
        if (!userResult.Succeeded)
            throw new BadRequestException("Could not persist refresh token.");

        await Send.OkAsync(new LoginResponse(command.Email, jwtToken, refreshToken.Token));
    }

    public RefreshToken GenerateRefreshToken(string clientIpAddress)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var randomBytes = new byte[64];
            rng.GetBytes(randomBytes);
            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                ExpiresOn = DateTime.UtcNow.AddDays(7),
                CreatedOn = DateTime.UtcNow,
                CreatedByIp = clientIpAddress,
            };
        }
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