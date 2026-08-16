using Meet.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Meet.Api.Controllers;

[ApiController]
[Route("")]
public class UploadsController(MeetDbContext db) : ControllerBase
{
    [HttpGet("uploads/{photoId:guid}")]
    public async Task<IActionResult> GetUploadedPhoto(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await db.UserPhotos.AsNoTracking()
            .SingleOrDefaultAsync(record => record.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }
        return File(photo.Bytes, photo.ContentType);
    }
}
