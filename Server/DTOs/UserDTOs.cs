using System.ComponentModel.DataAnnotations;

namespace Server.DTOs;

public record RegisterUserDTO
{
    [Required]
    public required string FirstName { get; init; }

    [Required]
    public required string LastName { get; init; }

    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    public required bool IsAdmin { get; init; }
}
