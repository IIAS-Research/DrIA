using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Authorization;
using Server.DTOs;
using Server.Models;
using Server.Repositories;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProcessesController : ApplicationController
{
  private readonly ProjectService _projects;
  private readonly ProcessService _processes;
  private readonly ProcessRepository _repository;
  private readonly IaWebService _ia;

  public ProcessesController(UserManager<User> users, IaWebService ia, IAuthorizationService authorization, ProjectService projects, ProcessService processes, ProcessRepository repository) : base(users, authorization)
  {
    _projects = projects;
    _processes = processes;
    _repository = repository;
    _ia = ia;
  }

  private async Task<Process?> OwnedProcess(long id)
  {
    var process = await _repository.GetProcessById(id);
    if (process?.Project == null || !(await _authorizationService.AuthorizeAsync(User, process.Project, Operations.ReadOnlyOwn)).Succeeded) return null;
    return process;
  }

  [HttpGet("{processId:long}")]
  public async Task<IActionResult> GetProcessPipeline(long processId, [FromQuery] long projectId)
  {
    var process = await OwnedProcess(processId);
    if (process == null || process.ProjectId != projectId) return NotFound(new { error = "Traitement introuvable." });
    var response = await _processes.GetProcessPipeline(processId, projectId);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/status")]
  public async Task<IActionResult> GetStatus(long id)
  {
    var process = await OwnedProcess(id);
    if (process == null) return NotFound(new { error = "Traitement introuvable." });
    var response = await _ia.GetJobStatus(process.IaRemoteId);
    if (!response.IsSuccess && response.HttpStatusCode == 404 && process.StatusSnapshotJson != null && process.Status is Process.EProcessStatut.FINISHED or Process.EProcessStatut.CACHED or Process.EProcessStatut.CANCELED or Process.EProcessStatut.CRASHED or Process.EProcessStatut.TIMEOUT)
      return Ok(process.StatusSnapshot);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("{id:long}/cancel")]
  public async Task<IActionResult> Cancel(long id)
  {
    var process = await OwnedProcess(id);
    if (process == null) return NotFound(new { error = "Traitement introuvable." });
    var response = await _ia.CancelJob(process);
    if (!response.IsSuccess) return ServiceFailure(response);
    await _repository.UpdateProcessStatus(process.IaRemoteId, response.Data.status);
    return Accepted(response.Data);
  }

  [HttpGet("{id:long}/metrics")]
  public async Task<IActionResult> GetMetrics(long id)
  {
    var process = await OwnedProcess(id);
    if (process == null) return NotFound(new { error = "Traitement introuvable." });
    var response = await _ia.RetrieveJobMetric(process.IaRemoteId);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/history")]
  public async Task<IActionResult> GetHistory(long id)
  {
    var process = await OwnedProcess(id);
    if (process == null) return NotFound(new { error = "Traitement introuvable." });
    var response = await _ia.GetJobHistory(process.IaRemoteId);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("{id:long}/audit")]
  public async Task<IActionResult> GetAudit(long id)
  {
    var process = await OwnedProcess(id);
    if (process == null) return NotFound(new { error = "Traitement introuvable." });
    var response = await _ia.GetJobAudit(process.IaRemoteId);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost]
  public async Task<IActionResult> RunNewPipeline([FromBody] RunNewProcessDTO request)
  {
    var project = await _projects.GetProjectById(request.ProjectId);
    if (!project.IsSuccess) return ServiceFailure(project);
    if (!(await _authorizationService.AuthorizeAsync(User, project.Data, Operations.ReadOnlyOwn)).Succeeded) return Forbid();
    var files = await _projects.GetTrainAndTestFiles(request.ProjectId);
    if (!files.IsSuccess) return ServiceFailure(files);
    var configuration = JsonSerializer.Serialize(request.Pipeline);
    var response = await _ia.RunPipeline(files.Data.TrainFile, files.Data.TestFile, project.Data, configuration, request.MaxDuration);
    if (!response.IsSuccess) return ServiceFailure(response);
    var process = await _processes.CreateProcess(response.Data, configuration, project.Data);
    return Accepted(new { response.Data.Id, response.Data.Socket, response.Data.Status, projectId = project.Data.Id, processId = process.Id, process.MaxDuration });
  }

  [HttpPost("{id:long}/restart")]
  public async Task<IActionResult> RestartProcess(long id, [FromBody] RestartProcessRequestDTO request)
  {
    var previous = await OwnedProcess(id);
    if (previous == null) return NotFound(new { error = "Traitement introuvable." });
    var files = await _projects.GetTrainAndTestFiles(previous.ProjectId);
    if (!files.IsSuccess) return ServiceFailure(files);
    var response = await _ia.RunPipeline(files.Data.TrainFile, files.Data.TestFile, previous.Project!, previous.PipelineJson, request.MaxDuration);
    if (!response.IsSuccess) return ServiceFailure(response);
    var process = await _processes.CreateProcess(response.Data, previous.PipelineJson, previous.Project!);
    return Accepted(new { response.Data.Id, response.Data.Socket, response.Data.Status, projectId = process.ProjectId, processId = process.Id, process.MaxDuration });
  }
}
