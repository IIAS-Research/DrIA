using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.DTOs;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProcessesController : ApplicationController
{
  private readonly ProjectService _projectService;
  private readonly ProcessService _processService;
  private readonly IaWebService _iaWebService;
  public ProcessesController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService, ProjectService projectRepository, ProcessService processService) : base(userManager, authorizationService)
  {
    _projectService = projectRepository;
    _processService = processService;
    _iaWebService = iaWebService;
  }

  [HttpGet("{processId}")]
  public async Task<IActionResult> GetProcessPipeline(string processId, [FromQuery] string projectId)
  {
    var processPipelineResponse = await _processService.GetProcessPipeline(processId, projectId);
    if (!processPipelineResponse.IsSuccess) return BadRequest(processPipelineResponse.Errors);
    return Ok(processPipelineResponse.Data);
  }

  [HttpPost]
  public async Task<IActionResult> RunNewPipeline([FromBody] RunNewProcessDTO runNewProcessDTO)
  {
    var responseGetProjectFiles = await _projectService.GetTrainAndTestFiles(runNewProcessDTO.ProjectId);
    if (!responseGetProjectFiles.IsSuccess) return BadRequest(responseGetProjectFiles.Errors);

    var responseGetProject = await _projectService.GetProjectById(runNewProcessDTO.ProjectId);
    if (!responseGetProject.IsSuccess) return BadRequest(responseGetProject.Errors);

    var responseIaWeb = await _iaWebService.RunPipeline(responseGetProjectFiles.Data.TrainFile, responseGetProjectFiles.Data.TestFile, responseGetProject.Data, JsonSerializer.Serialize(runNewProcessDTO.Pipeline));
    if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

    var responseProcess = await _processService.CreateProcess(responseIaWeb.Data, JsonSerializer.Serialize(runNewProcessDTO.Pipeline), responseGetProject.Data);
    return Ok(new { responseIaWeb.Data.Id, responseIaWeb.Data.Socket, responseIaWeb.Data.Status, projectId = responseGetProject.Data.Id, responseProcess.MaxDuration });
  }

}