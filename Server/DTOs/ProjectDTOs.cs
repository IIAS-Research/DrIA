using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Server.DTOs;

public record StartProjectInDTO
{
  [Required]
  public required string Title { get; set; }

  [Required]
  public required string Description { get; set; }

  [Required]
  public required string CsvColumnToPredict { get; set; }

  [Required]
  public required bool RowDepedencies { get; set; }

  public string[] RowDepedenciesLinked
  {
    get
    {
      if (RowDepedenciesLinkedString is null)
      {
        return [];
      }
      return RowDepedenciesLinkedString.Split(",");
    }
  }

  public string? RowDepedenciesLinkedString { get; set;} = string.Empty;

  [Required]
  public required bool DatasetHasIdColumn { get; set; }

  public string[] DatasetHasIdColumnList
  {
    get
    {
      if (DatasetHasIdColumnListString is null)
      {
        return [];
      }
      return DatasetHasIdColumnListString.Split(",");
    }
  }

  public string? DatasetHasIdColumnListString { get; set; } = string.Empty;

  [Required]
  public required string Pipeline { get; set; } // Deserialize in the service

  [Required]
  public required IFormFile File { get; set; }
}
