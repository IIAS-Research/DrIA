using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.Authorization;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModelsController : ApplicationController
{
  private readonly ModelService _modelService;

  public ModelsController(UserManager<User> userManager, IAuthorizationService authorizationService, ModelService modelService) : base(userManager, authorizationService)
  {
    _modelService = modelService;
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetModelById(long id)
  {
    var responseModel = await _modelService.GetModelById(id);
    if (!responseModel.IsSuccess) return NotFound(responseModel.Errors);

    var authorizationResult = await _authorizationService.AuthorizeAsync(User, responseModel.Data, Operations.Read);
    if (authorizationResult.Succeeded == false) return Unauthorized();

    return Ok(responseModel.Data);
  }

  [HttpGet("{id}/explanation")]
  public async Task<IActionResult> GetModelExplanationById(long id)
  {
    var responseExplanation = await _modelService.GetModeleExplanationById(id);
    if (!responseExplanation.IsSuccess) return NotFound(responseExplanation.Errors);

    var authorizationResult = await _authorizationService.AuthorizeAsync(User, responseExplanation.Data, Operations.Read);
    if (authorizationResult.Succeeded == false) return Unauthorized();

    return Ok(responseExplanation.Data.TextExplanation);
  }
}
