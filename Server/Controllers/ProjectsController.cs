using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;
using Server.Authorization;
using Server.DTOs;
using System.Text.Json;
using static Server.Models.ProjectFile;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ApplicationController
{
    private readonly ProjectService _projectService;
    private readonly ProcessService _processService;
    private readonly IaWebService _iaWebService;
    private readonly IaPullingModelService _pullingService;

    public ProjectsController(UserManager<User> userManager, IaWebService iaWebService, IAuthorizationService authorizationService, ProjectService projectRepository, ProcessService processService, IaPullingModelService pullingService) : base(userManager, authorizationService)
    {
        _projectService = projectRepository;
        _processService = processService;
        _iaWebService = iaWebService;
        _pullingService = pullingService;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetProjects([FromQuery]int limit, [FromQuery]int offset)
    {
        if (limit == 0 || limit > 30)
        {
          limit = 30;
        }
        var projectResponseGetAll = await _projectService.GetProjects(limit, offset);
        return Ok(projectResponseGetAll.Projects);
    }

    [HttpGet]
    [Route("me")]
    public async Task<IActionResult> GetUserProjects()
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return Unauthorized();

        var userProjects = await _projectService.GetUserProjects(currentUser!);

        return Ok(userProjects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(long id)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(id);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(projectResponseGetOne.Data);
    }

    [HttpGet("{id}/nested-data")]
    public async Task<IActionResult> GetProjectNestedDataById(long id)
    {
        var responseProject = await _projectService.GetProjectNestedDataById(id);
        if (responseProject.IsSuccess == false) return NotFound(responseProject.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, responseProject.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        return Ok(responseProject.Data);
    }

    [HttpPost]
    // [RequestTimeout(milliseconds: 300000)]
    public async Task<IActionResult> RegisterProject([FromForm] StartProjectInDTO projectData)
    {
        User? currentUser = await GetCurrentUser();
        if (currentUser is null) return Unauthorized();

        var fileValidation = new FileStorageService.ProjectDefaultFileOptions().EnsureFileIsOk(projectData.File);
        if (!fileValidation.IsSuccess) return ServiceFailure(fileValidation);
        if (projectData.TestFile != null)
        {
            var testValidation = new FileStorageService.ProjectTestFileOptions().EnsureFileIsOk(projectData.TestFile);
            if (!testValidation.IsSuccess) return ServiceFailure(testValidation);
        }
        var datasetValidation = ProjectCreationValidation.ValidateFiles(projectData);
        if (!datasetValidation.IsSuccess) return ServiceFailure(datasetValidation);
        using var study = JsonDocument.Parse(projectData.Pipeline);
        var validation = await _iaWebService.ValidateConfiguration(study.RootElement);
        if (!validation.IsSuccess) return ServiceFailure(validation);
        projectData.Pipeline = validation.Data.GetProperty("configuration").GetRawText();

        var responseProject = await _projectService.CreateProject(projectData, currentUser!);
        if (!responseProject.IsSuccess) return ServiceFailure(responseProject);

        async Task<IActionResult> RejectRegistration(ApplicationService.ResponseServiceAbstract failed)
        {
            await _projectService.DeleteProjectById(responseProject.Data.Id);
            return ServiceFailure(failed);
        }

        IFormFile trainFile = projectData.File;
        IFormFile? testFile = projectData.TestFile;
        if (projectData.EvaluationMode == "split")
        {
            var responseSplit = await _iaWebService.Split(projectData.File, responseProject.Data);
            if (!responseSplit.IsSuccess) return await RejectRegistration(responseSplit);
            var responseGetTrainFile = await _iaWebService.GetSplitTrainFile(responseSplit.Data.id);
            if (!responseGetTrainFile.IsSuccess) return await RejectRegistration(responseGetTrainFile);
            var responseGetTestFile = await _iaWebService.GetSplitTestFile(responseSplit.Data.id);
            if (!responseGetTestFile.IsSuccess) return await RejectRegistration(responseGetTestFile);
            trainFile = responseGetTrainFile.Data;
            testFile = responseGetTestFile.Data;
        }

        var predictionValidation = ProjectCreationValidation.ValidatePredictionKind(trainFile, projectData);
        if (!predictionValidation.IsSuccess) return await RejectRegistration(predictionValidation);

        var responseCreateProjectFileTrain = await _projectService.CreateProjectFile(trainFile, responseProject.Data, ProjectFile.EProjectFileType.TRAIN);
        if (!responseCreateProjectFileTrain.IsSuccess) return await RejectRegistration(responseCreateProjectFileTrain);
        if (testFile != null)
        {
            var responseCreateProjectFileTest = await _projectService.CreateProjectFile(testFile, responseProject.Data, ProjectFile.EProjectFileType.TEST);
            if (!responseCreateProjectFileTest.IsSuccess) return await RejectRegistration(responseCreateProjectFileTest);
        }

        RunPipelineFileDTO runPipelineTrainFileDTO = new() { File = await IaWebService.IFormFileToByteArray(trainFile), FileName = trainFile.FileName };
        RunPipelineFileDTO? runPipelineTestFileDTO = testFile == null ? null : new() { File = await IaWebService.IFormFileToByteArray(testFile), FileName = testFile.FileName };

        var getProjectBaselineMetricsResponse = await _iaWebService.StartBaselineAsync(runPipelineTrainFileDTO, runPipelineTestFileDTO, projectData);
        if (!getProjectBaselineMetricsResponse.IsSuccess) return await RejectRegistration(getProjectBaselineMetricsResponse);

        responseProject.Data.MetricsBaselineJson = JsonSerializer.Serialize(new { id = getProjectBaselineMetricsResponse.Data.Id, status = new { current = getProjectBaselineMetricsResponse.Data.Status.ToString() }, metrics = Array.Empty<object>() });
        var updateProjectResponse = await _projectService.UpdateProjectById(responseProject.Data);
        if (!updateProjectResponse.IsSuccess)
        {
            await _iaWebService.CancelJob(getProjectBaselineMetricsResponse.Data.Id);
            return await RejectRegistration(updateProjectResponse);
        }
        await _pullingService.CreateIaConnection(getProjectBaselineMetricsResponse.Data.Id, responseProject.Data.Id).StartPulling();


        var responseIaWeb = await _iaWebService.RunPipeline(runPipelineTrainFileDTO, runPipelineTestFileDTO, responseProject.Data, projectData.Pipeline, null);
        if (!responseIaWeb.IsSuccess)
        {
            await _iaWebService.CancelJob(getProjectBaselineMetricsResponse.Data.Id);
            _pullingService.DeleteIaConnection(getProjectBaselineMetricsResponse.Data.Id);
            return await RejectRegistration(responseIaWeb);
        }

        var responseProcess = await _processService.CreateProcess(responseIaWeb.Data, projectData.Pipeline, responseProject.Data);
        return Accepted(new { responseIaWeb.Data.Id, responseIaWeb.Data.Socket, responseIaWeb.Data.Status, projectId = responseProject.Data.Id, processId = responseProcess.Id, baselineId = getProjectBaselineMetricsResponse.Data.Id, responseProcess.MaxDuration });
    }

    [HttpGet("{projectId}/data-sample")]
    public async Task<ActionResult<ProjectService.ProjectFileFormated>> GetProjectDefaultFileLines(long projectId, [FromQuery] int rowCount = 10)
    {

        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        if (rowCount > 50 || rowCount < 1)
        {
            rowCount = 50;
        }
        var fileType = projectResponseGetOne.Data.EvaluationMode == "cross_validation" ? EProjectFileType.TRAIN : EProjectFileType.TEST;
        var responseFile = await _projectService.GetProjectFileFirstNLine(projectId, rowCount, fileType);
        if (responseFile.IsSuccess == false) return NotFound(responseFile.Errors);

        return Ok(responseFile.Data);
    }

    [HttpGet("{projectId}/defaultFile")]
    public async Task<ActionResult> GetProjectDefaultFile(long projectId)
    {
        var projectResponseGetOne = await _projectService.GetProjectById(projectId);
        if (projectResponseGetOne.IsSuccess == false) return NotFound(projectResponseGetOne.Errors);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, projectResponseGetOne.Data, Operations.ReadOnlyOwn);
        if (authorizationResult.Succeeded == false) return Unauthorized();

        var responseFile = await _projectService.GetProjectFile(projectId, ProjectFile.EProjectFileType.DEFAULT);
        if (responseFile.IsSuccess == false) return ServiceFailure(responseFile);

        return File(responseFile.Data.Data, responseFile.Data.MimeType);
    }

    [Authorize]
    [HttpGet("{projectId}/datasets/{dataset}")]
    public async Task<ActionResult> GetProjectDataset(long projectId, string dataset)
    {
        EProjectFileType? fileType = dataset.ToLowerInvariant() switch
        {
            "train" => EProjectFileType.TRAIN,
            "test" => EProjectFileType.TEST,
            _ => null
        };
        if (fileType == null) return BadRequest(new { error = "The requested dataset must be train or test." });

        var responseProject = await _projectService.GetProjectById(projectId);
        if (!responseProject.IsSuccess) return NotFound(responseProject.Errors);

        var ownerAuthorization = await _authorizationService.AuthorizeAsync(User, responseProject.Data, Operations.ReadOnlyOwn);
        if (!ownerAuthorization.Succeeded) return Unauthorized();

        var responseFile = await _projectService.GetProjectFile(projectId, fileType.Value);
        if (!responseFile.IsSuccess) return ServiceFailure(responseFile);

        return File(responseFile.Data.Data, responseFile.Data.MimeType, $"projet-{projectId}-{dataset.ToLowerInvariant()}.csv");
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> DeleteProjectById(long id)
    {
      var responseDelete = await _projectService.DeleteProjectById(id);
      if (!responseDelete.IsSuccess) return ServiceFailure(responseDelete);

      return Ok();
    }
}
