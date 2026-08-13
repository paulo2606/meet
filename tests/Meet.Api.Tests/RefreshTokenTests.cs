using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meet.Api.Data;
using Meet.Api.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meet.Api.Tests;

public class RefreshTokenTests : IClassFixture<MeetApiFactory>
{
    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public RefreshTokenTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_ComTokenValido_DeveRotacionarERetornarNovoAccessToken()
    {
        var (originalToken, refresh) = await RegisterAsync(_client);

        var response = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = refresh }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(originalToken.AccessToken, body.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReutilizandoTokenRevogado_DeveRetornarUnauthorized()
    {
        var (_, refresh) = await RegisterAsync(_client);

        var first = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = refresh }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var reused = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = refresh }));

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Refresh_ComTokenInvalido_DeveRetornarUnauthorized()
    {
        var response = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = "token-invalido" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ComTokenExpirado_DeveRetornarUnauthorized()
    {
        var (_, refresh) = await RegisterAsync(_client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetDbContext>();
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refresh)));
        var stored = await db.RefreshTokens.SingleAsync(token => token.TokenHash == hash);
        stored.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var response = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = refresh }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeveRevogarToken_EFazerRefreshRetornarUnauthorized()
    {
        var (_, refresh) = await RegisterAsync(_client);

        var logout = await _client.PostAsync("/api/auth/logout", Json(new { refreshToken = refresh }));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refreshAfter = await _client.PostAsync("/api/auth/refresh", Json(new { refreshToken = refresh }));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
    }

    [Fact]
    public async Task Logout_SemToken_DeveRetornarNoContent()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<(TokenResponse Token, string RefreshToken)> RegisterAsync(HttpClient client)
    {
        var email = $"rt-{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsync("/api/auth/register", Json(new
        {
            name = "Refresh Token Teste",
            email,
            password = "senha-segura-123",
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        var refresh = Uri.UnescapeDataString(response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("meet_refresh=", StringComparison.Ordinal))
            .Split(';')[0].Split('=', 2)[1]);
        return (token!, refresh);
    }

    private static StringContent Json(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }
}
