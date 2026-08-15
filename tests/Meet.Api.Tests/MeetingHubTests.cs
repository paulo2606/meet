using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Meet.Api.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;
using Xunit.Abstractions;

namespace Meet.Api.Tests;

public class MeetingHubTests : IClassFixture<MeetApiFactory>, IDisposable
{
    private readonly MeetApiFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly List<HubConnection> _connections = [];

    public MeetingHubTests(MeetApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Join_NotificaParticipantesExistentesENovos()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");

        var anaPeers = new TaskCompletionSource<List<ParticipantInfo>>();
        ana.On<List<ParticipantInfo>>("Peers", peers => anaPeers.TrySetResult(peers));
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        var anaInitial = await anaPeers.Task;
        Assert.Empty(anaInitial);

        var brunoPeers = new TaskCompletionSource<List<ParticipantInfo>>();
        bruno.On<List<ParticipantInfo>>("Peers", peers => brunoPeers.TrySetResult(peers));
        var anaNotified = new TaskCompletionSource<string>();
        ana.On<string, string, string>("PeerJoined", (participantId, name, photoUrl) => anaNotified.TrySetResult($"{participantId}|{name}|{photoUrl}"));

        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var brunoPeersValue = await brunoPeers.Task;
        Assert.Contains(brunoPeersValue, peer => peer.ParticipantId == "pa" && peer.Name == "Ana" && peer.PhotoUrl == "/avatars/1.svg");
        Assert.Equal("pb|Bruno|/avatars/2.svg", await anaNotified.Task);
    }

    [Fact]
    public async Task CameraState_DeveSerNotificadoParaTodosDaReuniao()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", null);
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", null);

        var anaCamera = new TaskCompletionSource<string>();
        ana.On<string, bool>("CameraState", (participantId, on) => anaCamera.TrySetResult($"{participantId}|{on}"));
        var brunoCamera = new TaskCompletionSource<string>();
        bruno.On<string, bool>("CameraState", (participantId, on) => brunoCamera.TrySetResult($"{participantId}|{on}"));

        await ana.InvokeAsync("CameraState", "m1", false);

        Assert.Equal("pa|False", await anaCamera.Task);
        Assert.Equal("pa|False", await brunoCamera.Task);
    }

    [Fact]
    public async Task Offer_DeveSerRetransmitidoParaODestino()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var offerReceived = new TaskCompletionSource<string>();
        bruno.On<string, string, string>("Offer", (_, fromParticipantId, sdp) => offerReceived.TrySetResult($"{fromParticipantId}|{sdp}"));

        await ana.InvokeAsync("Offer", "m1", "pb", "sdp-da-ana");

        Assert.Equal("pa|sdp-da-ana", await offerReceived.Task);
    }

    [Fact]
    public async Task Answer_DeveSerRetransmitidoParaODestino()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var answerReceived = new TaskCompletionSource<string>();
        ana.On<string, string, string>("Answer", (_, fromParticipantId, sdp) => answerReceived.TrySetResult($"{fromParticipantId}|{sdp}"));

        await bruno.InvokeAsync("Answer", "m1", "pa", "sdp-do-bruno");

        Assert.Equal("pb|sdp-do-bruno", await answerReceived.Task);
    }

    [Fact]
    public async Task IceCandidate_DeveSerRetransmitidoParaODestino()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var candidateReceived = new TaskCompletionSource<string>();
        bruno.On<string, string, string>("IceCandidate", (meetingId, fromParticipantId, candidate) => candidateReceived.TrySetResult($"{fromParticipantId}|{candidate}"));

        await ana.InvokeAsync("IceCandidate", "m1", "pb", "candidate-1");

        Assert.Equal("pa|candidate-1", await candidateReceived.Task);
    }

    [Fact]
    public async Task Desconectar_NotificaParticipantesDoGrupo()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var left = new TaskCompletionSource<string>();
        bruno.On<string>("PeerLeft", participantId => left.TrySetResult(participantId));

        await ana.DisposeAsync();

        Assert.Equal("pa", await left.Task);
    }

    [Fact]
    public async Task Mensagem_DeveSerEntregueParaTodosDaReuniao()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var anaMessage = new TaskCompletionSource<string>();
        ana.On<string, string, string>("Message", (participantId, name, text) => anaMessage.TrySetResult($"{participantId}|{name}|{text}"));
        var brunoMessage = new TaskCompletionSource<string>();
        bruno.On<string, string, string>("Message", (participantId, name, text) => brunoMessage.TrySetResult($"{participantId}|{name}|{text}"));

        await ana.InvokeAsync("SendMessage", "m1", "ola pessoal");

        Assert.Equal("pa|Ana|ola pessoal", await anaMessage.Task);
        Assert.Equal("pa|Ana|ola pessoal", await brunoMessage.Task);
    }

    [Fact]
    public async Task ScreenShare_DeveSerNotificadoParaTodosDaReuniao()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana", "/avatars/1.svg");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno", "/avatars/2.svg");

        var anaScreen = new TaskCompletionSource<string>();
        ana.On<string, bool>("ScreenShare", (participantId, sharing) => anaScreen.TrySetResult($"{participantId}|{sharing}"));
        var brunoScreen = new TaskCompletionSource<string>();
        bruno.On<string, bool>("ScreenShare", (participantId, sharing) => brunoScreen.TrySetResult($"{participantId}|{sharing}"));

        await ana.InvokeAsync("ScreenShare", "m1", true);

        Assert.Equal("pa|True", await anaScreen.Task);
        Assert.Equal("pa|True", await brunoScreen.Task);
    }

    [Fact]
    public async Task SemToken_DeveFalharAoConectarNoHub()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/meeting", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ComTokenDeConvidado_DeveConectarNoHub()
    {
        var token = await RequestGuestTokenAsync();
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/meeting", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        _connections.Add(connection);

        await connection.StartAsync();
    }

    private async Task<string> RequestGuestTokenAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/guest-token", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Meet.Api.DTOs.Auth.GuestTokenResponse>();
        Assert.NotNull(body);
        return body.AccessToken;
    }

    private async Task<HubConnection> ConnectAsync(string participantId, string name)
    {
        var token = CreateValidToken();
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/meeting", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        connection.On<List<ParticipantInfo>>("Peers", _ => Task.CompletedTask);
        connection.On<string, string, string>("PeerJoined", (_, _, _) => Task.CompletedTask);
        connection.On<string>("PeerLeft", _ => Task.CompletedTask);
        connection.On<string, string, string>("Offer", (_, _, _) => Task.CompletedTask);
        connection.On<string, string, string>("Answer", (_, _, _) => Task.CompletedTask);
        connection.On<string, string, string>("IceCandidate", (_, _, _) => Task.CompletedTask);
        connection.On<string, string, string>("Message", (_, _, _) => Task.CompletedTask);
        connection.On<string, bool>("ScreenShare", (_, _) => Task.CompletedTask);
        connection.On<string, bool>("CameraState", (_, _) => Task.CompletedTask);

        await connection.StartAsync();
        _connections.Add(connection);
        return connection;
    }

    private static string CreateValidToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MeetApiFactory.TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "meet-api",
            audience: "meet-web",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
