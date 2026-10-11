using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.DAL.Db;
using Server.Models;

namespace Server.Tests;

public class DatabaseSeedTests : IDisposable
{
  private readonly ServiceProvider _services;
  private readonly IServiceScope _scope;
  private readonly UserManager<User> _users;

  public DatabaseSeedTests()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDbContext<MainDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
    services.AddIdentityCore<User>().AddEntityFrameworkStores<MainDbContext>();
    _services = services.BuildServiceProvider();
    _scope = _services.CreateScope();
    _users = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
  }

  [Fact]
  public async Task ProductionDoesNotCreateADevelopmentAccount()
  {
    await DatabaseSeed.Run(_users, Configuration(), isDevelopment: false);

    Assert.Empty(_users.Users);
  }

  [Fact]
  public async Task DevelopmentCreatesTheDocumentedDefaultAdministrator()
  {
    await DatabaseSeed.Run(_users, Configuration(), isDevelopment: true);

    var user = await _users.FindByEmailAsync("admin@dria.local");
    Assert.NotNull(user);
    Assert.True(user.IsAdmin);
    Assert.True(await _users.CheckPasswordAsync(user, "DrIA-Dev123!"));
  }

  [Fact]
  public async Task ProductionCreatesTheConfiguredAdministratorOnceWithoutResettingItsPassword()
  {
    await DatabaseSeed.Run(_users, Configuration(" admin@example.org ", "First-Password123!"), isDevelopment: false);
    await DatabaseSeed.Run(_users, Configuration("admin@example.org", "Replacement-Password123!"), isDevelopment: false);

    var user = await _users.FindByEmailAsync("admin@example.org");
    Assert.NotNull(user);
    Assert.Single(_users.Users);
    Assert.True(user.IsAdmin);
    Assert.True(user.EmailConfirmed);
    Assert.True(await _users.CheckPasswordAsync(user, "First-Password123!"));
    Assert.False(await _users.CheckPasswordAsync(user, "Replacement-Password123!"));
  }

  [Theory]
  [InlineData("admin@example.org", null)]
  [InlineData(null, "First-Password123!")]
  public async Task RejectsIncompleteBootstrapCredentials(string? email, string? password)
  {
    await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseSeed.Run(_users, Configuration(email, password), isDevelopment: false));

    Assert.Empty(_users.Users);
  }

  [Fact]
  public async Task DoesNotPromoteAnExistingRegularUser()
  {
    var existing = new User
    {
      UserName = "user@example.org",
      Email = "user@example.org",
      FirstName = "Test",
      LastName = "User",
      IsAdmin = false
    };
    Assert.True((await _users.CreateAsync(existing, "First-Password123!")).Succeeded);

    await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseSeed.Run(_users, Configuration(existing.Email, "Replacement-Password123!"), isDevelopment: false));

    Assert.False((await _users.FindByEmailAsync(existing.Email))!.IsAdmin);
    Assert.Single(_users.Users);
  }

  private static IConfiguration Configuration(string? email = null, string? password = null) => new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["BootstrapAdmin:Email"] = email,
      ["BootstrapAdmin:Password"] = password
    }).Build();

  public void Dispose()
  {
    _scope.Dispose();
    _services.Dispose();
  }
}
