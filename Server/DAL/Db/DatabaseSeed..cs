using Microsoft.AspNetCore.Identity;
using Server.Models;

public static class DatabaseSeed
{
  private static readonly string _default_admin_mail = "admin@dria.local";
  private static readonly string _default_admin_password = "DrIA-Dev123!";
  public static async Task Run(UserManager<User> userManager, IConfiguration configuration, bool isDevelopment)
  {
    var email = configuration["BootstrapAdmin:Email"]?.Trim();
    var password = configuration["BootstrapAdmin:Password"];
    var configured = !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(password);
    if (configured && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)))
    {
      throw new InvalidOperationException("BootstrapAdmin requires both Email and Password.");
    }
    if (!configured)
    {
      if (!isDevelopment) return;
      email = _default_admin_mail;
      password = _default_admin_password;
    }
    var existing = await userManager.FindByEmailAsync(email!);
    if (existing != null)
    {
      if (!existing.IsAdmin)
      {
        throw new InvalidOperationException("The bootstrap account already exists and is not an administrator.");
      }
      return;
    }
    var admin = new User
    {
      UserName = email,
      FirstName = "Admin",
      LastName = configured ? "DrIA" : "Développement",
      IsAdmin = true,
      Email = email,
      EmailConfirmed = true
    };
    var result = await userManager.CreateAsync(admin, password!);
    if (!result.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create the administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
  }
}
