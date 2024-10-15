using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.Authorization;
using Server.DTOs;
using System.Text.Json;
using static Server.Models.ProjectFile;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectService _projectService;
    private readonly ProcessService _processService;
    private readonly IaWebService _iaWebService;

    public ProjectsController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService, ProjectService projectRepository, ProcessService processService) : base(userManager, authorizationService)
    {
        _projectService = projectRepository;
        _processService = processService;
        _iaWebService = iaWebService;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetProjects()
    {
        var projectResponseGetAll = await _projectService.GetProjects();
        return Ok(projectResponseGetAll.Projects);
    }

    [HttpGet]
    [Route("me")]
    public async Task<IActionResult> GetUserProjects()
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return Unauthorized();

        var userProjects = await _projectService.GetUserProjects(currentUser!);

        return Ok(userProjects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(long id)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(id);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(projectResponseGetOne.Data);
    }

    [HttpGet("{id}/nested-data")]
    public async Task<IActionResult> GetProjectNestedDataById(long id)
    {
        var responseProject = await _projectService.GetProjectNestedDataById(id);
        if (responseProject.IsSuccess == false) return NotFound(responseProject.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, responseProject.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(responseProject.Data);
    }

    [HttpPost]
    // [RequestTimeout(milliseconds: 300000)]
    public async Task<ActionResult<RunPipelineOutDTO>> RegisterProject([FromForm] StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return BadRequest();

        var responseProject = await _projectService.CreateProject(projectData, currentUser!);
        if (!responseProject.IsSuccess) return BadRequest(responseProject.Errors);

        var responseSplit = await _iaWebService.Split(projectData.File, responseProject.Data);
        if (!responseSplit.IsSuccess) return BadRequest(responseSplit.Errors);



        // Get and save files
        var responseGetTrainFile = await _iaWebService.GetSplitTrainFile(responseSplit.Data.id);
        if (!responseGetTrainFile.IsSuccess) return BadRequest(responseGetTrainFile.Errors);
        var responseCreateProjectFileTrain = await _projectService.CreateProjectFile(responseGetTrainFile.Data, responseProject.Data, ProjectFile.EProjectFileType.TRAIN);
        if (!responseCreateProjectFileTrain.IsSuccess) return BadRequest(responseCreateProjectFileTrain.Errors);
        var responseGetTestFile = await _iaWebService.GetSplitTestFile(responseSplit.Data.id);
        if (!responseGetTestFile.IsSuccess) return BadRequest(responseGetTestFile.Errors);
        var responseCreateProjectFileTest = await _projectService.CreateProjectFile(responseGetTestFile.Data, responseProject.Data, ProjectFile.EProjectFileType.TEST);
        if (!responseCreateProjectFileTest.IsSuccess) return BadRequest(responseCreateProjectFileTest.Errors);



        RunPipelineFileDTO runPipelineTrainFileDTO = new() { File = await IaWebService.IFormFileToByteArray(responseGetTrainFile.Data), FileName = responseGetTrainFile.Data.FileName };
        RunPipelineFileDTO runPipelineTestFileDTO = new() { File = await IaWebService.IFormFileToByteArray(responseGetTestFile.Data), FileName = responseGetTestFile.Data.FileName };

        var getProjectBaselineMetricsResponse = await _iaWebService.GetProjectBaselineMetrics(runPipelineTrainFileDTO, runPipelineTestFileDTO, projectData.CsvColumnToPredict);
        if (!getProjectBaselineMetricsResponse.IsSuccess) return BadRequest(getProjectBaselineMetricsResponse.Errors);

        responseProject.Data.MetricsBaselineJson = JsonSerializer.Serialize(getProjectBaselineMetricsResponse.Data);
        var updateProjectResponse = await _projectService.UpdateProjectById(responseProject.Data);
        if (!updateProjectResponse.IsSuccess) return BadRequest(updateProjectResponse.Errors);


        var responseIaWeb = await _iaWebService.RunPipeline(runPipelineTrainFileDTO, runPipelineTestFileDTO, responseProject.Data, projectData.Pipeline, null);
        if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

        var responseProcess = await _processService.CreateProcess(responseIaWeb.Data, projectData.Pipeline, responseProject.Data);
        return Ok(new { responseIaWeb.Data.Id, responseIaWeb.Data.Socket, responseIaWeb.Data.Status, projectId = responseProject.Data.Id, responseProcess.MaxDuration });
    }

    [HttpGet("{projectId}/data-sample")]
    public async Task<ActionResult<ProjectService.ProjectFileFormated>> GetProjectDefaultFileLines(long projectId, [FromQuery] int rowCount = 10)
    {

        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        if (rowCount > 50 || rowCount < 1)
        {
            rowCount = 50;
        }
        var responseFile = await _projectService.GetProjectFileFirstNLine(projectId, rowCount, EProjectFileType.TEST);
        if (responseFile.IsSuccess == false) return NotFound(responseFile.Errors);

        return Ok(responseFile.Data);
    }

    [HttpGet("{projectId}/defaultFile")]
    public async Task<ActionResult> GetProjectDefaultFile(long projectId)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        var responseFile = await _projectService.GetProjectFile(projectId, ProjectFile.EProjectFileType.DEFAULT);
        if (responseFile.IsSuccess == false) return BadRequest(responseFile.Errors);

        return File(responseFile.Data.Data, responseFile.Data.MimeType);
    }
}
