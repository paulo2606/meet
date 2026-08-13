namespace Meet.Api.Entities;

public class Meeting
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public Guid CreatedById { get; set; }
    public required User CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
