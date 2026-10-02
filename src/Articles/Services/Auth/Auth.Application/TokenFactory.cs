using Blocks.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Domain.Users;
using Blocks.Core.Security;

namespace Auth.Application;

public class TokenFactory
{
    private readonly JwtOptions _jwtOptions;

    public TokenFactory(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
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

        var secretKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_jwtOptions.Secret));
        var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            notBefore: DateTime.UtcNow,
            claims: claims,
            expires: _jwtOptions.Expiration);

        // Take everything in jwtToken, and encode it into a string that can be sent to the client
        var encodedJwtToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);
        return encodedJwtToken;
    }
}