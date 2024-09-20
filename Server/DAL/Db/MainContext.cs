namespace Server.DAL.Db;

using Microsoft.EntityFrameworkCore;
using Server.Models;

public sealed class MainContext : DbContext
{
  public MainContext(DbContextOptions<MainContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
  }

  public DbSet<User> Users { get; set; }
  public DbSet<Project> Projects { get; set; }

}