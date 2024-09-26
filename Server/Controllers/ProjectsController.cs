using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.DTOs;
using Server.Authorization;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectService _projectService;

    public ProjectsController(UserManager<User> userManager, IAuthorizationService authorizationService, ProjectService projectRepository) : base(userManager, authorizationService)
    {
        _projectService = projectRepository;
        _authorizationService = authorizationService;
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

    // [HttpPost]
    // public async Task<IActionResult> RegisterProject([FromForm] StartProjectInDTO registerProject)
    // {
    //     await _projectService.CreateProject(registerProject);
    //     // _remoteAiService. ??
    //     // https://github.com/dotnet/aspnetcore/issues/4868
    //     return Ok();
    // }

}

