using Microsoft.AspNetCore.Identity;
using Server.Models;

public static class DatabaseSeed
{
  private static readonly string _default_admin_mail = "dria-admin@chu-reims.fr";
  private static readonly string _default_admin_password = "France3*suce";
  async public static Task Run(UserManager<User> userManager)
  {
    if (await userManager.FindByEmailAsync(_default_admin_mail) == null)
    {
      var admin = new User
      {
        UserName = "Admin",
        FirstName = "Super",
        LastName = "Admin",
        IsAdmin = true,
        Email = _default_admin_mail,
        EmailConfirmed = true
      };
      await userManager.CreateAsync(admin, _default_admin_password);
    }
  }
}
