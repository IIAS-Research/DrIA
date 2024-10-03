using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Server.DTOs;
using Server.Models;
using Server.Repositories;

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

  async public Task<ResponseService<FileStorageService.FileStorageResponseWrite>> WriteProjectFile(IFormFile file, ProjectFile.EProjectFileType type)
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

    await _projectRepository.CreateProjectFile(project, responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType);
    response.SetSuccessful(project);
    return response;
  }

  async public Task<ResponseService<ProjectFile>> CreateProjectFile(IFormFile file, Project project, ProjectFile.EProjectFileType type)
  {
    var response = new ResponseService<ProjectFile>();

    var responseWriteFile = await WriteProjectFile(file, type);
    if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;

    var projectFile = await _projectRepository.CreateProjectFile(project, responseWriteFile.Data.OriginalFilename, responseWriteFile.Data.FilePath, responseWriteFile.Data.MimeType);
    response.SetSuccessful(projectFile);
    return response;
  }

  async public Task<ProjectResponseGetOne> GetProjectById(long id)
  {
    ProjectResponseGetOne projectResponseGetOne = new();
    Project? project = await _projectRepository.GetProjectById(id);
    if (project == null)
    {
      projectResponseGetOne.AddError("Project not found for this id");
      return projectResponseGetOne;
    }
    projectResponseGetOne.IsSuccess = true;
    projectResponseGetOne.Project = project;
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

  async public Task<ResponseService<ProjectResponseGetDefaultFile>> GetProjectDefaultFile(long id)
  {
    var response = new ResponseService<ProjectResponseGetDefaultFile>();
    var projectFile = await _projectRepository.GetProjectFileByProjectId(id);
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
    var responseData = new ProjectResponseGetDefaultFile()
    {
      Data = responseFile.Data.FileBytes!,
      MimeType = projectFile.MimeType
    };
    response.SetSuccessful(responseData);
    return response;
  }

  async public Task<ResponseService<ProjectFileFormated>> GetProjectDefaultFileFirstNLine(long projectId, int rowCount)
  {
    var response = new ResponseService<ProjectFileFormated>();

    var responseGetFile = await GetProjectDefaultFile(projectId);
    if (!responseGetFile.IsSuccess)
    {
      response.Errors = responseGetFile.Errors;
      return response;
    }

    var responseFormated = GetNFirstLineOfFile(responseGetFile.Data.Data, rowCount);
    if (!responseFormated.IsSuccess)
    {
      response.Errors = responseFormated.Errors;
      return response;
    }

    response.SetSuccessful(responseFormated.Data);
    return response;
  }

  private static ResponseService<ProjectFileFormated> GetNFirstLineOfFile(byte[] dataFile, int rowCount)
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
        Name = headerCell.Replace(" ", "")
      };
      fileFormated.Columns.Add(newColumn);
    }
    for (var i = 1; i <= rowCount + 1; i++)
    {
      var recordAsDict = (IDictionary<string, object>)records[i];
      if (recordAsDict == null)
      {
        continue;
      }
      fileFormated.Rows.Add(recordAsDict);
    }

    response.SetSuccessful(fileFormated);
    return response;
  }

  // Service responses
  public class ProjectResponseGetDefaultFile
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
    public List<ProjectFileFormatedColumn> Columns = [];
    public List<IDictionary<string, object>> Rows = [];

    public record ProjectFileFormatedColumn
    {
      public string Label = string.Empty;
      public string Name = string.Empty;
    }
  }

}
