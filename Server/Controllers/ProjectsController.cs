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

    [HttpPost]
    public async Task<ActionResult<RunPipelineOutDTO>> RegisterProject([FromForm] StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return BadRequest();

        var responseProject = await _projectService.CreateProject(projectData, currentUser!);
        if (!responseProject.IsSuccess) return BadRequest(responseProject.Errors);

        // TODO une fois que l'API est prête, route pour avoir le double fichier, et renvoyer le fichier de train à la runPipeline
        // donc sortir le formatCsv du RunPipeline pour le mettre dans lka nouvelle route une fois prête

        var responseIaWeb = await _iaWebService.RunPipeline(projectData.File, responseProject.Data, projectData.Pipeline);
        if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

        await _processService.CreateProcess(responseIaWeb.Data, projectData.Pipeline, responseProject.Data);

        return Ok(responseIaWeb.Data);
    }

    [HttpGet("{id}/dataSample")]
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
}
