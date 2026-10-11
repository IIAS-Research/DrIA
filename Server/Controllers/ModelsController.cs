using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

  public ModelsController(UserManager<User> users, IAuthorizationService authorization, ModelService models, IaWebService ia, ProjectService projects) : base(users, authorization)
  {
    _models = models;
    _ia = ia;
    _projects = projects;
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
    return model == null ? NotFound(new { error = "Modèle introuvable." }) : Ok(model);
  }

  [HttpGet("{id:long}/explanation")]
  public Task<IActionResult> StartModelExplanationById(long id) => StartExplanation(id, null);

  [HttpPost("{id:long}/explanation")]
  public Task<IActionResult> StartModelExplanationById(long id, [FromBody] JsonElement options) => StartExplanation(id, options);

  private async Task<IActionResult> StartExplanation(long id, JsonElement? options)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
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
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "Aucune analyse n’a été lancée." });
    if (stored.status is "FINISHED" or "CACHED" or "CRASHED") return Ok(new { stored.id, status = new { current = stored.status }, stored.report, stored.error, stored.cache_error });
    var response = await _ia.GetExplainStatus(stored.id);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/explanation/results")]
  public async Task<IActionResult> ExplanationResults(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "Aucune analyse n’a été lancée." });
    if (stored.status is "FINISHED" or "CACHED" or "CRASHED") return Ok(stored);
    var response = await _ia.GetExplainResults(stored.id);
    if (!response.IsSuccess) return ServiceFailure(response);
    return Ok(new Model.ModelExplanation { id = response.Data.id, status = response.Data.status.current.ToString(), results = response.Data.results, report = response.Data.report, error = response.Data.error, cache_error = response.Data.cache_error });
  }

  [HttpGet("{id:long}/explanation/plots")]
  public async Task<IActionResult> ExplanationPlots(long id, [FromQuery] string? key, [FromQuery] string? plots, [FromQuery] string? ps, [FromQuery(Name = "scatter_feature")] string? scatterFeature)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var stored = SavedExplanation(model);
    if (stored?.id == null) return NotFound(new { error = "Aucune analyse n’a été lancée." });
    var response = await _ia.GetExplainShapPlots(stored.id, key, plots, ps, scatterFeature);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("{modelId:long}/predict")]
  public async Task<IActionResult> PredictValueUsingAiModel(long modelId, [FromBody] JsonElement request)
  {
    var model = await OwnedModel(modelId);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    if (request.ValueKind != JsonValueKind.Object) return BadRequest(new { error = "La requête de prédiction doit être un objet JSON." });
    if (request.TryGetProperty("rows", out _))
    {
      var file = await _models.GetModelFileById(modelId);
      if (!file.IsSuccess) return ServiceFailure(file);
      ProjectFileFormated? table;
      try { table = JsonSerializer.Deserialize<ProjectFileFormated>(request.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
      catch (JsonException) { return BadRequest(new { error = "Observations invalides." }); }
      if (table == null) return BadRequest(new { error = "Observations invalides." });
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

  [HttpPost("{id:long}/evaluate")]
  public async Task<IActionResult> Evaluate(long id, IFormFile dataset)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var file = new RunPipelineFileDTO { FileName = dataset.FileName, File = await IaWebService.IFormFileToByteArray(dataset) };
    var response = await _ia.EvaluateCandidate(model.Process!.IaRemoteId, model.IaRemoteId, model.Process.Project!, file);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/report")]
  public async Task<IActionResult> Report(long id, [FromQuery] string format = "json")
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
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
    if (format != "json") return BadRequest(new { error = "Le format doit être json ou markdown." });
    var report = await _ia.GetModelReport(model.Process!.IaRemoteId, model.IaRemoteId);
    if (!report.IsSuccess && report.HttpStatusCode == 404 && model.ReportJson != null) return Ok(JsonSerializer.Deserialize<JsonElement>(model.ReportJson));
    return report.IsSuccess ? Ok(report.Data) : ServiceFailure(report);
  }

  [HttpGet("{id:long}/audit")]
  public async Task<IActionResult> Audit(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var response = await _ia.GetModelAudit(model.Process!.IaRemoteId, model.IaRemoteId);
    if (!response.IsSuccess && response.HttpStatusCode == 404 && model.AuditJson != null) return Ok(JsonSerializer.Deserialize<JsonElement>(model.AuditJson));
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/download")]
  public async Task<IActionResult> Download(long id)
  {
    var model = await OwnedModel(id);
    if (model == null) return NotFound(new { error = "Modèle introuvable." });
    var response = await _models.GetModelFileById(id);
    return response.IsSuccess ? File(response.Data!.FileBytes!, "application/octet-stream", "pyiaml_model.pickle") : ServiceFailure(response);
  }
}
