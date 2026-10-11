using System.Text.Json;
using System.Text.Json.Nodes;

namespace Server.Services;

/// <summary>Prepare canonical IAML studies for DrIA's compatibility and execution requirements.</summary>
public static class IamlStudyCompatibility
{
  private const int MaxWorkers = 4;

  public static void PrepareStudy(JsonObject study)
  {
    if (study["schema_version"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 1)
      return;

    RepairDefaultGaussianProcessAlpha(study);
    if (study["options"] == null) study["options"] = new JsonObject();
    if (study["options"] is not JsonObject options)
      throw new ArgumentException("Les options de l'étude doivent être un objet JSON.");
    // Parallel study execution is a server policy, including for imported studies
    // and restarting configurations saved before this policy was introduced.
    options["max_workers"] = MaxWorkers;
  }

  public static int RepairDefaultGaussianProcessAlpha(JsonObject study)
  {
    if (study["schema_version"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 1)
      return 0;
    return RepairRecipe(study["pipeline"]);
  }

  // Only the declared study and configuration/base envelopes are visited. Datasets,
  // descriptions, catalogue entries and unrelated metadata are never traversed.
  public static JsonElement PreparePayload(JsonElement payload, bool includeBase = false)
  {
    var node = JsonNode.Parse(payload.GetRawText());
    if (node is JsonObject root)
    {
      PrepareStudy(root);
      if (root["configuration"] is JsonObject configuration) PrepareStudy(configuration);
      if (includeBase && root["base"] is JsonObject baseline) PrepareStudy(baseline);
    }
    return JsonSerializer.SerializeToElement(node);
  }

  private static int RepairRecipe(JsonNode? node)
  {
    if (node is not JsonObject recipe) return 0;
    var changed = 0;
    if (IsString(recipe["type"], "use") && IsString(recipe["component"], "ActGaussianProcessRegressor") &&
        recipe["params"] is JsonObject parameters && parameters["alpha"] is JsonObject alpha &&
        IsString(alpha["type"], "parameter") && IsNumber(alpha["value"], 1e-14) &&
        alpha["domain"] is JsonArray { Count: 2 } domain && IsNumber(domain[0], 1e-8) && IsNumber(domain[1], 1) &&
        IsFalse(alpha["fixed"]) && IsFalse(alpha["explicit_domain"]) &&
        alpha.ContainsKey("categorical") && alpha["categorical"] is null &&
        alpha["optimizable"] is JsonValue optimizable && optimizable.TryGetValue<bool>(out var searchable) && searchable &&
        alpha.ContainsKey("retained_initial") && alpha["retained_initial"] is null)
    {
      alpha["value"] = 1e-8;
      changed++;
    }

    if (recipe["children"] is JsonArray children)
      foreach (var child in children) changed += RepairRecipe(child);
    changed += RepairRecipe(recipe["recipe"]);
    if (recipe["members"] is JsonArray members)
      foreach (var member in members)
        if (member is JsonObject entry) changed += RepairRecipe(entry["recipe"]);
    return changed;
  }

  private static bool IsString(JsonNode? node, string expected) =>
    node is JsonValue value && value.TryGetValue<string>(out var actual) && actual == expected;

  private static bool IsFalse(JsonNode? node) =>
    node is JsonValue value && value.TryGetValue<bool>(out var actual) && !actual;

  private static bool IsNumber(JsonNode? node, double expected)
  {
    if (node is not JsonValue value) return false;
    if (value.TryGetValue<double>(out var actual)) return actual == expected;
    return value.TryGetValue<int>(out var integer) && integer == expected;
  }
}
