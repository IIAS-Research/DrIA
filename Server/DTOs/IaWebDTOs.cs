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
  public required string id;
  public required string socket;
  public required Process.EProcessStatut status;
}

public record GeneratePipelineOutDTO
{
  public required Process.ProcessPipeline pipeline;
}

public record JobStatusOutDTO
{
  public required string id;
  public required string socket;
  public required object status;
  public float? best_train_accuracy;
  public float? best_test_accuracy;
}

public record CancelJobOutDTO
{
  public required string id;
  public required Process.EProcessStatut status;
}

public record GetJobMetricsOutDTO
{
  public required string id;
  public required object[] results;
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

public record GetModelBibliographyOutDTO
{
  public required string id;
  public int? rid;
  public required string bibliography;
  public required object structured_bibliography;
}
