using Meet.Api.Extensions;
using Meet.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meet.Api.Controllers;

[ApiController]
[Route("api/meetings")]
public class MeetingsController(IMeetingService meetingService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var meeting = await meetingService.CreateAsync(User.GetUserId(), cancellationToken);
        return Ok(meeting);
    }

    [HttpGet("{meetingId:guid}")]
    public async Task<IActionResult> Get(Guid meetingId, CancellationToken cancellationToken)
    {
        var meeting = await meetingService.GetAsync(meetingId, cancellationToken);
        if (meeting is null)
        {
            return NotFound();
        }

        return Ok(meeting);
    }
}
