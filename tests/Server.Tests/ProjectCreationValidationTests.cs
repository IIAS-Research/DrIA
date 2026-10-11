using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;
using Server.DTOs;
using Server.Services;

namespace Server.Tests;

public class ProjectCreationValidationTests
{
  [Fact]
  public void AcceptsAValidProjectAndDataset()
  {
    var project = Project("feature,target\n12,no\n18,yes\n");

    Assert.Empty(Validate(project));
    Assert.True(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Theory]
  [InlineData("feature,feature,target\n1,2,no\n")]
  [InlineData("feature,other\n1,no\n")]
  [InlineData("feature,target\n1,\n")]
  [InlineData("feature,target\n1,no,unexpected\n")]
  [InlineData("feature,target\n")]
  public void RejectsInvalidDatasetSchemasAndMissingTargets(string csv)
  {
    var result = ProjectCreationValidation.ValidateFiles(Project(csv));

    Assert.False(result.IsSuccess);
    Assert.Equal(StatusCodes.Status400BadRequest, result.HttpStatusCode);
    Assert.NotEmpty(result.Errors);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void PreventsUsingTheTargetAsAGroupOrExcludedColumn(bool grouping)
  {
    var project = Project("feature,target\n1,no\n2,yes\n");
    project.RowDepedencies = grouping;
    project.RowDepedenciesLinkedString = "[\"target\"]";
    project.DatasetHasIdColumn = !grouping;
    project.DatasetHasIdColumnListString = "[\"target\"]";

    Assert.False(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Fact]
  public void RequiresAtLeastOnePredictiveColumn()
  {
    var project = Project("patient,target\n1,no\n2,yes\n");
    project.DatasetHasIdColumn = true;
    project.DatasetHasIdColumnListString = "patient";

    Assert.False(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Fact]
  public void AcceptsExternalEvaluationWithReorderedColumnsAndDistinctPatients()
  {
    var project = ExternalProject("patient,feature,target\n1,12,no\n", "target,patient,feature\nyes,2,18\n");

    Assert.Empty(Validate(project));
    Assert.True(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Theory]
  [InlineData("01", "1")]
  [InlineData("1.0", "1")]
  [InlineData("true", "1")]
  public void RejectsTheSamePatientAcrossTrainAndTestDespiteNumericEncoding(string trainPatient, string testPatient)
  {
    var project = ExternalProject($"patient,feature,target\n{trainPatient},12,no\n", $"patient,feature,target\n{testPatient},18,yes\n");

    var result = ProjectCreationValidation.ValidateFiles(project);

    Assert.False(result.IsSuccess);
    Assert.Contains("both files", result.Error);
  }

  [Fact]
  public void PreservesDistinctLargeIntegerPatientIdentifiers()
  {
    var project = ExternalProject("patient,feature,target\n9007199254740992,12,no\n", "patient,feature,target\n9007199254740993,18,yes\n");

    Assert.True(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Fact]
  public void RejectsExternalEvaluationWithDifferentColumns()
  {
    var project = ExternalProject("patient,feature,target\n1,12,no\n", "patient,other,target\n2,18,yes\n");

    Assert.False(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Theory]
  [InlineData("quantity", "feature,target\n1,no\n2,yes\n", false)]
  [InlineData("category", "feature,target\n1,1.5\n2,2.5\n", false)]
  [InlineData("quantity", "feature,target\n1,1.5\n2,2.5\n", true)]
  [InlineData("category", "feature,target\n1,no\n2,yes\n", true)]
  public void ChecksThePredictionKindAgainstTheActualTrainingTarget(string requestedKind, string csv, bool expectedSuccess)
  {
    var project = Project(csv);
    project.CreationContextJson = $"{{\"predictionKind\":\"{requestedKind}\"}}";

    var result = ProjectCreationValidation.ValidatePredictionKind(project.File, project);

    Assert.Equal(expectedSuccess, result.IsSuccess);
    if (!expectedSuccess) Assert.Equal(StatusCodes.Status400BadRequest, result.HttpStatusCode);
  }

  [Fact]
  public void RejectsNonFiniteNumericTargets()
  {
    var project = Project("feature,target\n1,NaN\n2,1.5\n");
    project.CreationContextJson = "{\"predictionKind\":\"quantity\"}";

    Assert.False(ProjectCreationValidation.ValidatePredictionKind(project.File, project).IsSuccess);
  }

  [Fact]
  public void SurvivalRequiresADistinctTimeColumnAndAScalarEventValue()
  {
    var project = Project("feature,target,time\n1,yes,12\n");
    project.SurvivalModel = true;
    project.SurvivalModelDatasetTargetTime = "target";
    project.SurvivalModelEventValue = "{\"event\":true}";

    var errors = Validate(project);

    Assert.Contains(errors, error => error.MemberNames.Contains(nameof(project.SurvivalModelDatasetTargetTime)));
    Assert.Contains(errors, error => error.MemberNames.Contains(nameof(project.SurvivalModelEventValue)));

    project.SurvivalModelDatasetTargetTime = "time";
    project.SurvivalModelEventValue = "\"yes\"";
    Assert.Empty(Validate(project));
    Assert.True(ProjectCreationValidation.ValidateFiles(project).IsSuccess);
  }

  [Theory]
  [InlineData("unknown", false)]
  [InlineData("external", false)]
  [InlineData("split", true)]
  public void RejectsInconsistentEvaluationModes(string mode, bool includeTestFile)
  {
    var project = Project("feature,target\n1,no\n");
    project.EvaluationMode = mode;
    if (includeTestFile) project.TestFile = File("feature,target\n2,yes\n");

    Assert.NotEmpty(Validate(project));
  }

  [Theory]
  [InlineData("{\"schema_version\":2}")]
  [InlineData("{\"schema_version\":1.5}")]
  [InlineData("[]")]
  [InlineData("invalid JSON")]
  public void RequiresACanonicalStudyConfiguration(string pipeline)
  {
    var project = Project("feature,target\n1,no\n");
    project.Pipeline = pipeline;

    Assert.NotEmpty(Validate(project));
  }

  private static List<ValidationResult> Validate(StartProjectInDTO project) => project.Validate(new ValidationContext(project)).ToList();

  private static StartProjectInDTO ExternalProject(string train, string test)
  {
    var project = Project(train);
    project.EvaluationMode = "external";
    project.TestFile = File(test);
    project.RowDepedencies = true;
    project.RowDepedenciesLinkedString = "patient";
    return project;
  }

  private static StartProjectInDTO Project(string csv) => new()
  {
    Title = "Test project",
    CsvColumnToPredict = "target",
    RowDepedencies = false,
    DatasetHasIdColumn = false,
    Pipeline = "{\"schema_version\":1}",
    File = File(csv),
    SurvivalModel = false
  };

  private static FormFile File(string csv)
  {
    var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
    return new FormFile(stream, 0, stream.Length, "File", "data.csv");
  }
}
