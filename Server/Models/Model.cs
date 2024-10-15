using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Server.DTOs;

namespace Server.Models;

public class Model
{
  private string _bibliographyJson = "";
  private string _metricsJson = "";


  [Key]
  public long Id { get; set; }

  [Required]
  public required int IaRemoteId { get; set; }

  [Required]
  public required DateTime Date { get; set; }

  [NotMapped] // This ensures Entity Framework doesn't treat this as a mapped column
  public ModelBibliography? Bibliography
  {
    get
    {
      return string.IsNullOrWhiteSpace(_bibliographyJson)
          ? null
          : JsonSerializer.Deserialize<ModelBibliography>(_bibliographyJson);
    }
    set
    {
      // Serialize the object to JSON
      _bibliographyJson = JsonSerializer.Serialize(value);
    }
  }

  [Required]
  [Column(TypeName = "jsonb")]
  public string BibliographyJson
  {
    get => _bibliographyJson;
    set => _bibliographyJson = value;
  }

  [Column(TypeName = "jsonb")]
  public string? Explanation { get; set; }

  public string[] TextExplanation { get; set; } = [];

  [NotMapped] // This ensures Entity Framework doesn't treat this as a mapped column
  public object? Metrics
  {
    get
    {
      return string.IsNullOrWhiteSpace(_metricsJson)
          ? null
          : JsonSerializer.Deserialize<object>(_metricsJson);
    }
    set
    {
      // Serialize the object to JSON
      _metricsJson = JsonSerializer.Serialize(value);
    }
  }

  [Required]
  [Column(TypeName = "jsonb")]
  public string MetricsJson
  {
    get => _metricsJson;
    set => _metricsJson = value;
  }

  [Required]
  public required long ProcessId { get; set; }
  public Process? Process { get; set; }


  public record ModelBibliography
  {
    public required string id { get; set; }
    public required int rid { get; set; }
    public required string bibliography { get; set; }
    public required StructuredBibliography[] structured_bibliography { get; set; }
  }
  public record StructuredBibliography
  {
    public required string step { get; set; }
    public required int year { get; set; }
    public required string name { get; set; }
    public required string[] authors { get; set; }
    public required string doi { get; set; }
    public required string publisher { get; set; }
  }

  public record ModelExplanation
  {
    public required Dictionary<string, Plot> plots { get; set; }
    public required ModelExplainFeature[] features { get; set; }

    public record ModelExplainFeature
    {
      public required string name { get; set; }
      public required float value { get; set; }
    }
  }

}