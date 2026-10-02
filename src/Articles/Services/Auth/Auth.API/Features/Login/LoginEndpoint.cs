using System.Net;
using Blocks.AspNetCore;
using Blocks.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Auth.API.Features.Login;

[AllowAnonymous]
[HttpPost("/tokens")]
[Tags("Auth")]
public class LoginEndpoint(
    UserManager<Users> _userManager,
    SignInManager<Users> _signInManager)
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
    }
}