using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Server.Models;

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

    [Required]
    public required string[] RowDepedenciesLinked { get; set; } = [];

    [Required]
    public required bool DatasetHasIdColumn { get; set; }

    [Required]
    public required string[] DatasetHasIdColumnList { get; set; } = [];

    [Required]
    public required string Pipeline { get; set; } // Deserialize in the service

    [Required]
    public required IFormFile File { get; set; }
}
