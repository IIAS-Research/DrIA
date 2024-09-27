using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProcessController : ApplicationController
{
  private readonly ProjectService _projectService;
  private readonly ProcessService _processService;
  private readonly IaWebService _iaWebService;
  public ProcessController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService, ProjectService projectRepository, ProcessService processService) : base(userManager, authorizationService)
  {
    _projectService = projectRepository;
    _processService = processService;
    _iaWebService = iaWebService;
  }


}