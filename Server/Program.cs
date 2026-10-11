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
using Server.Hubs;
using SwaggerThemes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

public partial class Program
{
  public static async Task Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);
    const long maxUploadSize = 100L * 1024L * 1024L;
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxUploadSize);
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        options.MultipartBodyLengthLimit = maxUploadSize);

    RunBeforeStart(builder);
    RegisterServices(builder);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      var services = scope.ServiceProvider;
      var dbContext = services.GetRequiredService<MainDbContext>();
      dbContext.Database.Migrate();
      if (!app.Environment.IsProduction())
      {
        var userManager = services.GetRequiredService<UserManager<User>>();
        await DatabaseSeed.Run(userManager);
      }
    }

    app.UseSwagger(c =>
    {
      c.RouteTemplate = "api/swagger/{documentname}/swagger.json";
    });
    app.UseSwaggerThemes(Theme.UniversalDark);
    app.UseSwaggerUI(c =>
    {
      c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "API Swagger");
      c.RoutePrefix = "api/swagger";
    });

    // app.UseHttpsRedirection();
    app.UseCors(app.Environment.EnvironmentName);
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
      ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers().RequireAuthorization();
    app.MapHub<IaGenerateModelHub>("/api/ws/generateModel").RequireAuthorization();
    app.MapHub<IaExplainModelHub>("/api/ws/explainModel").RequireAuthorization();
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
      options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // Circle dependencies
      options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); // Make all app enum sent as string in JSON instead of int
    });
    builder.Services.AddDbContext<MainDbContext>(options =>
        options.UseNpgsql(builder.Configuration[Configuration.ConnectionString]));
    builder.Services.AddScoped<UsersRepository>();
    builder.Services.AddScoped<ProjectRepository>();
    builder.Services.AddScoped<ProcessRepository>();
    builder.Services.AddScoped<ModelRepository>();
    builder.Services.AddTransient<FileStorageService>();
    builder.Services.AddTransient<AuthService>();
    builder.Services.AddTransient<MailerService>();
    builder.Services.AddTransient<ProjectService>();
    builder.Services.AddTransient<UserService>();
    builder.Services.AddTransient<IaWebService>();
    builder.Services.AddTransient<ProcessService>();
    builder.Services.AddTransient<ModelService>();
    builder.Services.AddSingleton<IaPullingModelService>();
    builder.Services.AddSingleton<IaPullingExplainService>();
    builder.Services.AddHostedService<IaPullingRecoveryService>();

    builder.Services.AddSignalR();

    builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "isAdmin" && c.Value == "true")
        ));

    builder.Services.AddSingleton<IAuthorizationHandler, ProjectAuthorizationCrudHandler>();
    builder.Services.AddSingleton<IAuthorizationHandler, ModelAuthorizationCrudHandler>();
    builder.Services.AddSingleton<IAuthorizationHandler, UserAuthorizationCrudHandler>();

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
          options.Events = new JwtBearerEvents
          {
            OnMessageReceived = context =>
            {
              var accessToken = context.Request.Query["access_token"];
              if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/api/ws"))
              {
                context.Token = accessToken;
              }
              return Task.CompletedTask;
            }
          };
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
                builder.WithOrigins("http://localhost:9000") // Must be set explicitly for cors to work with Web Sockets
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
              });
        });
  }
}
