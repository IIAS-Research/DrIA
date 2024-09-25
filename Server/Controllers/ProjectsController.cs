using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Repositories;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectsRepository _projectRepository;

    public ProjectsController(UserManager<User> userManager, ProjectsRepository projectRepository) : base(userManager)
    {
        _projectRepository = projectRepository;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserProjects()
    {
        if (!IsCurrentUserAuth())
        {
            return BadRequest();
        }
        User currentUser = GetCurrentUser();
        return Ok(_projectRepository.GetProjectByUser());
    }

    // POST: api/projects
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterProject(RegisterProjectInDTO registerProject)
    {

    }

    private record RegisterProjectInDTO
    {

    }
}