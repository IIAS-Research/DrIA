using System.ComponentModel.DataAnnotations;

namespace Server.Models;

public class UserFile : ApplicationRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    public required string Filename { get; set; }

    [Required]
    public required string StorageFilename { get; set; }

    [Required]
    public string MimeType { get; set; } = "application/octet-stream";

    [Timestamp]
    public required byte[] CreatedAt { get; set; }

    [Required]
    public required long UserId { get; set; }
    public User? User { get; set; }
}