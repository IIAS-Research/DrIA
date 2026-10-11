namespace Server;

public static class Configuration
{
  public static readonly string ConnectionString = "ConnectionString";
  public static readonly string JwtPrivateKey = "JwtPrivateKey";
  public static readonly string PathMainStorage = "PathMainStorage";
  public static readonly string IamlBaseUrl = "Iaml:BaseUrl";

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

