using Articles.Abstractions.Enums;
using Auth.Domain.Users;
using Auth.Domain.Users.Events;
using Blocks.Exceptions;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Auth.API.Features.CreateUser;

[Authorize(Roles = Role.USERADMIN)]
[HttpPost("users")]
public class CreateUserEndpoint(UserManager<Users> _userManager) : Endpoint<CreateUserCommand, CreateUserResponse>
{
    public override async Task HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        var user = async _userManager.FindByEmailAsync(command.Email);
        if (user != null) // check if email is already used by an existing User
            throw new BadRequestException($"User with email {command.Email} already exists");

        user = Auth.Domain.Users.Users.Create(command);


        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errorMessages = string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new BadRequestException($"Unable to create user: {errorMessages}");
        }

        var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);


        await PublishAsync(new UserCreated(user, resetPasswordToken));

        await Send.OkAsync(new CreateUserResponse(command.Email, user.Id, resetPasswordToken));
    }
}