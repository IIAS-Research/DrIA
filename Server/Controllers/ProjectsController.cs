using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.DTOs;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectService _projectService;

    public ProjectsController(UserManager<User> userManager, IAuthorizationService authorizationService, ProjectService projectRepository) : base(userManager, authorizationService)
    {
        _projectService = projectRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserProjects()
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null)
        {
            return BadRequest();
        }
        var response = await _projectService.GetUserProjects(currentUser);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterProject(StartProjectInDTO registerProject)
    {
        await _projectService.CreateProject(registerProject);
        // _remoteAiService. ??
        return Ok();
    }

}
