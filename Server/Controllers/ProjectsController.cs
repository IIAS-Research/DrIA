using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.Authorization;
using Server.DTOs;

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
        if (currentUser is null) return BadRequest();

        var userProjects = await _projectService.GetUserProjects(currentUser!);
        
        return Ok(userProjects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(long id)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(id);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Project, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(projectResponseGetOne.Project);
    }

    [HttpGet("{id}/nestedData")]
    public async Task<IActionResult> GetProjectNestedDataById(long id)
    {
        var responseProject = await _projectService.GetProjectNestedDataById(id);
        if (responseProject.IsSuccess == false) return NotFound(responseProject.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, responseProject.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(responseProject.Data);
    }

    [HttpPost]
    public async Task<ActionResult<RunPipelineOutDTO>> RegisterProject([FromForm] StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return BadRequest();

        var responseProject = await _projectService.CreateProject(projectData, currentUser!);
        if (!responseProject.IsSuccess) return BadRequest(responseProject.Errors);

        var responseSplit = await _iaWebService.Split(projectData.File, responseProject.Data);
        if (!responseSplit.IsSuccess) return BadRequest(responseSplit.Errors);

        await Task.Delay(2000); // TODO dégueulasse mais c'est en cours

        // Get and save files
        var responseGetTrainFile = await _iaWebService.GetSplitTrainFile(responseSplit.Data.id);
        if (!responseGetTrainFile.IsSuccess) return BadRequest(responseGetTrainFile.Errors);
        var responseCreateProjectFileTrain = await _projectService.CreateProjectFile(responseGetTrainFile.Data, responseProject.Data, ProjectFile.EProjectFileType.TRAIN);
        if (!responseCreateProjectFileTrain.IsSuccess) return BadRequest(responseCreateProjectFileTrain.Errors);
        var responseGetTestFile = await _iaWebService.GetSplitTestFile(responseSplit.Data.id);
        if (!responseGetTestFile.IsSuccess) return BadRequest(responseGetTestFile.Errors);
        var responseCreateProjectFileTest = await _projectService.CreateProjectFile(responseGetTestFile.Data, responseProject.Data, ProjectFile.EProjectFileType.TEST);
        if (!responseCreateProjectFileTest.IsSuccess) return BadRequest(responseCreateProjectFileTest.Errors);
        

        var responseIaWeb = await _iaWebService.RunPipeline(responseGetTrainFile.Data, responseGetTestFile.Data, responseProject.Data, projectData.Pipeline);
        if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

        await _processService.CreateProcess(responseIaWeb.Data, projectData.Pipeline, responseProject.Data);
        return Ok(new { responseIaWeb.Data.id, responseIaWeb.Data.socket, responseIaWeb.Data.status, projectId = responseProject.Data.Id });
    }

    [HttpGet("{projectId}/dataSample")]
    public async Task<ActionResult> GetProjectDefaultFileLines(long projectId, [FromQuery]int rowCount = 10)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Project, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        if (rowCount > 50 || rowCount < 1)
        {
          rowCount = 50;
        }
        var responseFile = await _projectService.GetProjectDefaultFileFirstNLine(projectId, rowCount);
        if (responseFile.IsSuccess == false) return NotFound(responseFile.Errors);

        return Ok(responseFile.Data);
    }

    [HttpGet("{projectId}/defaultFile")]
    public async Task<ActionResult> GetProjectDefaultFile(long projectId)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Project, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        var responseFile = await _projectService.GetProjectDefaultFile(projectId);
        if (responseFile.IsSuccess == false) return BadRequest(responseFile.Errors);

        return File(responseFile.Data.Data, responseFile.Data.MimeType);
    }
}
