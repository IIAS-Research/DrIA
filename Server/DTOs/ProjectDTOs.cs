using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Newtonsoft.Json;

namespace Server.DTOs;

public record StartProjectInDTO : IValidatableObject
{
  public required string Title { get; set; }

  public string? Description { get; set; } = string.Empty;

  public string EvaluationMode { get; set; } = "split";

  public IFormFile? TestFile { get; set; }

  public string? CreationContextJson { get; set; }

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
        errors.Add(new ValidationResult("Select the column used to group observations.", [nameof(RowDepedenciesLinkedString)]));
      using var study = System.Text.Json.JsonDocument.Parse(Pipeline);
      if (study.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !study.RootElement.TryGetProperty("schema_version", out var version) || version.ValueKind != System.Text.Json.JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
        errors.Add(new ValidationResult("The configuration must be a DrIA study with schema_version: 1.", [nameof(Pipeline)]));
      if (!string.IsNullOrWhiteSpace(SurvivalModelEventValue))
      {
        using var scalar = System.Text.Json.JsonDocument.Parse(SurvivalModelEventValue);
        if (scalar.RootElement.ValueKind is not (System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
          errors.Add(new ValidationResult("The event value must be a JSON scalar.", [nameof(SurvivalModelEventValue)]));
      }
      if (!string.IsNullOrWhiteSpace(CreationContextJson))
      {
        using var context = System.Text.Json.JsonDocument.Parse(CreationContextJson);
        if (context.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
          errors.Add(new ValidationResult("The creation context must be a JSON object.", [nameof(CreationContextJson)]));
      }
    }
    catch (System.Text.Json.JsonException)
    {
      errors.Add(new ValidationResult("The configuration, column lists and event value must use valid JSON."));
    }
    if (SurvivalModel && (string.IsNullOrWhiteSpace(SurvivalModelDatasetTargetTime) || SurvivalModelDatasetTargetTime == CsvColumnToPredict))
      errors.Add(new ValidationResult("Survival time must use a different column from the event.", [nameof(SurvivalModelDatasetTargetTime)]));
    if (EvaluationMode is not ("split" or "external" or "cross_validation"))
      errors.Add(new ValidationResult("Unknown evaluation mode.", [nameof(EvaluationMode)]));
    if (EvaluationMode == "external" && TestFile == null)
      errors.Add(new ValidationResult("Upload the file reserved for evaluation.", [nameof(TestFile)]));
    if (EvaluationMode != "external" && TestFile != null)
      errors.Add(new ValidationResult("The evaluation file can only be used with a separate test set.", [nameof(TestFile)]));
    return errors;
  }

}
