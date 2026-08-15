using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meet.Api.DTOs.Auth;

namespace Meet.Api.Tests;

public class AuthControllerTests : IClassFixture<MeetApiFactory>
{
    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ComDadosValidos_DeveRetornarOkComAccessToken()
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            name = "Usuario Teste",
            email = $"user-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.Name));
    }

    [Fact]
    public async Task Register_ComEmailJaCadastrado_DeveRetornarConflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.com";
        var payload = new { name = "Usuario A", email, password = "senha-segura-123" };

        var first = await _client.PostAsync("/api/auth/register", Json(payload));
        var second = await _client.PostAsync("/api/auth/register", Json(payload));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("", "email@test.com", "senha-segura-123")]
    [InlineData("Nome", "email-invalido", "senha-segura-123")]
    [InlineData("Nome", "email@test.com", "curta")]
    [InlineData("Nome", "email@test.com", "")]
    public async Task Register_ComDadosInvalidos_DeveRetornarBadRequest(
        string name,
        string email,
        string password)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new { name, email, password }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DeveSetarCookieDeRefreshHttpOnly()
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            name = "Cookie Teste",
            email = $"cookie-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("meet_refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", setCookie.Split(';')[0]);
    }

    [Fact]
    public async Task Login_ComCredenciaisCorretas_DeveRetornarOkComAccessToken()
    {
        var email = $"login-{Guid.NewGuid():N}@test.com";
        var password = "senha-segura-123";

        await _client.PostAsync("/api/auth/register", Json(new { name = "Login Teste", email, password }));

        var response = await _client.PostAsync("/api/auth/login", Json(new { email, password }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(email, body.Email);
    }

    [Theory]
    [InlineData("senha-incorreta-1")]
    [InlineData("outra-senha-errada")]
    public async Task Login_ComCredenciaisIncorretas_DeveRetornarUnauthorized(string wrongPassword)
    {
        var email = $"nautorizado-{Guid.NewGuid():N}@test.com";
        var password = "senha-segura-123";

        await _client.PostAsync("/api/auth/register", Json(new { name = "Login Nao Autorizado", email, password }));

        var response = await _client.PostAsync("/api/auth/login", Json(new { email, password = wrongPassword }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ComEmailInexistente_DeveRetornarUnauthorized()
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email = $"inexistente-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ComPayloadInvalido_DeveRetornarBadRequest()
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email = $"payload-{Guid.NewGuid():N}@test.com",
            password = "",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_TentativasExcedidas_DeveRetornarTooManyRequests()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:LoginPermitLimit", "3"));
        var limitedClient = limitedFactory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await limitedClient.PostAsync("/api/auth/login", Json(new
            {
                email = $"brute-{Guid.NewGuid():N}@test.com",
                password = "senha-errada-123",
            }));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var blocked = await limitedClient.PostAsync("/api/auth/login", Json(new
        {
            email = $"brute-{Guid.NewGuid():N}@test.com",
            password = "senha-errada-123",
        }));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task GuestToken_DeveRetornarTokenDeConvidado()
    {
        var response = await _client.PostAsync("/api/auth/guest-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GuestTokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    private static StringContent Json(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }
}
