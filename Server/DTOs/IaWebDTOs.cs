using Server.Models;

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
  public required Process.EProcessStatut Status { get; set; }
  public required int MaxDuration { get; set; }
}

public record GeneratePipelineOutDTO
{
  public required Process.ProcessPipeline pipeline;
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
  public required Process.EProcessStatut current { get; init; } // Change to string for deserialization
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
  public required Process.EProcessStatut status;
}

public record GetJobMetricsOutDTO
{
  public required string id { get; init; }
  public required Metrics[] metrics { get; init; }
}

public record Metrics
{
  public required int id { get; init; }
  public required string model { get; init; }
  public required Dictionary<string, MetricInfo> metrics_info { get; init; }
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

public record Plot
{
  public required string title { get; init; }
  public required string description { get; init; }
  public required string image { get; init; }
}


// public record GetJobBibliographyOutDTO
// {
//   public required string id { get; set; }
//   public required int rid { get; set; }
//   public required string bibliography { get; set; }
//   public required StructuredBibliography[] structured_bibliography { get; set; }
// }


public record ExplainModelInDTO
{
  public required IFormFile dataset;
}
public record ExplainModelOutDTO
{
  public required string id { get; init; }
  public required string socket { get; init; }
  public required Process.EProcessStatut status { get; init; }
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
  public required Process.EProcessStatut status { get; set; }
}

public record GetSplitStatusOutDTO
{
  public required string id { get; init; }
  public required string socket { get; init; }
  public required SplitStatus status { get; init; }

  public record SplitStatus
  {
    public required Process.EProcessStatut current;
    public object? history;
  }
}

public record GetModelFeaturesInDTO
{
  public required Model.ModelExplanation.ModelExplainFeature[] features { get; set;}
}

// public record StructuredBibliography
// {
//   public required string step { get; set; }
//   public required int year { get; set; }
//   public required string name { get; set; }
//   public required string[] authors { get; set; }
//   public required string doi { get; set; }
//   public required string publisher { get; set; }
// }
