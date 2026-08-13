using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Meet.Api.Hubs;

public record ParticipantInfo(string ParticipantId, string Name);

public class MeetingHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ParticipantConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionParticipants = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionMeetings = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> MeetingParticipants = new();

    public async Task Join(string meetingId, string participantId, string name)
    {
        ParticipantConnections[participantId] = Context.ConnectionId;
        ConnectionParticipants[Context.ConnectionId] = participantId;
        ConnectionMeetings[Context.ConnectionId] = meetingId;
        await Groups.AddToGroupAsync(Context.ConnectionId, meetingId);

        var participants = MeetingParticipants.GetOrAdd(meetingId, _ => new ConcurrentDictionary<string, string>());
        participants.TryAdd(participantId, name);

        var peers = participants
            .Where(pair => pair.Key != participantId)
            .Select(pair => new ParticipantInfo(pair.Key, pair.Value))
            .ToList();
        await Clients.Caller.SendAsync("Peers", peers);
        await Clients.OthersInGroup(meetingId).SendAsync("PeerJoined", participantId, name);
    }

    public Task SendMessage(string meetingId, string text)
    {
        var participantId = ConnectionParticipants.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
        var name = MeetingParticipants
            .GetValueOrDefault(meetingId)?
            .GetValueOrDefault(participantId) ?? participantId;
        return Clients.Group(meetingId).SendAsync("Message", participantId, name, text);
    }

    public Task Offer(string meetingId, string targetParticipantId, string sdp)
    {
        return RelayAsync(targetParticipantId, "Offer", meetingId, Context.ConnectionId, sdp);
    }

    public Task Answer(string meetingId, string targetParticipantId, string sdp)
    {
        return RelayAsync(targetParticipantId, "Answer", meetingId, Context.ConnectionId, sdp);
    }

    public Task IceCandidate(string meetingId, string targetParticipantId, string candidate)
    {
        return RelayAsync(targetParticipantId, "IceCandidate", meetingId, Context.ConnectionId, candidate);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectionParticipants.TryRemove(Context.ConnectionId, out var participantId);
        ConnectionMeetings.TryRemove(Context.ConnectionId, out var meetingId);
        if (participantId is not null)
        {
            ParticipantConnections.TryRemove(participantId, out _);
        }

        if (participantId is not null && meetingId is not null)
        {
            if (MeetingParticipants.TryGetValue(meetingId, out var participants))
            {
                participants.TryRemove(participantId, out _);
                if (participants.IsEmpty)
                {
                    MeetingParticipants.TryRemove(meetingId, out _);
                }
            }

            await Clients.Group(meetingId).SendAsync("PeerLeft", participantId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RelayAsync(string targetParticipantId, string eventName, string meetingId, string fromConnectionId, string payload)
    {
        if (ParticipantConnections.TryGetValue(targetParticipantId, out var targetConnectionId))
        {
            var fromParticipantId = ConnectionParticipants.GetValueOrDefault(fromConnectionId) ?? fromConnectionId;
            await Clients.Client(targetConnectionId).SendAsync(eventName, meetingId, fromParticipantId, payload);
        }
    }
}
