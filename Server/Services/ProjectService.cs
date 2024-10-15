using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Server.DTOs;
using Server.Models;
using Server.Repositories;
using static Server.Models.ProjectFile;
using static Server.Services.FileStorageService;

namespace Server.Services;

public class ProjectService : ApplicationService
{
  private readonly ProjectRepository _projectRepository;
  private readonly FileStorageService _fileStorageService;

  public ProjectService(ProjectRepository projectRepository, FileStorageService fileStorageService)
  {
    _projectRepository = projectRepository;
    _fileStorageService = fileStorageService;
  }

  async public Task<List<Project>> GetUserProjects(User user)
  {
    return await _projectRepository.GetProjectByUser(user);
  }

  async public Task<ResponseService<FileStorageResponseWrite>> WriteProjectFile(IFormFile file, ProjectFile.EProjectFileType type)
  {
    return await _fileStorageService.WriteProjectFileToStorageAsync(file, type);
  }

  async public Task<ProjectResponseGetAll> GetProjects()
  {
    ProjectResponseGetAll projectResponseGetAll = new();
    List<Project> projects = await _projectRepository.GetProjects();
    if (projects.Count == 0)
    {
      projectResponseGetAll.AddError("No projects found");
      return projectResponseGetAll;
    }
    projectResponseGetAll.IsSuccess = true;
    projectResponseGetAll.Projects = projects;
    return projectResponseGetAll;
  }

  async public Task<ResponseService<Project>> CreateProject(StartProjectInDTO projectData, User user)
  {
    var response = new ResponseService<Project>();
    Project project = await _projectRepository.CreateProject(projectData, user);

    var responseWriteFile = await WriteProjectFile(projectData.File, ProjectFile.EProjectFileType.DEFAULT);
    if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

    await _projectRepository.CreateProjectFile(project, responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType, ProjectFile.EProjectFileType.DEFAULT);
    response.SetSuccessful(project);
    return response;
  }

  async public Task<ResponseService<ProjectFile>> CreateProjectFile(IFormFile file, Project project, ProjectFile.EProjectFileType type)
  {
    var response = new ResponseService<ProjectFile>();

    var responseWriteFile = await WriteProjectFile(file, type);
    if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

    var projectFile = await _projectRepository.CreateProjectFile(project, responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType, type);
    response.SetSuccessful(projectFile);
    return response;
  }

  async public Task<ResponseService<Project>> GetProjectById(long id)
  {
    ResponseService<Project> projectResponseGetOne = new();
    Project? project = await _projectRepository.GetProjectById(id);
    if (project == null)
    {
      projectResponseGetOne.AddError("Project not found for this id");
      return projectResponseGetOne;
    }
    projectResponseGetOne.SetSuccessful(project);
    return projectResponseGetOne;
  }

  async public Task<ResponseService<Project>> GetProjectNestedDataById(long id)
  {
    var response = new ResponseService<Project>();
    Project? projectWithNestedData = await _projectRepository.GetProjectNestedDataById(id);
    if (projectWithNestedData == null)
    {
      response.AddError("Project not found for this id");
      return response;
    }
    response.SetSuccessful(projectWithNestedData);
    return response;
  }

  async public Task<ResponseService<ProjectResponseGetFile>> GetProjectFile(long projectId, ProjectFile.EProjectFileType fileType)
  {
    var response = new ResponseService<ProjectResponseGetFile>();
    var projectFile = await _projectRepository.GetProjectFileByProjectId(projectId, fileType);
    if (projectFile == null)
    {
      response.AddError("ProjectFile not found");
      return response;
    }
    var responseFile = await _fileStorageService.GetFileFromStorageAsync(projectFile.StorageFilename);
    if (!responseFile.IsSuccess)
    {
      response.Errors = responseFile.Errors;
      return response;
    }
    var responseData = new ProjectResponseGetFile()
    {
      Data = responseFile.Data.FileBytes!,
      MimeType = projectFile.MimeType
    };
    response.SetSuccessful(responseData);
    return response;
  }

  async public Task<ResponseService<ProjectFileFormated>> GetProjectFileFirstNLine(long projectId, int rowCount, EProjectFileType type)
  {
    var response = new ResponseService<ProjectFileFormated>();

    var responseGetFile = await GetProjectFile(projectId, type);
    if (!responseGetFile.IsSuccess)
    {
      response.Errors = responseGetFile.Errors;
      return response;
    }

    var GetColumnToPredictByProjectId = await _projectRepository.GetColumnToPredictByProjectId(projectId);
    if (GetColumnToPredictByProjectId == null)
    {
      response.AddError("No project found for this id.");
      return response;
    }

    var responseFormated = GetNFirstLineOfFile(responseGetFile.Data.Data, rowCount, GetColumnToPredictByProjectId);
    if (!responseFormated.IsSuccess)
    {
      response.Errors = responseFormated.Errors;
      return response;
    }

    response.SetSuccessful(responseFormated.Data);
    return response;
  }

