namespace Meet.Api.DTOs.Meetings;

public class MeetingResponse
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string HostName { get; set; }
}
