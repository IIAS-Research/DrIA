using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Server;
using Server.Services;
using Server.DAL.Db;
using Server.Repositories;
using System.Text.Json;
using Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Server.Authorization.Handlers;

public partial class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    RunBeforeStart(builder);
    RegisterServices(builder);

    if (builder.Environment.IsDevelopment())
    {
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();
    }

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      var services = scope.ServiceProvider;
      var context = services.GetRequiredService<MainDbContext>();
      context.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    // app.UseHttpsRedirection();
    app.UseCors(app.Environment.EnvironmentName);
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers().RequireAuthorization();
    app.Run();
  }

  private static void RunBeforeStart(WebApplicationBuilder builder)
  {
    Configuration.EnsureSecretsAreOk(builder.Configuration);
    FileStorageService.EnsureStorageDirectoryAreCreated(builder.Configuration);
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
  }

  private static void RegisterServices(WebApplicationBuilder builder)
  {
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
      options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // Ensures camelCase serialization for JSON
    });
    builder.Services.AddDbContext<MainDbContext>(options =>
        options.UseNpgsql(builder.Configuration[Configuration.ConnectionString]));
    builder.Services.AddScoped<UsersRepository>();
    builder.Services.AddScoped<ProjectRepository>();
    builder.Services.AddTransient<FileStorageService>();
    builder.Services.AddTransient<AuthService>();
    builder.Services.AddTransient<MailerService>();
    builder.Services.AddTransient<ProjectService>();
    builder.Services.AddTransient<UserService>();
    builder.Services.AddSingleton<IAWebSocketService>();

    builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "isAdmin" && c.Value == "true")
        ));

    builder.Services.AddSingleton<IAuthorizationHandler, ProjectAuthorizationCrudHandler>();

    builder.Services.AddIdentityCore<User>()
                .AddEntityFrameworkStores<MainDbContext>()
                .AddDefaultTokenProviders();

    builder.Services.AddAuthentication(options =>
        {
          options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
          options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
          options.TokenValidationParameters = AuthService.GetAuthServiceTokenValidationParameters(builder.Configuration);
        });
    builder.Services.AddAuthorization();
    builder.Services.AddCors(options =>
        {
          options.AddPolicy("Production", builder =>
              {
                // TODO
              });
          options.AddPolicy("Development", builder =>
              {
                builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
              });
        });
  }
}
