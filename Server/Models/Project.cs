using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Models;

public class Project : ApplicationRecord
{
  private string _metricsBaselineJson = "";


  [Key]
  public long Id { get; set; }

  [Required]
  public required string Title { get; set; }

  [Required]
  public required string Description { get; set; }

  public string EvaluationMode { get; set; } = "split";

  [Column(TypeName = "jsonb")]
  public string? CreationContextJson { get; set; }

  [Required]
  public required string CsvColumnToPredict { get; set; }

  [Required]
  public required bool RowDepedencies { get; set; }

  [Required]
  public required string[] RowDepedenciesLinked { get; set; } = [];

  [Required]
  public required bool DatasetHasIdColumn { get; set; }

  [Required]
  public required string[] DatasetHasIdColumnList { get; set; } = [];

  [Required]
  public required bool SurvivalModel { get; set; } = false;

  public string? SurvivalModelDatasetTargetTime { get; set; }

  // JSON scalar: preserve the type of a textual, boolean or numeric event value.
  public string? SurvivalModelEventValue { get; set; }

  [NotMapped] // This ensures Entity Framework doesn't treat this as a mapped column
  public object? MetricsBaseline
  {
    get
    {
      return string.IsNullOrWhiteSpace(_metricsBaselineJson)
          ? null
          : JsonSerializer.Deserialize<object>(_metricsBaselineJson);
    }
    set
    {
      // Serialize the object to JSON
      _metricsBaselineJson = JsonSerializer.Serialize(value);
    }
  }

  [Required]
  [Column(TypeName = "jsonb")]
  public string MetricsBaselineJson
  {
    get => _metricsBaselineJson;
    set => _metricsBaselineJson = value;
  }
  [Timestamp]
  public byte[]? CreatedAt { get; set; } = [];

  [Timestamp]
  public byte[]? UpdatedAt { get; set; } = [];

  [Required]
  public required long UserId { get; set; }
  public User? User { get; set; }

  public ICollection<ProjectFile> ProjectFile { get; } = [];

  public ICollection<Process> Processes { get; } = [];

}
