using System.ComponentModel.DataAnnotations;

namespace Meet.Api.DTOs.Me;

public class SetPhotoRequest
{
    [Range(1, 12)]
    public int AvatarId { get; set; }
}
