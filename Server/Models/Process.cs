using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Models;

public class Process : ApplicationRecord
{
  [Key]
  public long Id { get; set; }

  public DateTime Date { get; set; } = DateTime.Now;

  [Required]
  public required long Duration { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required ProcessPipeline Pipeline { get; set; }

  [Required]
  public required EProcessStatut Status { get; set; }
  [Timestamp]
  public byte[]? CreatedAt { get; set; } = [];

  [Timestamp]
  public byte[]? UpdatedAt { get; set; } = [];

  [Required]
  public required long ProjectId { get; set; }
  public Project? Project { get; set; }

  public enum EProcessStatut
  {
    QUEUED,
    RUNNING,
    FINISHED,
    CANCELED,
    CRASHED,
    TIMEOUT
  }

  public record ProcessPipeline
  {
    public required string Step { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public bool? Enable { get; set; }
    public bool? CanBeDisable { get; set; }

    public required object? Configuration { get; set; }
    public required ProcessPipeline[] Children { get; set; }


    public record ProcessPipelineConfiguration
    {
      public object? Default { get; set; }
      public object? Value { get; set; }
      public string? Description { get; set; }
      public List<object>? Range { get; set; }
      public List<object>? Categorical { get; set; }
    }
  }

}
