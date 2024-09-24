namespace Server;

public static class Configuration
{
  public static readonly string AppName = "Dr_IA";
  public static readonly string ConnectionString = $"{AppName}:ConnectionString";
  public static readonly string JwtPrivateKey = $"{AppName}:JwtPrivateKey";
  public static readonly string PathMainStorage = $"{AppName}:PathMainStorage";

  public static void EnsureSecretsAreOk(IConfiguration config)
  {
    var secretsToCheck = new string[]
    {
            ConnectionString,
            JwtPrivateKey,
            PathMainStorage,
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

