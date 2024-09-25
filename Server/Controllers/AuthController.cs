using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.DTOs;
using Server.Models;
using Server.Services;

namespace Server.Controllers;
[Route("api/[controller]")]
[ApiController]
public class AuthController : ApplicationController
{
  private readonly AuthService _authService;

  public AuthController(UserManager<User> userManager, AuthService authService) : base(userManager)
  {
    _authService = authService;
  }

  // POST: api/register
  [HttpPost("register")]
  [AllowAnonymous]
  public async Task<IActionResult> RegisterUser(RegisterUserDTO registerUserDTO)
  {
    var result = await _authService.RegisterUser(registerUserDTO);

    if (result)
    {
      return CreatedAtAction(nameof(RegisterUser), new { email = registerUserDTO.Email }, registerUserDTO);
    }

    return BadRequest("User registration failed.");
  }

  // POST: api/login
  [HttpPost("login")]
  [AllowAnonymous]
  public async Task<ActionResult<LoginResponse>> Login(PostLoginInDTO loginInDTO)
  {
    var authServiceResponse = await _authService.Login(loginInDTO.Email, loginInDTO.Password);
    if (authServiceResponse.IsLoggedIn)
    {
      return Ok(authServiceResponse);
    }
    return Unauthorized();
  }

  // GET: api/refresh-token
  [HttpPost("refresh-access-token")]
  public async Task<ActionResult> RefreshToken(string accessToken)
  {
    var authServiceResponse = await _authService.RefreshAccessToken(accessToken);
    if (authServiceResponse.IsLoggedIn)
    {
      return Ok(authServiceResponse);
    }
    return BadRequest();
  }


}