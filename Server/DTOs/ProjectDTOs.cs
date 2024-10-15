using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Server.DTOs;

public record StartProjectInDTO
{
  public required string Title { get; set; }

  public required string Description { get; set; }

  public required string CsvColumnToPredict { get; set; }

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

  public string? RowDepedenciesLinkedString { get; set; } = string.Empty;

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

  public required string Pipeline { get; set; } // Deserialize in the service

  public required IFormFile File { get; set; }

  public required bool SurvivalModel { get; set; }

  public string? SurvivalModelDatasetTargetTime { get; set; }

}
