using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.DAL.Db;
using Server.Repositories;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectsRepository _projectRepository;

    public ProjectsController(ProjectsRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserProjects()
    {
        if (User.Identity.)
        return Ok(_projectRepository.GetProjectByUser(User.FindFirstValue(Claim)));
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