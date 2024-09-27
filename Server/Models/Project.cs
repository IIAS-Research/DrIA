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

  [Timestamp]
  public byte[] CreatedAt { get; set; } = [];

  [Timestamp]
  public byte[] UpdatedAt { get; set; } = [];


  [Required]
  public required long UserId { get; set; }
  public User? User { get; set; }

  public ProjectFile? ProjectFile { get; set; }

  public Collection<Process>? Processes { get; set; }

}