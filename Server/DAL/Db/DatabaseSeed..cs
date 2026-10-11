using Microsoft.AspNetCore.Identity;
using Server.Models;

public static class DatabaseSeed
{
  private static readonly string _default_admin_mail = "admin@dria.local";
  private static readonly string _default_admin_password = "DrIA-Dev123!";
  public static async Task Run(UserManager<User> userManager)
  {
    if (await userManager.FindByEmailAsync(_default_admin_mail) == null)
    {
      var admin = new User
      {
        UserName = _default_admin_mail,
        FirstName = "Admin",
        LastName = "Développement",
        IsAdmin = true,
        Email = _default_admin_mail,
        EmailConfirmed = true
      };
      var result = await userManager.CreateAsync(admin, _default_admin_password);
      if (!result.Succeeded)
      {
        throw new InvalidOperationException($"Failed to create the development administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
      }
    }
  }
}
