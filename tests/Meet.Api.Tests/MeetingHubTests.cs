using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
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

        var anaPeers = new TaskCompletionSource<List<string>>();
        ana.On<List<string>>("Peers", peers => anaPeers.TrySetResult(peers));
        await ana.InvokeAsync("Join", "m1", "pa", "Ana");
        var anaInitial = await anaPeers.Task;
        Assert.Empty(anaInitial);

        var brunoPeers = new TaskCompletionSource<List<string>>();
        bruno.On<List<string>>("Peers", peers => brunoPeers.TrySetResult(peers));
        var anaNotified = new TaskCompletionSource<string>();
        ana.On<string, string>("PeerJoined", (participantId, name) => anaNotified.TrySetResult($"{participantId}|{name}"));

        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno");

        var brunoPeersValue = await brunoPeers.Task;
        Assert.Contains("pa", brunoPeersValue);
        Assert.Equal("pb|Bruno", await anaNotified.Task);
    }

    [Fact]
    public async Task Offer_DeveSerRetransmitidoParaODestino()
    {
        var ana = await ConnectAsync("pa", "Ana");
        var bruno = await ConnectAsync("pb", "Bruno");
        await ana.InvokeAsync("Join", "m1", "pa", "Ana");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno");

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
        await ana.InvokeAsync("Join", "m1", "pa", "Ana");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno");

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
        await ana.InvokeAsync("Join", "m1", "pa", "Ana");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno");

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
        await ana.InvokeAsync("Join", "m1", "pa", "Ana");
        await bruno.InvokeAsync("Join", "m1", "pb", "Bruno");

        var left = new TaskCompletionSource<string>();
        bruno.On<string>("PeerLeft", participantId => left.TrySetResult(participantId));

        await ana.DisposeAsync();

        Assert.Equal("pa", await left.Task);
    }

    private async Task<HubConnection> ConnectAsync(string participantId, string name)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/meeting", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        connection.On<List<string>>("Peers", _ => Task.CompletedTask);
        connection.On<string, string>("PeerJoined", (_, _) => Task.CompletedTask);
        connection.On<string>("PeerLeft", _ => Task.CompletedTask);
        connection.On<string, string, string>("Offer", (_, _, _) => Task.CompletedTask);
        connection.On<string, string, string>("Answer", (_, _, _) => Task.CompletedTask);
        connection.On<string, string, string>("IceCandidate", (_, _, _) => Task.CompletedTask);

        await connection.StartAsync();
        _connections.Add(connection);
        return connection;
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
