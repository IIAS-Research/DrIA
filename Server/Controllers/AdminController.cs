using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.Models;

namespace Server.Controllers;

[ApiController]
public class AdminController : ControllerBase
{
  private readonly MainContext _mainContext;
  public AdminController(MainContext mainContext)
  {
    _mainContext = mainContext;
  }

  // POST: api/Users
  [HttpPost]
  public async Task<ActionResult> PostUser(User user)
  {
    _mainContext.Users.Add(user);
    try
    {
      await _mainContext.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
      if (UserExists(user.Id))
      {
        return Conflict();
      }
      else
      {
        throw;
      }
    }

    return Ok();
  }
}
