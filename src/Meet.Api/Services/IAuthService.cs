using Meet.Api.DTOs.Auth;

namespace Meet.Api.Services;

public class EmailAlreadyRegisteredException : Exception;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
