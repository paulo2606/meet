using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meet.Api.DTOs.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Meet.Api.Tests;

public class MePhotoTests : IClassFixture<MeetApiFactory>
{
    private static readonly byte[] TinyPng =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly MeetApiFactory _factory;
    private readonly HttpClient _client;

    public MePhotoTests(MeetApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PutPhoto_ComAvatarValido_DeveAtualizarPhotoUrl()
    {
        var token = await RegisterAsync("Foto Avatar");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.PutAsync("/api/me/photo", Json(new { avatarId = 3 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>();
        Assert.NotNull(body);
        Assert.Equal("/avatars/3.svg", body.PhotoUrl);

        var me = await _client.GetAsync("/api/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal("/avatars/3.svg", meBody.PhotoUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task PutPhoto_ComAvatarInvalido_DeveRetornarBadRequest(int avatarId)
    {
        var token = await RegisterAsync("Foto Invalida");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.PutAsync("/api/me/photo", Json(new { avatarId }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutPhoto_SemToken_DeveRetornarUnauthorized()
    {
        var response = await _client.PutAsync("/api/me/photo", Json(new { avatarId = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ComImagemPngValida_DeveRetornarOkComPhotoUrl()
    {
        var token = await RegisterAsync("Upload Foto");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.PostAsync("/api/me/photo/upload", Multipart("foto.png", "image/png", TinyPng));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.PhotoUrl);
        Assert.StartsWith("/uploads/", body.PhotoUrl, StringComparison.Ordinal);
        Assert.EndsWith(".png", body.PhotoUrl, StringComparison.Ordinal);

        DeleteUploadedFile(body.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhoto_ComTipoInvalido_DeveRetornarBadRequest()
    {
        var token = await RegisterAsync("Upload Invalido");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var bytes = Encoding.UTF8.GetBytes("isto nao e uma imagem");
        var response = await _client.PostAsync("/api/me/photo/upload", Multipart("nota.txt", "text/plain", bytes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ComTamanhoExcedido_DeveRetornarBadRequest()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("PhotoUpload:MaxSizeBytes", "1024"));
        var limitedClient = limitedFactory.CreateClient();
        var token = await RegisterAsync(limitedClient, "Upload Grande");
        limitedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var bytes = new byte[2048];
        Array.Fill(bytes, (byte)0x89);
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        var response = await limitedClient.PostAsync("/api/me/photo/upload", Multipart("grande.png", "image/png", bytes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_SemToken_DeveRetornarUnauthorized()
    {
        var response = await _client.PostAsync("/api/me/photo/upload", Multipart("foto.png", "image/png", TinyPng));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<TokenResponse> RegisterAsync(string name) => await RegisterAsync(_client, name);

    private static async Task<TokenResponse> RegisterAsync(HttpClient client, string name)
    {
        var response = await client.PostAsync("/api/auth/register", Json(new
        {
            name,
            email = $"foto-{Guid.NewGuid():N}@test.com",
            password = "senha-segura-123",
        }));
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        return token;
    }

    private void DeleteUploadedFile(string photoUrl)
    {
        var environment = _factory.Services.GetRequiredService<IHostEnvironment>();
        var relative = photoUrl.TrimStart('/');
        var path = Path.Combine(environment.ContentRootPath, "wwwroot", relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static MultipartFormDataContent Multipart(string fileName, string contentType, byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private static StringContent Json(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }
}

public class PhotoResponse
{
    public string? PhotoUrl { get; set; }
}
