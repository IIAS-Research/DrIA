using System.ComponentModel.DataAnnotations;

namespace Server.Models;

public class ProjectFile : ApplicationRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    public required string Filename { get; set; }

    [Required]
    public required string StorageFilename { get; set; }

    [Required]
    public required string MimeType { get; set; } = "application/octet-stream";

    [Required]
    public required EProjectFileType FileType { get; set;} = EProjectFileType.DEFAULT;

    [Timestamp]
    public byte[]? CreatedAt { get; set; } = [];

    [Required]
    public required long ProjectId { get; set; }
    public Project? Project { get; set; }

    public enum EProjectFileType
    {
      DEFAULT,
      TEST,
      TRAIN
    }
}