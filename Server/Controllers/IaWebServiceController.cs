using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class IaWebServiceController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService) : ApplicationController(userManager, authorizationService)
{
  private readonly IaWebService _iaWebService = iaWebService;

  [HttpGet("pipeline")]
  public async Task<IActionResult> GeneratePipeline()
  {
    var response = await _iaWebService.GeneratePipeline();
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("health")]
  public async Task<IActionResult> Health()
  {
    var response = await _iaWebService.GetHealth();
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpGet("catalogue")]
  [HttpGet("pipeline/catalogue")]
  public async Task<IActionResult> Catalogue()
  {
    var response = await _iaWebService.GetCatalogue();
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("validate")]
  [HttpPost("pipeline/validate")]
  public async Task<IActionResult> Validate([FromBody] JsonElement configuration)
  {
    var response = await _iaWebService.ValidateConfiguration(configuration);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("edit")]
  [HttpPost("pipeline/edit")]
  public async Task<IActionResult> Edit([FromBody] JsonElement request)
  {
    var response = await _iaWebService.EditConfiguration(request);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("diff")]
  [HttpPost("pipeline/diff")]
  public async Task<IActionResult> Diff([FromBody] JsonElement request)
  {
    var response = await _iaWebService.DiffConfiguration(request);
    return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
  }

  [HttpPost("export")]
  [HttpPost("pipeline/export")]
  public async Task<IActionResult> Export([FromBody] JsonElement request, [FromQuery] string format = "json")
  {
    if (format is not ("json" or "python")) return BadRequest(new { error = "The format must be json or python." });
    var response = await _iaWebService.ExportConfiguration(request, format);
    if (!response.IsSuccess) return ServiceFailure(response);
    var file = response.Data;
    await using var stream = file.OpenReadStream();
    using var memory = new MemoryStream();
    await stream.CopyToAsync(memory);
    return File(memory.ToArray(), file.ContentType, file.FileName);
  }

  [HttpPost("statistics")]
  [RequestSizeLimit(104857600)]
  [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
  public async Task<IActionResult> Statistics([FromQuery] bool plots = true)
  {
    if (Request.HasFormContentType)
    {
      var response = await _iaWebService.GetStatistics(await Request.ReadFormAsync(), plots);
      return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
    }
    if (Request.ContentType?.Split(';')[0].Trim() == "application/json")
    {
      try
      {
        using var document = await JsonDocument.ParseAsync(Request.Body);
        var response = await _iaWebService.GetStatistics(document.RootElement, plots);
        return response.IsSuccess ? Ok(response.Data) : ServiceFailure(response);
      }
      catch (JsonException) { return BadRequest(new { error = "The JSON body is invalid." }); }
    }
    return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = "Use multipart/form-data or application/json." });
  }
}
