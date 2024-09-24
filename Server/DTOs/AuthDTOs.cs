using System.ComponentModel.DataAnnotations;

namespace Server.DTOs;

public record LoginResponse
{
    public string? AccessToken { get; set; }
    [Required]
    public required bool IsLoggedIn { get; set; }
};

public record PostLoginInDTO
{
    [Required]
    public required string Email { get; init; }
    [Required]
    public required string Password { get; init; }
}

public record JwtPayload
{
    [Required]
    public required string Email { get; init; }
    [Required]
    public required long Sub { get; init; } // user id
    [Required]
    public required string FirstName { get; init; }
    [Required]
    public required string LastName { get; init; }
    [Required]
    public required bool IsAdmin { get; init; }
}