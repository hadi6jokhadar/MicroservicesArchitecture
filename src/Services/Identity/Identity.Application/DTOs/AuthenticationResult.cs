namespace Identity.Application.DTOs;

public record AuthenticationResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Role
);