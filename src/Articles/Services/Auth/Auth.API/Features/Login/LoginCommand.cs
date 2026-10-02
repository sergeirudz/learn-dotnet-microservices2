namespace Auth.Api.Features.Login;

public record LoginCommand(string Email, string Password);

public record LoginResponse(string Email, string JWTToken, string RefreshToken);