using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Blocks.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Articles.Security;
using Auth.Application;
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
    TokenFactory _tokenFactory
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

        var jwtToken = _tokenFactory.GenerateJWTToken(user.Id.ToString(), user.FullName, command.Email, userRoles,
            Array.Empty<Claim>());
        var refreshToken =
            _tokenFactory.GenerateRefreshToken(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        user.AssignRefreshToken(refreshToken);

        var userResult = await _userManager.UpdateAsync(user);
        if (!userResult.Succeeded)
            throw new BadRequestException("Could not persist refresh token.");

        await Send.OkAsync(new LoginResponse(command.Email, jwtToken, refreshToken.Token));
    }
}