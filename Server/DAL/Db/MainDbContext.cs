namespace Server.DAL.Db;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Server.Models;

public class MainDbContext : IdentityDbContext<User, Role, long>
{
  public MainDbContext(DbContextOptions<MainDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
  }

  public DbSet<Project> Projects { get; set; }
  public DbSet<Process> Processes { get; set; }
  public DbSet<Model> Models { get; set; }
  public DbSet<ModelFile> ModelFiles { get; set; }
  public DbSet<UserFile> UserFiles { get; set; }
}
