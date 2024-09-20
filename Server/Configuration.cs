namespace Server;

public static class Configuration
{
  public static readonly string AppName = "Dr.IA";
  public static readonly string DbCredentials = $"{AppName}DbCredentials";
  public static readonly string JwtPrivateKey = $"{AppName}JwtPrivateKey";
  public static readonly string PathMainStorage = $"{AppName}PathMainStorage";
  public static readonly string CorsPolicy = $"{AppName}CorsPolicy";

  public static string GetCorsModeByEnv(IConfiguration config)
  {
    return config[CorsPolicy] ?? "Prod";
  }

  public static void EnsureSecretsAreOk(IConfiguration config)
  {
    var secretsToCheck = new string[]
    {
            DbCredentials,
            JwtPrivateKey,
            PathMainStorage,
            CorsPolicy
    };
    foreach (string secret in secretsToCheck)
    {
      if (config[secret] is null)
      {
        throw new Exception($"Secret '{secret}' is not defined!");
      }
    }
  }
}
