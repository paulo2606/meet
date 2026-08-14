using Meet.Api.Data;
using Meet.Api.DTOs.Me;
using Meet.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Meet.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class MeController(
    MeetDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    private const int DefaultMaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        return Ok(new { userId = user.Id, name = user.Name, email = user.Email, photoUrl = user.PhotoUrl });
    }

    [HttpPut("me/photo")]
    public async Task<IActionResult> SetPhoto(SetPhotoRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        user.PhotoUrl = $"/avatars/{request.AvatarId}.svg";
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { user.PhotoUrl });
    }

    [HttpPost("me/photo/upload")]
    public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "nenhum arquivo enviado" });
        }

        var maxBytes = configuration.GetValue<long>("PhotoUpload:MaxSizeBytes", DefaultMaxPhotoBytes);
        if (file.Length > maxBytes)
        {
            return BadRequest(new { message = "arquivo muito grande (maximo 5 MB)" });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "tipo de arquivo invalido" });
        }

        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null,
        };
        if (expectedContentType is null || !file.ContentType.Equals(expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "tipo de arquivo invalido" });
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (!HasValidSignature(extension, bytes))
        {
            return BadRequest(new { message = "arquivo corrompido ou tipo invalido" });
        }

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(webRoot, "uploads", user.Id.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadsRoot, fileName), bytes, cancellationToken);

        user.PhotoUrl = $"/uploads/{user.Id}/{fileName}";
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { user.PhotoUrl });
    }

    private async Task<Entities.User> LoadUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await db.Users.SingleOrDefaultAsync(record => record.Id == userId, cancellationToken);
        return user ?? throw new InvalidOperationException("usuario nao encontrado");
    }

    private static bool HasValidSignature(string extension, byte[] bytes)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8
                && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".webp" => bytes.Length >= 12
                && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => false,
        };
    }
}
