using Articles.Abstractions.Enums;
using Articles.Security;
using Auth.Domain.Persons;
using Auth.Domain.Users;
using Auth.Domain.Users.Events;
using Auth.Persistence;
using Auth.Persistence.Repositories;
using Blocks.Exceptions;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Auth.API.Features.Users.CreateUser;

[Authorize(Roles = Role.USERADMIN)]
[HttpPost("users")]
public class CreateUserEndpoint(
    UserManager<User> _userManager,
    PersonRepository _personRepository,
    AuthDbContext _dbContext)
    : Endpoint<CreateUserCommand, CreateUserResponse>
{
    public override async Task HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        var person = await _personRepository.GetByEmailAsync(command.Email, ct);
        if (person?.User != null) // check if email is already used by an existing User
            throw new BadRequestException($"User with email {command.Email} already exists");
        //start transaction to ensure atomic creation 
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        if (person is null) // create new Person if not exists
            person = await CreatePersonAsync(command, ct);

        var user = Domain.Users.User.Create(command);

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errorMessages = string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new BadRequestException($"Unable to create user: {errorMessages}");
        }

        // link User to Person and Save
        person.AssignUser(user);
        await _personRepository.SaveChangesAsync(ct);

        var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        await PublishAsync(new UserCreated(user, resetPasswordToken));

        await transaction.CommitAsync(ct);

        await Send.OkAsync(new CreateUserResponse(command.Email, user.Id, resetPasswordToken));
    }


    private async Task<Person> CreatePersonAsync(CreateUserCommand command, CancellationToken ct)
    {
        var person = Person.Create(command);

        await _personRepository.AddAsync(person);
        await _personRepository.SaveChangesAsync(ct);
        return person;
    }
}