using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Models;

public class Process : ApplicationRecord
{
  private string _pipelineJson = "";

  [Key]
  public long Id { get; set; }

  public DateTime Date { get; set; } = DateTime.Now;

  [Required]
  public required long Duration { get; set; }

  [NotMapped] // This ensures Entity Framework doesn't treat this as a mapped column
  public ProcessPipeline? Pipeline
  {
    get
    {
      return string.IsNullOrWhiteSpace(_pipelineJson)
          ? null
          : JsonSerializer.Deserialize<ProcessPipeline>(_pipelineJson);
    }
    set
    {
      // Serialize the ProcessPipeline object to JSON
      _pipelineJson = JsonSerializer.Serialize(value);
    }
  }

  [Required]
  [Column(TypeName = "jsonb")]
  public string PipelineJson
  {
    get => _pipelineJson;
    set => _pipelineJson = value;
  }

  [Required]
  public required EProcessStatut Status { get; set; }

  [Required]
  public required string IaRemoteId { get; set; }
  [Required]
  public required int MaxDuration { get; set; }

  [Timestamp]
  public byte[]? CreatedAt { get; set; } = [];

  [Timestamp]
  public byte[]? UpdatedAt { get; set; } = [];

  [Required]
  public required long ProjectId { get; set; }
  public Project? Project { get; set; }

  public ICollection<Model> Models { get; } = [];

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
    public string? Step { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? Enable { get; set; }
    public bool? CanBeDisable { get; set; }
    public Dictionary<string, ProcessPipelineConfiguration>? Configuration { get; set; }
    public ProcessPipeline[]? Children { get; set; }

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
