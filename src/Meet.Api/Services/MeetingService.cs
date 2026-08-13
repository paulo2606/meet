using Meet.Api.Data;
using Meet.Api.DTOs.Meetings;
using Meet.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Meet.Api.Services;

public interface IMeetingService
{
    Task<MeetingResponse> CreateAsync(Guid userId, CancellationToken cancellationToken);
    Task<MeetingResponse?> GetAsync(Guid meetingId, CancellationToken cancellationToken);
}

public class MeetingService(MeetDbContext db) : IMeetingService
{
    public async Task<MeetingResponse> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var host = await db.Users
            .FirstAsync(user => user.Id == userId, cancellationToken);

        Meeting meeting;
        do
        {
            meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                Code = MeetingCodeGenerator.Generate(),
                CreatedById = userId,
                CreatedBy = host,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
        } while (await db.Meetings.AnyAsync(existing => existing.Code == meeting.Code, cancellationToken));

        db.Meetings.Add(meeting);
        await db.SaveChangesAsync(cancellationToken);

        return new MeetingResponse
        {
            Id = meeting.Id,
            Code = meeting.Code,
            CreatedAtUtc = meeting.CreatedAtUtc,
            HostName = host.Name,
        };
    }

    public async Task<MeetingResponse?> GetAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        return await db.Meetings
            .AsNoTracking()
            .Where(meeting => meeting.Id == meetingId)
            .Select(meeting => new MeetingResponse
            {
                Id = meeting.Id,
                Code = meeting.Code,
                CreatedAtUtc = meeting.CreatedAtUtc,
                HostName = meeting.CreatedBy.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
