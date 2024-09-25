using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models;

public class Process : ApplicationRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    public required DateTime Date { get; set; }

    [Required]
    public required int Duration { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public required ProcessPipeline Pipeline { get; set; }

    [Required]
    public required EProcessStatut Statut { get; set; }

    [Timestamp]
    public required byte[] CreatedAt { get; set; }

    [Timestamp]
    public required byte[] UpdatedAt { get; set; }

    [Required]
    public required long ProjectId { get; set; }
    public Project? Project { get; set; }

    public enum EProcessStatut
    {

    }

    public record ProcessPipeline
    {
        public required string Step { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public ProcessPipelineConfiguration? Configuration { get; set; }
        public required ProcessPipeline[] Children { get; set; }

        public record ProcessPipelineConfiguration
        {
            public ProcessPipelineConfigurationChild? AuthorizedErrorRatios { get; set; }
            public ProcessPipelineConfigurationChild? SampleSize { get; set; }

            public record ProcessPipelineConfigurationChild
            {
                public float Default { get; set; }
                public required string Description { get; set; }
                public float Value { get; set; }
            }
        }

    }
}