using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.DTOs;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IaWebServiceController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService) : ApplicationController(userManager, authorizationService)
{
    private readonly IaWebService _iaWebService = iaWebService;

    [HttpGet("pipeline")]
    public async Task<ActionResult<GeneratePipelineWebServiceJsonDTO>> GeneratePipeline()
    {
        var response = await _iaWebService.GeneratePipeline();
        if (response.IsSuccess == false) return NotFound();
        return Ok(response.Data.Pipeline);
    }
}