using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Server.Models;

public class Project : ApplicationRecord
{
  [Key]
  public long Id { get; set; }

  [Required]
  public required string Title { get; set; }

  [Required]
  public required string Description { get; set; }

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