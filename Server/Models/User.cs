using System.ComponentModel.DataAnnotations;

namespace Server.Models;

public class User : ApplicationRecord
{
  [Key]
  public long Id { get; set; }

  [Required]
  public required string Firstname { get; set; }

  [Required]
  public required string Lastname { get; set; }

  [Required]
  [MaxLength(255)]
  public required string Email { get; set; }

  [Required]
  [MaxLength(60)]
  public required string HashedPassword { get; set; }

  [Required]
  public required bool IsAdmin { get; set; } = false;

  [Timestamp]
  public required byte[] CreatedAt { get; set; }

}