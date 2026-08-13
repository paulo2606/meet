using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Meet.Api.DTOs.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Meet.Api.Tests;

public class AuthorizationTests : IClassFixture<MeetApiFactory>
{
    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Me_SemToken_DeveRetornarUnauthorized()
    {
        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenInvalido_DeveRetornarUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-invalido");

        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenAssinaturaIncorreta_DeveRetornarUnauthorized()
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("chave-diferente-para-assinatura-incorreta-32b"));
        var token = CreateToken(wrongKey, notBefore: DateTime.UtcNow.AddMinutes(-5), expires: DateTime.UtcNow.AddMinutes(15));
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenExpirado_DeveRetornarUnauthorized()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MeetApiFactory.TestJwtKey));
        var token = CreateToken(key, notBefore: DateTime.UtcNow.AddMinutes(-30), expires: DateTime.UtcNow.AddMinutes(-5));
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenValido_DeveRetornarOkComDadosDoUsuario()
    {
        var register = await _client.PostAsync("/api/auth/register", Json(new
        {
            name = "Autorizado",
            email = $"me-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));
        var token = await register.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.Equal(token.UserId, body.UserId);
        Assert.Equal("Autorizado", body.Name);
        Assert.False(string.IsNullOrWhiteSpace(body.Email));
    }

    private static string CreateToken(SymmetricSecurityKey key, DateTime notBefore, DateTime expires)
    {
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "meet-api",
            audience: "meet-web",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static StringContent Json(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }
}

public class MeResponse
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}
