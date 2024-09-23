// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using Server.DAL.Db;
// using Server.Models;

// namespace Server.Controllers;

// [ApiController]
// public class AdminController : ControllerBase
// {
//   private readonly MainDbContext _MainDbContext;
//   public AdminController(MainDbContext MainDbContext)
//   {
//     _MainDbContext = MainDbContext;
//   }

//   // POST: api/Users
//   [HttpPost]
//   public async Task<ActionResult> PostUser(User user)
//   {
//     _MainDbContext.Users.Add(user);
//     try
//     {
//       await _MainDbContext.SaveChangesAsync();
//     }
//     catch (DbUpdateException)
//     {
//       if (UserExists(user.Id))
//       {
//         return Conflict();
//       }
//       else
//       {
//         throw;
//       }
//     }

//     return Ok();
//   }
// }
