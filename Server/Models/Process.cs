using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Models;

public class Process : ApplicationRecord
{
  private string _pipelineJson = "";
  private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

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
          : JsonSerializer.Deserialize<ProcessPipeline>(_pipelineJson, jsonOptions);
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

  [Column(TypeName = "jsonb")]
  public string? StatusSnapshotJson { get; set; }

  [NotMapped]
  public object? StatusSnapshot => string.IsNullOrWhiteSpace(StatusSnapshotJson)
    ? null
    : JsonSerializer.Deserialize<object>(StatusSnapshotJson);

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
    TIMEOUT,
    CACHED
  }

  public record ProcessPipeline
  {
    [JsonPropertyName("schema_version")]
    public int? SchemaVersion { get; set; }

    // Keep the complete canonical study, including parameter domains and resolved
    // families. An old pipeline without a version stays identifiable as incompatible.
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Fields { get; set; } = [];
  }

}
