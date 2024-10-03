using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Server.Models;

public class User : IdentityUser<long>
{
  [Required]
  public required string FirstName { get; set; }

  [Required]
  public required string LastName { get; set; }

  [Required]
  public required bool IsAdmin { get; set; } = false;

  [Timestamp]
  public byte[]? CreatedAt { get; set; }

  public ICollection<Project> Projects { get; set; } = [];
}
