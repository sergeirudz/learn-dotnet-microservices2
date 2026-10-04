using System.Net;
using Blocks.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Auth.Application;
using Auth.Domain.Users;
using Auth.Persistence.Repositories;


namespace Auth.API.Features.Users.Login;

[AllowAnonymous]
[HttpPost("/tokens")]
[Tags("Auth")]
public class LoginEndpoint(
    PersonRepository _personRepository,
    UserManager<Domain.Users.User> _userManager,
    SignInManager<Domain.Users.User> _signInManager,
    TokenFactory _tokenFactory
)
    : Endpoint<LoginCommand, LoginResponse>
{
    public override async Task HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var person = Guard.NotFound(
            await _personRepository.GetByEmailAsync(command.Email, ct)
        );
        var user = Guard.NotFound(person.User);

        var result = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new BadRequestException($"Invalid credentials for {command.Email}");
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        var jwtToken = _tokenFactory.GenerateJWTToken(user.Id.ToString(), user.Person.FullName, command.Email,
            userRoles,
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