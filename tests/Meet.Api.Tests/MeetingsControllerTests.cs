using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meet.Api.DTOs.Auth;
using Meet.Api.DTOs.Meetings;

namespace Meet.Api.Tests;

public class MeetingsControllerTests : IClassFixture<MeetApiFactory>
{
    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingsControllerTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateMeeting_ComUsuarioAutenticado_DeveRetornarOkComCodigo()
    {
        await RegisterAsync();

        var response = await _client.PostAsync("/api/meetings", EmptyJson());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.False(string.IsNullOrWhiteSpace(body.Code));
        Assert.Equal("Criador", body.HostName);
    }

    [Fact]
    public async Task CreateMeeting_SemAutenticacao_DeveRetornarUnauthorized()
    {
        var response = await _client.PostAsync("/api/meetings", EmptyJson());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMeeting_DeveGerarCodigosDiferentes()
    {
        await RegisterAsync();

        var first = await CreateMeetingAsync();
        var second = await CreateMeetingAsync();

        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    public async Task GetMeeting_ComUsuarioAutenticado_DeveRetornarOkComDados()
    {
        await RegisterAsync();
        var created = await CreateMeetingAsync();

        var response = await _client.GetAsync($"/api/meetings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(created.Code, body.Code);
        Assert.Equal("Criador", body.HostName);
    }

    [Fact]
    public async Task GetMeeting_Inexistente_DeveRetornarNotFound()
    {
        await RegisterAsync();

        var response = await _client.GetAsync($"/api/meetings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMeeting_SemAutenticacao_DevePermitirConsultaPublica()
    {
        await RegisterAsync();
        var created = await CreateMeetingAsync();

        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync($"/api/meetings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Code, body.Code);
        Assert.Equal("Criador", body.HostName);
    }

    [Fact]
    public async Task GetMeeting_Inexistente_SemAutenticacao_DeveRetornarNotFound()
    {
        var response = await _client.GetAsync($"/api/meetings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<MeetingResponse> CreateMeetingAsync()
    {
        var response = await _client.PostAsync("/api/meetings", EmptyJson());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;
    }

    private async Task RegisterAsync()
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            name = "Criador",
            email = $"meeting-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private static StringContent EmptyJson() =>
        new("{}", Encoding.UTF8, "application/json");

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
