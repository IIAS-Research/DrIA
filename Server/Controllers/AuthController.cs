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

  public AuthController(UserManager<User> userManager, AuthService authService, IAuthorizationService authorizationService) : base(userManager, authorizationService)
  {
    _authService = authService;
  }

  // POST: api/register; TODO: Policy admin only to register users
  [HttpPost("register")]
  [AllowAnonymous]
  public async Task<ActionResult<User>> RegisterUser(RegisterUserDTO registerUserDTO)
  {
    var user = await _authService.RegisterUser(registerUserDTO);

    if (user != null)
    {
      return CreatedAtAction(nameof(RegisterUser), new { email = registerUserDTO.Email }, user);
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
  [HttpGet("refresh-access-token")]
  public async Task<ActionResult<string>> RefreshToken()
  {
    // Retrieve the authorization header from the request
    var authHeader = Request.Headers.Authorization.ToString();

    // Check if the Authorization header contains a Bearer token
    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
    {
      // Extract the token from the Bearer string
      var accessToken = authHeader["Bearer ".Length..].Trim();

      var authServiceResponse = await _authService.RefreshAccessToken(accessToken);

      if (!authServiceResponse.IsLoggedIn) return BadRequest("Token expired or invalid.");
      return Ok(authServiceResponse.AccessToken);
    }
    return BadRequest("Authorization Bearer invalid.");
  }


}