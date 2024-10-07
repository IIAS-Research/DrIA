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
}

public record MetricInfo
{
  public required string name { get; init; }
  public required string desc { get; init; }
}


public record GetJobBibliographyOutDTO
{
  public required string IaRemoteId { get; set; }
  public required int Rid { get; set; }
  public required string Bibliography { get; set; }
  public required IStructuredBibliography StructuredBibliography { get; set; }
}


public record ExplainModelInDTO
{
  public required IFormFile dataset;
}
public record ExplainModelOutDTO
{
  public required string id;
  public required string socket;
  public required Process.EProcessStatut status;
}

public record GetModelTextExplanationOutDTO
{
  public required string id;
  public int? rid;
  public required string[] explanations;
}

public record SplitOutDTO
{
  public required string id { get; set; }
  public required string socket { get; set; }
  public required Process.EProcessStatut status { get; set; }
}

public record GetSplitStatusOutDTO
{
  public required string id;
  public required string socket;
  public required SplitStatus status;

  public record SplitStatus
  {
    public required Process.EProcessStatut current;
    public object? history;
  }
}

public record IStructuredBibliography
{
  public required string Step { get; set; }
  public required int Year { get; set; }
  public required string Name { get; set; }
  public required string[] Authors { get; set; }
  public required string Doi { get; set; }
  public required string Publisher { get; set; }
}
