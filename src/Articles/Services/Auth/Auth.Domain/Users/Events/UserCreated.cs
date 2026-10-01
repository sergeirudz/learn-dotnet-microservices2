namespace Auth.Domain.Users.Events;

public record UserCreated(Users User, string RessetPasswordToken);