using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.DTOs;
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
    public async Task<IActionResult> RegisterProject([FromBody]StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return BadRequest();

        var responseProject = await _projectService.CreateProject(projectData, currentUser);
        if (!responseProject.IsSuccess) return BadRequest(responseProject.Errors);
        
        var responseIaWeb = await _iaWebService.RunPipeline(projectData.File, responseProject.Data!, JsonSerializer.Serialize(projectData.Pipeline));
        if (!responseIaWeb.IsSuccess) return BadRequest(responseIaWeb.Errors);

        // connexion ws
        //await _processService.CreateProcess();

        return Ok();
    }
}
