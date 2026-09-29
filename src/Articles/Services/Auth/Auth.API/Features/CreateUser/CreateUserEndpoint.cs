using Articles.Abstractions.Enums;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;

namespace Auth.API.Features.CreateUser;

[Authorize(Roles = Role.USERADMIN)]
[HttpPost("users")]
public class CreateUserEndpoint : Endpoint<CreateUserCommand, CreateUserResponse>
{
    public override async Task HandleAsync(CreateUserCommand req, CancellationToken ct)
    {
        return base.HandleAsync(req, ct);
    }
}