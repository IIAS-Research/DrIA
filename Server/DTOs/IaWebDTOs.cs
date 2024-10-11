using Server.Models;
using static Server.Models.Process;

namespace Server.DTOs;

public record RunPipelineInDTO
{
  public required byte[] train_dataset = [];
  public byte[]? test_dataset;
  public required string dataset_target = string.Empty;
  public required Process.ProcessPipeline pipeline;
}
public record RunPipelineOutDTO
{
  public required string Id { get; set; }
  public required string Socket { get; set; }
  public required EProcessStatut Status { get; set; }
  public required int MaxDuration { get; set; }
}

public record GeneratePipelineOutDTO
{
  public required ProcessPipeline pipeline;
}

public record GeneratePipelineWebServiceJsonDTO
{
  public required object Pipeline;
}

public record JobStatusOutDTO
{
  public required string id { get; init; }
  public required string socket { get; init; }
  public required JobStatusOut status { get; init; }
  public float? best_train_accuracy { get; init; }
  public float? best_test_accuracy { get; init; }
}

public record JobStatusOut
{
  public required EProcessStatut current { get; init; } // Change to string for deserialization
  public required List<JobStatusHistory> history { get; init; } // Change to List
}

public record JobStatusHistory
{
  public required string date { get; init; } // Change type to DateTime
  public required string status { get; init; } // Change type to string
}
public record CancelJobOutDTO
{
  public required string Id;
  public required EProcessStatut status;
}

public record GetJobMetricsOutDTO
{
  public required string id { get; init; }
  public required Metrics[] metrics { get; init; }
}

public record Metrics
{
  public required int id { get; init; }
  public required Dictionary<string, MetricInfo> metrics_info { get; init; }
  public required ModelInfo model_info { get; init; }
  public required string main_metrics { get; init; }
  public required Dictionary<string, float> metrics_train { get; init; }
  public required Dictionary<string, float>? metrics_test { get; init; }
  public required Plot[] plots { get; init; }
}

public record MetricInfo
{
  public required string name { get; init; }
  public required string desc { get; init; }
}

public record ModelInfo
{
  public required string name { get; init; }
  public required string description { get; init; }
  public required string description_long { get; init; }
}

public record Plot
{
  public required string title { get; init; }
  public required string description { get; init; }
  public required string image { get; init; }
}


public record ExplainModelInDTO
{
  public required IFormFile dataset;
}
public record ExplainModelOutDTO
{
  public required string id { get; init; }
  public required string socket { get; init; }
  public required EProcessStatut status { get; init; }
}

public record GetModelTextExplanationOutDTO
{
  public required string id { get; init; }
  public required int rid { get; init; }
  public required string[] explanations { get; init; }
}

public record SplitOutDTO
{
  public required string id { get; set; }
  public required string socket { get; set; }
  public required EProcessStatut status { get; set; }
}

public record GetSplitStatusOutDTO
{
  public required string id { get; init; }
  public required string socket { get; init; }
  public required SplitStatus status { get; init; }

  public record SplitStatus
  {
    public required EProcessStatut current;
    public object? history;
  }
}

public record GetModelFeaturesInDTO
{
  public required Model.ModelExplanation.ModelExplainFeature[] features { get; set;}
}

public record RunNewProcessDTO
{
  public required long ProjectId { get; set; }
  public required ProcessPipeline Pipeline { get; set; } // JSON
}

public record RunNewPipelineDTO
{
  public required Project Project { get; set; }
  public required ProcessPipeline PipelineToRun { get; set; }
  public required byte[] TrainFile { get; set; }
  public required byte[] TestFile { get; set; }
}

public record GetTrainAndTestFilesOutDTO
{
  public required RunPipelineFileDTO TrainFile { get; set; }
  public required RunPipelineFileDTO TestFile { get; set; }
}

public record RunPipelineFileDTO
{
  public required byte[] File { get; set; }
  public required string FileName { get; set; }
}