using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.DTOs;
using Server.Authorization;
using System.Text.Json;

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
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return Unauthorized();
        ProjectService.ProjectResponseGetAll projectResponseGetAll = await _projectService.GetProjects();
        return Ok(projectResponseGetAll.Projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(long id)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return Unauthorized();
        ProjectService.ProjectResponseGetOne projectResponseGetOne = await _projectService.GetProjectById(id);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Project, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();
        return Ok(projectResponseGetOne.Project);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterProject([FromBody]StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return BadRequest();

        var responseProject = await _projectService.CreateProject(projectData, currentUser);
        if (!responseProject.IsSuccess) return BadRequest(responseProject.Errors);

        var pipelineSerialized = JsonSerializer.Deserialize<Process.ProcessPipeline>(projectData.Pipeline)!;
        
        var responseIaWeb = await _iaWebService.RunPipeline(projectData.File, responseProject.Data, pipelineSerialized);
        if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

        var process = await _processService.CreateProcess(responseIaWeb.Data, pipelineSerialized, responseProject.Data);

        return Ok(process);
    }
}
