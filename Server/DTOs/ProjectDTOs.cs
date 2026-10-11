using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Newtonsoft.Json;

namespace Server.DTOs;

public record StartProjectInDTO : IValidatableObject
{
  public required string Title { get; set; }

  public required string Description { get; set; }

  public required string CsvColumnToPredict { get; set; }

  public required bool RowDepedencies { get; set; }

  [ValidateNever]
  public string[] RowDepedenciesLinked
  {
    get
    {
      if (RowDepedenciesLinkedString is null)
      {
        return [];
      }
      return ParseColumns(RowDepedenciesLinkedString);
    }
  }

  public string? RowDepedenciesLinkedString { get; set; } = string.Empty;

  public required bool DatasetHasIdColumn { get; set; }

  [ValidateNever]
  public string[] DatasetHasIdColumnList
  {
    get
    {
      if (DatasetHasIdColumnListString is null)
      {
        return [];
      }
      return ParseColumns(DatasetHasIdColumnListString);
    }
  }

  public string? DatasetHasIdColumnListString { get; set; } = string.Empty;

  public required string Pipeline { get; set; } // Deserialize in the service

  public required IFormFile File { get; set; }

  public required bool SurvivalModel { get; set; }

  public string? SurvivalModelDatasetTargetTime { get; set; }

  public string? SurvivalModelEventValue { get; set; }

  private static string[] ParseColumns(string value)
  {
    if (string.IsNullOrWhiteSpace(value)) return [];
    if (value.TrimStart().StartsWith('[')) return System.Text.Json.JsonSerializer.Deserialize<string[]>(value) ?? [];
    return value.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
  }

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    var errors = new List<ValidationResult>();
    try
    {
      _ = RowDepedenciesLinked;
      _ = DatasetHasIdColumnList;
      if (RowDepedencies && RowDepedenciesLinked.Length == 0)
        errors.Add(new ValidationResult("Sélectionnez la colonne de regroupement des observations.", [nameof(RowDepedenciesLinkedString)]));
      using var study = System.Text.Json.JsonDocument.Parse(Pipeline);
      if (study.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !study.RootElement.TryGetProperty("schema_version", out var version) || version.ValueKind != System.Text.Json.JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
        errors.Add(new ValidationResult("La configuration doit être une étude de DrIA avec schema_version: 1.", [nameof(Pipeline)]));
      if (!string.IsNullOrWhiteSpace(SurvivalModelEventValue))
      {
        using var scalar = System.Text.Json.JsonDocument.Parse(SurvivalModelEventValue);
        if (scalar.RootElement.ValueKind is not (System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
          errors.Add(new ValidationResult("La valeur d’événement doit être un scalaire JSON.", [nameof(SurvivalModelEventValue)]));
      }
    }
    catch (System.Text.Json.JsonException)
    {
      errors.Add(new ValidationResult("La configuration, les listes de colonnes et la valeur d’événement doivent utiliser un JSON valide."));
    }
    if (SurvivalModel && (string.IsNullOrWhiteSpace(SurvivalModelDatasetTargetTime) || SurvivalModelDatasetTargetTime == CsvColumnToPredict))
      errors.Add(new ValidationResult("Le temps de survie doit utiliser une colonne distincte de l’événement.", [nameof(SurvivalModelDatasetTargetTime)]));
    return errors;
  }

}
