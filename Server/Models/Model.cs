using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Server.DTOs;

namespace Server.Models;

public class Model
{
  [Key]
  public long Id { get; set; }

  [Required]
  public required DateTime Date { get; set; }

  [Required]
  [Column(TypeName = "jsonb")]
  public required ModelBibliographie Bibliography { get; set; }

  [Column(TypeName = "jsonb")]
  public ModelExplanation? Explanation { get; set; }

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
        public static implicit operator ModelBibliographie(GetJobBibliographyOutDTO v)
        {
            throw new NotImplementedException();
        }
    }

    public record ModelExplanation
  {

  }

}