using System.ComponentModel.DataAnnotations;

namespace Server.Models;

public class User : ApplicationRecord
{
  [Key]
  public long Id { get; set; }

  [Required]
  public required string FirstName { get; set; }

  [Required]
  public required string LastName { get; set; }

  [Required]
  [EmailAddress]
  public required string Email { get; set; }

  [Required]
  public required string HashedPassword { get; set; }

  [Required]
  public required bool IsAdmin { get; set; } = false;

  [Timestamp]
  public byte[]? CreatedAt { get; set; }

}