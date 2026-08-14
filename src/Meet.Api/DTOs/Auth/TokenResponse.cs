namespace Meet.Api.DTOs.Auth;

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? PhotoUrl { get; set; }
}

public record AuthResult(TokenResponse Token, string RefreshToken);
