using System.Text.Json;
using System.Text.Json.Nodes;
using Server.Services;

namespace Server.Tests;

public class IamlStudyCompatibilityTests
{
  [Theory]
  [InlineData(null)]
  [InlineData(1)]
  [InlineData(32)]
  public void PrepareStudyEnforcesFourWorkers(int? requestedWorkers)
  {
    var study = Study();
    if (requestedWorkers.HasValue)
      study["options"] = new JsonObject { ["max_workers"] = requestedWorkers.Value, ["seed"] = 42 };

    IamlStudyCompatibility.PrepareStudy(study);

    Assert.Equal(4, study["options"]!["max_workers"]!.GetValue<int>());
    if (requestedWorkers.HasValue) Assert.Equal(42, study["options"]!["seed"]!.GetValue<int>());
  }

  [Fact]
  public void RepairsTheInvalidDefaultInsideNestedRecipes()
  {
    var study = Study(new JsonObject
    {
      ["type"] = "sequence",
      ["children"] = new JsonArray
      {
        GaussianProcess(),
        new JsonObject { ["type"] = "wrapper", ["recipe"] = GaussianProcess() },
        new JsonObject
        {
          ["type"] = "choice",
          ["members"] = new JsonArray(new JsonObject { ["name"] = "candidate", ["recipe"] = GaussianProcess() })
        }
      }
    });

    var repairs = IamlStudyCompatibility.RepairDefaultGaussianProcessAlpha(study);
    var children = study["pipeline"]!["children"]!.AsArray();

    Assert.Equal(3, repairs);
    Assert.Equal(1e-8, AlphaValue(children[0]!));
    Assert.Equal(1e-8, AlphaValue(children[1]!["recipe"]!));
    Assert.Equal(1e-8, AlphaValue(children[2]!["members"]![0]!["recipe"]!));
  }

  [Theory]
  [InlineData("fixed", "true")]
  [InlineData("explicit_domain", "true")]
  [InlineData("value", "0.001")]
  [InlineData("domain", "[1e-16, 1]")]
  [InlineData("retained_initial", "1e-14")]
  public void PreservesUserConfiguredGaussianProcessParameters(string property, string json)
  {
    var recipe = GaussianProcess();
    recipe["params"]!["alpha"]![property] = JsonNode.Parse(json);
    var study = Study(recipe);
    var before = study.ToJsonString();

    Assert.Equal(0, IamlStudyCompatibility.RepairDefaultGaussianProcessAlpha(study));
    Assert.Equal(before, study.ToJsonString());
  }

  [Fact]
  public void PreparePayloadDoesNotModifyDatasetsOrUndeclaredBaselines()
  {
    var payload = new JsonObject
    {
      ["configuration"] = Study(GaussianProcess()),
      ["base"] = Study(GaussianProcess()),
      ["dataset"] = Study(GaussianProcess())
    };

    var prepared = IamlStudyCompatibility.PreparePayload(JsonSerializer.SerializeToElement(payload));

    Assert.Equal(4, prepared.GetProperty("configuration").GetProperty("options").GetProperty("max_workers").GetInt32());
    Assert.Equal(1e-8, prepared.GetProperty("configuration").GetProperty("pipeline").GetProperty("params").GetProperty("alpha").GetProperty("value").GetDouble());
    Assert.Equal(payload["dataset"]!.ToJsonString(), JsonNode.Parse(prepared.GetProperty("dataset").GetRawText())!.ToJsonString());
    Assert.Equal(payload["base"]!.ToJsonString(), JsonNode.Parse(prepared.GetProperty("base").GetRawText())!.ToJsonString());

    var withBaseline = IamlStudyCompatibility.PreparePayload(JsonSerializer.SerializeToElement(payload), includeBase: true);
    Assert.Equal(4, withBaseline.GetProperty("base").GetProperty("options").GetProperty("max_workers").GetInt32());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(2)]
  public void DoesNotRewriteUnsupportedSchemaVersions(int schemaVersion)
  {
    var study = Study(GaussianProcess());
    study["schema_version"] = schemaVersion;
    var before = study.ToJsonString();

    IamlStudyCompatibility.PrepareStudy(study);

    Assert.Equal(before, study.ToJsonString());
  }

  private static JsonObject Study(JsonObject? recipe = null) => new()
  {
    ["schema_version"] = 1,
    ["pipeline"] = recipe ?? new JsonObject { ["type"] = "use", ["component"] = "ActLinearRegression" }
  };

  private static JsonObject GaussianProcess() => JsonNode.Parse("""
    {
      "type": "use",
      "component": "ActGaussianProcessRegressor",
      "params": {
        "alpha": {
          "type": "parameter",
          "value": 1e-14,
          "domain": [1e-8, 1],
          "fixed": false,
          "explicit_domain": false,
          "categorical": null,
          "optimizable": true,
          "retained_initial": null
        }
      }
    }
    """)!.AsObject();

  private static double AlphaValue(JsonNode recipe) => recipe["params"]!["alpha"]!["value"]!.GetValue<double>();
}