  public async Task<ResponseService<GetTrainAndTestFilesOutDTO>> GetTrainAndTestFiles(long projectId)
  {
    var responseService = new ResponseService<GetTrainAndTestFilesOutDTO>();
    var projectFileTrain = await _projectRepository.GetProjectFileAsync(projectId, ProjectFile.EProjectFileType.TRAIN);
    var projectFileTest = await _projectRepository.GetProjectFileAsync(projectId, ProjectFile.EProjectFileType.TEST);
    if (projectFileTrain == null || projectFileTest == null)
    {
      responseService.AddError("Couldn't find either Test or Train File in DB, both are required for this method.");
      return responseService;
    }
    var getTrainFileResponse = await _fileStorageService.GetFileFromStorageAsync(projectFileTrain.StorageFilename);
    var getTestFileResponse = await _fileStorageService.GetFileFromStorageAsync(projectFileTest.StorageFilename);

    // Handle potential errors
    if (!getTrainFileResponse.IsSuccess) responseService.Errors.AddRange(getTrainFileResponse.Errors);
    if (!getTestFileResponse.IsSuccess) responseService.Errors.AddRange(getTestFileResponse.Errors);
    if (!getTrainFileResponse.IsSuccess || !getTestFileResponse.IsSuccess) return responseService;

    RunPipelineFileDTO trainFile = new() { File = getTrainFileResponse.Data.FileBytes!, FileName = projectFileTrain.Filename };
    RunPipelineFileDTO testFile = new() { File = getTestFileResponse.Data.FileBytes!, FileName = projectFileTest.Filename };

    responseService.SetSuccessful(new GetTrainAndTestFilesOutDTO { TrainFile = trainFile, TestFile = testFile });

    return responseService;
  }

  public async Task<ResponseService<string?>> GetColumnToPredictByModelId(long modelId)
  {
    var serviceResponse = new ResponseService<string?>();
    var columnToPredict = await _projectRepository.GetColumnToPredictByModelId(modelId);
    if (columnToPredict == null)
    {
      serviceResponse.AddError("No project found containing this model id.");
    }
    else
    {
      serviceResponse.SetSuccessful(columnToPredict);
    }
    return serviceResponse;
  }

  public async Task<ResponseService<Project?>> GetProjectByProcessId(long processId)
  {
    var serviceResponse = new ResponseService<Project?>();
    var project = await _projectRepository.GetProjectByProcessId(processId);
    if (project == null)
    {
      serviceResponse.AddError("No project found containing this process id.");
    }
    else
    {
      serviceResponse.SetSuccessful(project);
    }
    return serviceResponse;
  }

  async public Task<ResponseService<Project?>> UpdateProjectById(Project project)
  {
    var response = new ResponseService<Project?>();
    var projectResponse = await _projectRepository.UpdateProject(project);

    if (projectResponse == null)
    {
      response.AddError("Error trying to update project.");
      return response;
    }

    response.SetSuccessful(project);
    return response;
  }

  private static ResponseService<ProjectFileFormated> GetNFirstLineOfFile(byte[] dataFile, int rowCount, string colToPredict)
  {
    var response = new ResponseService<ProjectFileFormated>();
    var fileFormated = new ProjectFileFormated() { };
    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
      HasHeaderRecord = true,
      DetectDelimiter = true
    };

    using var reader = new StreamReader(new MemoryStream(dataFile));
    using var csvReader = new CsvReader(reader, config);
    var records = csvReader.GetRecords<dynamic>().ToList();
    var headerRow = ((IDictionary<string, object>)records.First()).Keys;
    foreach (var headerCell in headerRow)
    {
      var newColumn = new ProjectFileFormated.ProjectFileFormatedColumn
      {
        Label = headerCell,
        Name = headerCell.Replace(" ", ""),
        IsColToPredict = headerCell == colToPredict ? true : false
      };
      fileFormated.Columns.Add(newColumn);
    }
    for (var i = 0; i < rowCount; i++)
    {
      var recordAsDict = (IDictionary<string, object>)records[i];
      if (recordAsDict == null)
      {
        continue;
      }
      fileFormated.Rows.Add(recordAsDict.ToDictionary(entry => entry.Key.Replace(" ", ""), entry => entry.Value));
    }

    response.SetSuccessful(fileFormated);
    return response;
  }

  public async Task<ResponseService> DeleteProjectById(long id)
  {
    var response = new ResponseService();
    var project = await _projectRepository.GetProjectById(id);
    if (project == null)
    {
      response.AddError("Project not found");
      return response;
    }
    _projectRepository.DeleteProjectbyId(project);
    response.SetSuccessful();
    return response;
  }

  // Service responses
  public class ProjectResponseGetFile
  {
    public byte[] Data = [];
    public string MimeType = string.Empty;
  }

  public class ProjectResponseGetOne : ResponseService
  {
    public Project? Project { get; set; }
  }

  public class ProjectResponseGetAll : ResponseService
  {
    public List<Project> Projects { get; set; } = [];
  }

  // Utils
  public record ProjectFileFormated
  {
    public List<ProjectFileFormatedColumn> Columns { get; set; } = new();
    public List<IDictionary<string, object>> Rows { get; set; } = new();

    public record ProjectFileFormatedColumn
    {
      public string Label { get; set; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public required bool IsColToPredict { get; set; } = false;
    }
  }

}
