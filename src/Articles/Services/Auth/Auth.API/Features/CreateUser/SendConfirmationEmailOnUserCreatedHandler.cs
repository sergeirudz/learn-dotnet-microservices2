using Auth.Domain.Users.Events;

namespace Auth.API.Features.Users.CreateAccount;

public class SendConfirmationEmailOnUserCreatedHandler(
    IEmailService emailService
)
{
}