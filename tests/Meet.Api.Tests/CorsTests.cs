using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;

namespace Meet.Api.Tests;

public class CorsTests : IClassFixture<MeetApiFactory>
{
    private const string AllowedOrigin = "http://localhost:3000";

    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public CorsTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_DeOrigemPermitida_DevePermitirComCredenciais()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await _client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task GetComOrigin_DeveIncluirAllowOriginComCredenciais()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _client.SendAsync(request);

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Preflight_DeOrigemNaoPermitida_NaoDeveIncluirAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "http://localhost:9999");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_ComOrigemConfigurada_DevePermitir()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:AllowedOrigins", "https://meet-front.vercel.app"));
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://meet-front.vercel.app");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal("https://meet-front.vercel.app", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
