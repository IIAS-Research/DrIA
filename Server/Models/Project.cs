using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models;

public class Project : ApplicationRecord
{
  [Key]
  public long Id { get; set; }

  [Required]
  public required string Title { get; set; }

  [Required]
  public required string Description { get; set; }

  [Timestamp]
  public required byte[] CreatedAt { get; set; }

  [Timestamp]
  public required byte[] UpdatedAt { get; set; }


  [Required]
  public required long UserId { get; set; }
  public User? User { get; set; }

  public ProjectFile? ProjectFile { get; set; }

  public Collection<Process>? Processes { get; set; }

}