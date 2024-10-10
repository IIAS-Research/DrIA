using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.Authorization;
using static Server.Services.ProjectService;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModelsController : ApplicationController
{
  private readonly ModelService _modelService;
  private readonly IaWebService _iaWebService;
  private readonly ProjectService _projectService;

  public ModelsController(UserManager<User> userManager, IAuthorizationService authorizationService, ModelService modelService, IaWebService iaWebService, ProjectService projectService) : base(userManager, authorizationService)
  {
    _modelService = modelService;
    _iaWebService = iaWebService;
    _projectService = projectService;
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

  [HttpPost("{modelId}/explanation")]
  public async Task<ActionResult<ProjectFileFormated>> PredictValueUsingAiModel(long modelId, [FromBody] ProjectFileFormated dataTable)
  {
    // Get model file of the current model
    var getModelFileResponse = await _modelService.GetModelFileById(modelId);
    if (!getModelFileResponse.IsSuccess) return BadRequest(getModelFileResponse.Errors);

    // Send request to web service to predict
    var serviceResponse = await _iaWebService.PredictValueUsingAiModel(getModelFileResponse.Data!, dataTable);
    if (!serviceResponse.IsSuccess) return BadRequest(getModelFileResponse.Errors);

    return Ok(serviceResponse.Data);


    // Return the response
  }
}
