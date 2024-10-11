using Microsoft.EntityFrameworkCore;
using Server.DAL.Db;
using Server.DTOs;
using Server.Models;
using static Server.Models.ProjectFile;

namespace Server.Repositories;
public class ProjectRepository(MainDbContext dbContext)
{
  private readonly MainDbContext _dbContext = dbContext;

  public async Task<List<Project>> GetProjectByUser(User user)
  {
    var projects = await _dbContext.Projects
        .Where(p => p.UserId == user.Id)
        .ToListAsync();
    return projects;
  }

  public async Task<List<Project>> GetProjects()
  {
    return await _dbContext.Projects.ToListAsync();
  }

  public async Task<Project?> GetProjectById(long id)
  {
    return await _dbContext.Projects.FindAsync(id);
  }

  public async Task<Project> CreateProject(StartProjectInDTO projectData, User user)
  {
    Project project = new Project
    {
      Title = projectData.Title,
      Description = projectData.Description,
      CsvColumnToPredict = projectData.CsvColumnToPredict,
      RowDepedencies = projectData.RowDepedencies,
      RowDepedenciesLinked = projectData.RowDepedenciesLinked,
      DatasetHasIdColumn = projectData.DatasetHasIdColumn,
      DatasetHasIdColumnList = projectData.DatasetHasIdColumnList,
      UserId = user.Id
    };
    await _dbContext.Projects.AddRangeAsync(project);
    await _dbContext.SaveChangesAsync();
    return project;
  }

  public async Task<ProjectFile> CreateProjectFile(Project project, string filename, string filePath, string mimeType, EProjectFileType type)
  {
    ProjectFile projectFile = new ProjectFile
    {
      Filename = filename,
      StorageFilename = filePath,
      ProjectId = project.Id,
      MimeType = mimeType,
      FileType = type
    };
    await _dbContext.ProjectFiles.AddRangeAsync(projectFile);
    await _dbContext.SaveChangesAsync();
    return projectFile;
  }

  public async Task<ProjectFile?> GetProjectFileByProjectId(long id, EProjectFileType fileType)
  {
    return await _dbContext.ProjectFiles.Where(pj => pj.ProjectId == id && pj.FileType == fileType)
      .FirstOrDefaultAsync();
  }

  public async Task<Project?> GetProjectNestedDataById(long id)
  {
    return await _dbContext.Projects
      .Include(proj => proj.Processes)
      .ThenInclude(proc => proc.Models)
      .FirstOrDefaultAsync(proj => proj.Id == id);
  }

  public async Task<ProjectFile?> GetProjectFileAsync(long projectId, ProjectFile.EProjectFileType type)
  {
    return await _dbContext.ProjectFiles.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.FileType == type);
  }

  public async Task<string?> GetColumnToPredictByModelId(long modelId)
  {
    return (await _dbContext.Projects
   .Where(p => p.Processes.Any(proc => proc.Models.Any(m => m.Id == modelId)))
   .FirstOrDefaultAsync())?.CsvColumnToPredict;
  }

  public async Task<string?> GetColumnToPredictByProjectId(long projectId)
  {
    return (await _dbContext.Projects.FindAsync(projectId))?.CsvColumnToPredict;
  }



}