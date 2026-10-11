using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Models;
using static Server.Models.Process;

namespace Server.DTOs;

public record RunPipelineInDTO
{
  public required byte[] train_dataset = [];
  public byte[]? test_dataset;
  public required string dataset_target = string.Empty;
  public required ProcessPipeline configuration;
}

public record RunPipelineOutDTO
{
  public string Id { get; set; } = string.Empty;
  public string? Socket { get; set; }
  public EProcessStatut Status { get; set; }
  public int MaxDuration { get; set; } = 60;
  public double? TotalTimeout { get; set; }
  public int? SchemaVersion { get; set; }
  public string? Mode { get; set; }
  [JsonIgnore]
  public string? ConfigurationJson { get; set; }
}

public record GeneratePipelineOutDTO
{
  public required ProcessPipeline pipeline;
}

public record GeneratePipelineWebServiceJsonDTO
{
  public required object Pipeline;
}

public record MetricObjective
{
  public string key { get; init; } = string.Empty;
  public bool greater_is_better { get; init; }
}

public record JobStatusOutDTO
{
  public string id { get; init; } = string.Empty;
  public string? socket { get; init; }
  public JobStatusOut status { get; init; } = new();
  public double? best_train_metric { get; init; }
  public double? best_test_metric { get; init; }
  public MetricObjective? objective { get; init; }
  public string? score_source { get; init; }
  public string? mode { get; init; }
  public int? chosen_candidate { get; init; }
  public string? error { get; init; }
  public string? cache_error { get; init; }
  public JsonElement? logs { get; init; }
}

public record ExplainStatusOutDTO
{
  public string id { get; init; } = string.Empty;
  public string? socket { get; init; }
  public JobStatusExplainOut status { get; init; } = new();
  public JsonElement? report { get; init; }
  public string? error { get; init; }
  public string? cache_error { get; init; }
}

public record ExplainResultsOutDTO : ExplainStatusOutDTO
{
  public Dictionary<string, JsonElement> results { get; init; } = [];
}

public record JobStatusExplainOut
{
  public EExplainStatut current { get; init; }
  public List<JobStatusHistory> history { get; init; } = [];
}

public record JobStatusOut
{
  public EProcessStatut current { get; init; }
  public List<JobStatusHistory> history { get; init; } = [];
}

public enum EExplainStatut
{
  COMPUTING,
  FINISHED,
  CRASHED,
  QUEUED,
  CACHED
}

public record JobStatusHistory
{
  public string date { get; init; } = string.Empty;
  public string status { get; init; } = string.Empty;
}

public record CancelJobOutDTO
{
  public string Id { get; init; } = string.Empty;
  public EProcessStatut status { get; init; }
}

public record GetJobMetricsOutDTO
{
  public string id { get; init; } = string.Empty;
  public Metrics[] metrics { get; init; } = [];
}

public record Metrics
{
  public int id { get; init; }
  public Dictionary<string, MetricInfo> metrics_info { get; init; } = [];
  public ModelInfo model_info { get; init; } = new();
  public string? main_metric { get; init; }
  public string? main_metrics { get; init; }
  public MetricObjective? objective { get; init; }
  public string? score_source { get; init; }
  public Dictionary<string, double?> metrics_train { get; init; } = [];
  public Dictionary<string, double?>? metrics_test { get; init; }
  public Plot[]? plots { get; init; }
  public JsonElement? metric_report { get; init; }
  public JsonElement? evaluation_report { get; init; }
  public JsonElement? explanation_report { get; init; }
  public JsonElement? metric_coverage { get; init; }
  public JsonElement? training_audit { get; init; }
}

public record MetricInfo
{
  public string name { get; init; } = string.Empty;
  public string desc { get; init; } = string.Empty;
  public bool? greater_is_better { get; init; }
}

public record ModelInfo
{
  public string name { get; init; } = string.Empty;
  public string description { get; init; } = string.Empty;
  public string description_long { get; init; } = string.Empty;
}

public record Plot
{
  public string title { get; init; } = string.Empty;
  public string description { get; init; } = string.Empty;
  public string image { get; init; } = string.Empty;
}

public record ExplainModelInDTO
{
  public required IFormFile dataset;
}

public record ExplainModelOutDTO
{
  public string id { get; init; } = string.Empty;
  public string? socket { get; init; }
  public string status { get; init; } = string.Empty;
}

public record PredictionExplanationOutputDTO
{
  public required string kind { get; init; }
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public JsonElement? classLabel { get; init; }
  public required double value { get; init; }
}

public record GetModelTextExplanationOutDTO
{
  public string id { get; init; } = string.Empty;
  public int rid { get; init; }
  public string[] explanations { get; init; } = [];
  public Dictionary<string, JsonElement> results { get; init; } = [];
  public JsonElement? configured_report { get; init; }
  public EExplainStatut? status { get; init; }
  public string? explainer_id { get; init; }
}

// Split is synchronous. Its identifier is immediately usable for both downloads.
public record SplitOutDTO
{
  public string id { get; set; } = string.Empty;
  public JsonElement? train { get; set; }
  public JsonElement? test { get; set; }
}

public record GetModelFeaturesInDTO
{
  public Dictionary<string, JsonElement> features { get; set; } = [];
}

public record RunNewProcessDTO
{
  public required long ProjectId { get; set; }
  public required ProcessPipeline Pipeline { get; set; }
  public int MaxDuration { get; set; } = 60;
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
  public RunPipelineFileDTO? TestFile { get; set; }
}

public record RunPipelineFileDTO
{
  public required byte[] File { get; set; }
  public required string FileName { get; set; }
}

public record PredictWithModelAiResponseDTO
{
  public string status { get; init; } = string.Empty;
  public string method { get; init; } = "predict";
  public JsonElement prediction { get; init; }
  public JsonElement? classes { get; init; }
}

public record RestartProcessRequestDTO
{
  public int MaxDuration { get; set; } = 60;
}

public record GetProjectBaselineMetricsInDTO
{
  public string? id { get; init; }
  public string? status { get; init; }
  public Dictionary<string, MetricInfo> metrics_info { get; init; } = [];
  public Dictionary<string, double?> metrics { get; init; } = [];
  public Dictionary<string, double?>? metrics_test { get; init; }
  public string? main_metric { get; init; }
  public string? main_metrics { get; init; }
  public MetricObjective? objective { get; init; }
  public string? score_source { get; init; }
  public JsonElement? metric_report { get; init; }
  public JsonElement? evaluation_report { get; init; }
}
