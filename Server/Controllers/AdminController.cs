using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
  private readonly IaPullingModelService _iaPullingModelService;
  private readonly IaPullingExplainService _iaPullingExplainService;
  public AdminController(IaPullingModelService iaPullingModelService, IaPullingExplainService iaPullingExplainService)
  {
    _iaPullingModelService = iaPullingModelService;
    _iaPullingExplainService = iaPullingExplainService;
  }

  [HttpGet]
  [Route("PullingData")]
  public IActionResult GetSingletonsPullingData()
  {
    var connectionsModel = _iaPullingModelService.GetAllConnections();
    var connectionsExplain = _iaPullingExplainService.GetAllConnections();
    return Ok(new {connectionsModel, connectionsExplain});
  }

  [HttpDelete]
  [Route("DeleteConnection/{jobServiceId}")]
  public IActionResult DeletePullingConnection(string jobServiceId)
  {
    _iaPullingModelService.DeleteIaConnection(jobServiceId);
    _iaPullingExplainService.DeleteIaConnection(jobServiceId);
    return Ok();
  }
}
