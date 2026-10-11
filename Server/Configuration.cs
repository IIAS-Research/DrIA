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
      if (string.IsNullOrWhiteSpace(config[secret]))
      {
        throw new Exception($"Secret '{secret}' is not defined!");
      }
    }
    if (System.Text.Encoding.UTF8.GetByteCount(config[JwtPrivateKey]!) < 32)
    {
      throw new InvalidOperationException("JwtPrivateKey must contain at least 32 bytes.");
    }
  }
}

