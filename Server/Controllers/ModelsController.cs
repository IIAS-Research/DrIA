using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Server.Models;
using Server.Services;
using Server.Authorization;
using Server.DTOs;
using static Server.Services.ProjectService;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModelsController : ApplicationController
{
  private readonly ModelService _models;
  private readonly IaWebService _ia;
  private readonly ProjectService _projects;
  private readonly IMemoryCache _predictionExplanations;
  private record ObservationExplanationTicket(long ModelId, string ExplainerId);

  public ModelsController(UserManager<User> users, IAuthorizationService authorization, ModelService models, IaWebService ia, ProjectService projects,
    [FromKeyedServices("prediction-explanations")] IMemoryCache predictionExplanations) : base(users, authorization)
  {
    _models = models;
    _ia = ia;
    _projects = projects;
    _predictionExplanations = predictionExplanations;
  }

  private async Task<Model?> OwnedModel(long id)
  {
    var response = await _models.GetModeleWithProcessById(id);
    if (!response.IsSuccess || !(await _authorizationService.AuthorizeAsync(User, response.Data, Operations.Read)).Succeeded) return null;
    return response.Data;
  }

  [HttpGet("{id:long}")]
  public async Task<IActionResult> GetModelById(long id)
  {
    var model = await OwnedModel(id);
    return model == null ? NotFound(new { error = "Model not found." }) : Ok(model);
  }

  [HttpGet("{id:long}/explanation")]
  public Task<IActionResult> StartModelExplanationById(long id) => StartExplanation(id, null);

  [HttpPost("{id:long}/explanation")]
  public Task<IActionResult> StartModelExplanationById(long id, [FromBody] JsonElement options) => StartExplanation(id, options);

  private async Task<IActionResult> StartExplanation(long id, JsonElement? options)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var fileType = model.Process!.Project?.EvaluationMode == "cross_validation" ? ProjectFile.EProjectFileType.TRAIN : ProjectFile.EProjectFileType.TEST;
    var data = await _projects.GetProjectFile(model.Process.ProjectId, fileType);
    if (!data.IsSuccess) return ServiceFailure(data);
    var response = await _models.StartModelExplain(model, data.Data.Data, options);
    return response.IsSuccess ? Accepted(response.Data) : ServiceFailure(response);
  }

  private static Model.ModelExplanation? SavedExplanation(Model model) => string.IsNullOrWhiteSpace(model.Explanation) ? null : JsonSerializer.Deserialize<Model.ModelExplanation>(model.Explanation);

  [HttpGet("{id:long}/explanation/status")]
  public async Task<IActionResult> ExplanationStatus(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "No analysis has been started." });
    if (stored.status is "FINISHED" or "CACHED" or "CRASHED") return Ok(new { stored.id, status = new { current = stored.status }, stored.report, stored.error, stored.cache_error });
    var response = await _ia.GetExplainStatus(stored.id);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/explanation/results")]
  public async Task<IActionResult> ExplanationResults(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "No analysis has been started." });
    if (stored.status is "FINISHED" or "CACHED" or "CRASHED") return Ok(stored);
    var response = await _ia.GetExplainResults(stored.id);
    if (!response.IsSuccess) return ServiceFailure(response);
    return Ok(new Model.ModelExplanation { id = response.Data.id, status = response.Data.status.current.ToString(), results = response.Data.results, report = response.Data.report, error = response.Data.error, cache_error = response.Data.cache_error });
  }

  [HttpGet("{id:long}/explanation/plots")]
  public async Task<IActionResult> ExplanationPlots(long id, [FromQuery] string? key, [FromQuery] string? plots, [FromQuery] string? ps, [FromQuery(Name = "scatter_feature")] string? scatterFeature)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "No analysis has been started." });
    var response = await _ia.GetExplainShapPlots(stored.id, key, plots, ps, scatterFeature);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("{modelId:long}/predict")]
  public async Task<IActionResult> PredictValueUsingAiModel(long modelId, [FromBody] JsonElement request)
  {
    var model = await OwnedModel(modelId);
    if (model == null) return NotFound(new { error = "Model not found." });
    if (request.ValueKind != JsonValueKind.Object) return BadRequest(new { error = "The prediction request must be a JSON object." });
    if (request.TryGetProperty("rows", out _))
    {
      var file = await _models.GetModelFileById(modelId);
      if (!file.IsSuccess) return ServiceFailure(file);
      ProjectFileFormated? table;
      try { table = JsonSerializer.Deserialize<ProjectFileFormated>(request.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
      catch (JsonException) { return BadRequest(new { error = "Invalid observations." }); }
      if (table == null) return BadRequest(new { error = "Invalid observations." });
      var legacy = await _ia.PredictValueUsingAiModel(file.Data!, table, model.Process!.Project!.CsvColumnToPredict);
      return legacy.IsSuccess ? Ok(legacy.Data) : ServiceFailure(legacy);
    }
    var response = await _ia.PredictCandidate(model.Process!.IaRemoteId, model.IaRemoteId, request);
    if (!response.IsSuccess && response.HttpStatusCode == 404)
    {
      var file = await _models.GetModelFileById(modelId);
      if (!file.IsSuccess) return ServiceFailure(file);
      response = await _ia.PredictWithArtifact(file.Data!, request);
    }
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("{id:long}/prediction-explanation")]
  public async Task<IActionResult> StartPredictionExplanation(long id, [FromBody] JsonElement request)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var project = model.Process!.Project!;
    if (project.SurvivalModel)
      return BadRequest(new { error = "Local SHAP explanations are not available for survival models." });
    if (!TryObservationRows(request, project.CsvColumnToPredict, out var rows))
      return BadRequest(new { error = "Provide exactly one observation in json_row, using scalar values and no target column." });

    var file = await _models.GetModelFileById(id);
    if (!file.IsSuccess) return ServiceFailure(file);
    var prediction = await _ia.PredictWithArtifact(file.Data!, JsonSerializer.SerializeToElement(new { json_row = rows, method = "predict_proba" }));
    var probability = true;
    // Kernel SHAP explains probability column 1 when this method exists.
    // Other errors must remain visible, rather than changing the explained output.
    if (!prediction.IsSuccess && prediction.HttpStatusCode == StatusCodes.Status400BadRequest
      && prediction.Error == "The model does not support predict_proba.")
    {
      probability = false;
      prediction = await _ia.PredictWithArtifact(file.Data!, JsonSerializer.SerializeToElement(new { json_row = rows, method = "predict" }));
    }
    if (!prediction.IsSuccess) return ServiceFailure(prediction);
    if (!TryExplainedOutput(prediction.Data, probability, out var output))
      return StatusCode(StatusCodes.Status502BadGateway, new { error = "DrIA did not provide numeric output compatible with this explanation." });

    var explanation = await _ia.ExplainObservation(file.Data!, rows);
    if (!explanation.IsSuccess) return ServiceFailure(explanation);
    if (string.IsNullOrWhiteSpace(explanation.Data.id)
      || explanation.Data.status is not ("QUEUED" or "COMPUTING" or "FINISHED" or "CACHED" or "CRASHED"))
      return StatusCode(StatusCodes.Status502BadGateway, new { error = "DrIA did not provide a valid explanation status." });

    var ticket = Guid.NewGuid().ToString("N");
    var key = PredictionExplanationKey(ticket);
    // Store only the two identifiers, never the observation or generated image.
    _predictionExplanations.Set(key, new ObservationExplanationTicket(id, explanation.Data.id), new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
      Size = 1
    });
    if (!_predictionExplanations.TryGetValue(key, out ObservationExplanationTicket? _))
      return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Explanation progress is unavailable. Please try again shortly." });
    return Accepted(new { ticket, status = explanation.Data.status, output, nsamples = 64 });
  }

  [HttpGet("{id:long}/prediction-explanation/{ticket}")]
  public async Task<IActionResult> PredictionExplanation(long id, string ticket)
  {
    if (await OwnedModel(id) == null) return NotFound(new { error = "Model not found." });
    if (!Guid.TryParseExact(ticket, "N", out _)
      || !_predictionExplanations.TryGetValue(PredictionExplanationKey(ticket), out ObservationExplanationTicket? stored)
      || stored == null || stored.ModelId != id)
      return NotFound(new { error = "This explanation could not be found or has expired." });

    var response = await _ia.GetExplainStatus(stored.ExplainerId);
    if (!response.IsSuccess)
      return response.HttpStatusCode == StatusCodes.Status404NotFound
        ? Ok(new { status = "UNAVAILABLE", message = "This explanation is no longer available." })
        : ServiceFailure(response);
    var status = response.Data.status.current.ToString();
    if (status == "CRASHED")
      return Ok(new { status, message = "The explanation for this observation could not be calculated." });
    if (status is not ("FINISHED" or "CACHED")) return Ok(new { status });

    var plots = await _ia.GetExplainShapPlots(stored.ExplainerId, plots: "waterfall", ps: "0");
    if (!plots.IsSuccess)
      return plots.HttpStatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status404NotFound
        ? Ok(new { status = "UNAVAILABLE", message = "DrIA did not produce a SHAP plot for this observation." })
        : ServiceFailure(plots);
    if (!plots.Data.TryGetValue("waterfall", out var plot) || plot == null || !IsPng(plot.image))
      return Ok(new { status = "UNAVAILABLE", message = "DrIA did not produce a SHAP plot for this observation." });
    return Ok(new { status, plot });
  }

  private static string PredictionExplanationKey(string ticket) => "prediction-explanation:" + ticket;

  private static bool TryObservationRows(JsonElement request, string target, out JsonElement rows)
  {
    rows = default;
    if (request.ValueKind != JsonValueKind.Object || request.EnumerateObject().Count() != 1
      || !request.TryGetProperty("json_row", out rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != 1
      || rows[0].ValueKind != JsonValueKind.Object) return false;
    var columns = new HashSet<string>(StringComparer.Ordinal);
    foreach (var field in rows[0].EnumerateObject())
    {
      if (string.IsNullOrWhiteSpace(field.Name) || field.Name == target || !columns.Add(field.Name)) return false;
      if (field.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined) return false;
      if (field.Value.ValueKind == JsonValueKind.Number
        && (!field.Value.TryGetDouble(out var value) || !double.IsFinite(value))) return false;
    }
    return columns.Count > 0;
  }

  private static bool TryExplainedOutput(JsonElement response, bool probability, out PredictionExplanationOutputDTO? output)
  {
    output = null;
    if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("prediction", out var predictions)
      || predictions.ValueKind != JsonValueKind.Array || predictions.GetArrayLength() != 1) return false;
    var predicted = predictions[0];
    JsonElement? classLabel = null;
    if (probability)
    {
      if (predicted.ValueKind != JsonValueKind.Array || predicted.GetArrayLength() < 2
        || !response.TryGetProperty("classes", out var classes) || classes.ValueKind != JsonValueKind.Array
        || classes.GetArrayLength() != predicted.GetArrayLength()
        || classes[1].ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)) return false;
      classLabel = classes[1].Clone();
      predicted = predicted[1];
    }
    if (predicted.ValueKind != JsonValueKind.Number || !predicted.TryGetDouble(out var value)
      || !double.IsFinite(value) || (probability && (value < 0 || value > 1))) return false;
    output = new PredictionExplanationOutputDTO { kind = probability ? "probability" : "value", classLabel = classLabel, value = value };
    return true;
  }

  private static bool IsPng(string? image)
  {
    if (string.IsNullOrWhiteSpace(image)) return false;
    try
    {
      var bytes = Convert.FromBase64String(image);
      return bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    }
    catch (FormatException) { return false; }
  }

  [HttpPost("{id:long}/evaluate")]
  public async Task<IActionResult> Evaluate(long id, IFormFile dataset)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var file = new RunPipelineFileDTO { FileName = dataset.FileName, File = await IaWebService.IFormFileToByteArray(dataset) };
    var response = await _ia.EvaluateCandidate(model.Process!.IaRemoteId, model.IaRemoteId, model.Process.Project!, file);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/report")]
  public async Task<IActionResult> Report(long id, [FromQuery] string format = "json")
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    if (format == "markdown")
    {
      var response = await _ia.GetModelMarkdownReport(model.Process!.IaRemoteId, model.IaRemoteId);
      if (!response.IsSuccess)
      {
        if (response.HttpStatusCode == 404 && model.ReportMarkdown != null) return File(Encoding.UTF8.GetBytes(model.ReportMarkdown), "text/markdown", "model-report.md");
        return ServiceFailure(response);
      }
      return File(await IaWebService.IFormFileToByteArray(response.Data), "text/markdown", "model-report.md");
    }
    if (format != "json") return BadRequest(new { error = "The format must be json or markdown." });
    var report = await _ia.GetModelReport(model.Process!.IaRemoteId, model.IaRemoteId);
    if (!report.IsSuccess && report.HttpStatusCode == 404 && model.ReportJson != null) return Ok(JsonSerializer.Deserialize<JsonElement>(model.ReportJson));
    return report.IsSuccess ? Ok(report.Data) : ServiceFailure(report);
  }

  [HttpGet("{id:long}/audit")]
  public async Task<IActionResult> Audit(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var response = await _ia.GetModelAudit(model.Process!.IaRemoteId, model.IaRemoteId);
    if (!response.IsSuccess && response.HttpStatusCode == 404 && model.AuditJson != null) return Ok(JsonSerializer.Deserialize<JsonElement>(model.AuditJson));
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/download")]
  public async Task<IActionResult> Download(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Model not found." });
    var response = await _models.GetModelFileById(id);
    return response.IsSuccess ? File(response.Data!.FileBytes!, "application/octet-stream", "pyiaml_model.pickle") : ServiceFailure(response);
  }
}
