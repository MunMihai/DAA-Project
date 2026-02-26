namespace Quiz.AuthService.DTOs;

public sealed record SignupRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string Email
);