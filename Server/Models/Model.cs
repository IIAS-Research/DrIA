using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models;

public class Model
{
    [Required]
    public required DateTime Date { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public required ModelResult Results { get; set; }
    
    [Required]
    public required string PredictionModelName { get; set; }
    
    [Required]
    [Column(TypeName = "jsonb")]
    public required ModelBibliographie Bibliographie { get; set;}

    [Required]
    [Column(TypeName = "jsonb")]
    public required ModelExplanation Explanation { get; set;}

    

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