using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Server;
using Server.Services;
using Server.DAL.Db;

public partial class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    RunBeforeStart(builder);
    RegisterServices(builder);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      var services = scope.ServiceProvider;
      var context = services.GetRequiredService<MainContext>();
      context.Database.Migrate();
    }


    app.UseHttpsRedirection();
    app.UseCors(Configuration.GetCorsModeByEnv(builder.Configuration));
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
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
    builder.Services.AddControllers();
    builder.Services.AddDbContext<MainContext>(options =>
        options.UseNpgsql(builder.Configuration[Configuration.DbCredentials]));
    builder.Services.AddTransient<FileStorageService>();
    builder.Services.AddTransient<AuthService>();
    builder.Services.AddTransient<MailerService>();
    builder.Services.AddAuthentication(options =>
        {
          options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
          options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
            {
              options.TokenValidationParameters = AuthService.GetAuthServiceTokenValidationParameters(builder.Configuration);
            });

    builder.Services.AddAuthorization();
    builder.Services.AddCors(options =>
        {
          options.AddPolicy("Prod", builder =>
              {
                // TODO
              });
          options.AddPolicy("Dev", builder =>
              {
                builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
              });
        });
  }

}