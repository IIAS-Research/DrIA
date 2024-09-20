using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
  private readonly AuthService _authService;

  public AuthController(AuthService authService)
  {
    _authService = authService;
  }

  // GET: api/login
  [HttpPost("login")]
  public async Task<ActionResult> Login(PostLoginInDTO loginEndPointParams)
  {

    var authServiceResponse = await _authService.Login(loginEndPointParams.Email, loginEndPointParams.Password);
    if (authServiceResponse.IsLogedIn)
    {
      return Ok(authServiceResponse);
    }
    return BadRequest();
  }

  // GET: api/refresh-token
  [HttpPost("refresh-token")]
  public async Task<ActionResult> RefreshToken(PostRefreshTokenInDTO refreshTokenEndPointParams)
  {
    var authServiceResponse = await _authService.RefreshToken(refreshTokenEndPointParams.Jwt, refreshTokenEndPointParams.RefreshToken);
    if (authServiceResponse.IsLogedIn)
    {
      return Ok(authServiceResponse);
    }
    return BadRequest();
  }


}

// DTOS
public record PostLoginInDTO
{
  public required string Email;
  public required string Password;
}
public record PostRefreshTokenInDTO
{
  public required string Jwt;
  public required string RefreshToken;
}