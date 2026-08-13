using System.ComponentModel.DataAnnotations;

namespace Meet.Api.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}
