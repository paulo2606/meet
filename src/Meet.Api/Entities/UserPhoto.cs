namespace Meet.Api.Entities;

public class UserPhoto
{
    public Guid Id { get; set; }
    public required byte[] Bytes { get; set; }
    public required string ContentType { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
