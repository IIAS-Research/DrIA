using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models;

public class Model
{
  [Key]
  public required long Id { get; set; }

  [Required]
  public required DateTime Date { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required ModelResult Results { get; set; }

  [Required]
  public required string PredictionModelName { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required ModelBibliographie Bibliographie { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required ModelExplanation Explanation { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required object Metrics { get; set; }

  [Required]
  public required long ProcessId { get; set; }
  public Process? Process { get; set; }


  public record ModelResult
  {

  }

  public record ModelBibliographie
  {

  }

  public record ModelExplanation
  {

  }

}