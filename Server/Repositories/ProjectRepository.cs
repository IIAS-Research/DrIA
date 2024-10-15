using System.Text.Json;
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

  public async Task<List<Project>> GetProjects(int limit, int offset)
  {
    return await _dbContext.Projects.Skip(offset).Take(limit).ToListAsync();
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
      UserId = user.Id,
      SurvivalModel = projectData.SurvivalModel,
      SurvivalModelDatasetTargetTime = projectData.SurvivalModelDatasetTargetTime,
      MetricsBaselineJson = JsonSerializer.Serialize("")
    };
    await _dbContext.Projects.AddRangeAsync(project);
    await _dbContext.SaveChangesAsync();
    return project;
  }

  public async Task<Project?> UpdateProject(Project project)
  {
    var projectToUpdate = await _dbContext.Projects.FindAsync(project.Id);
    if (projectToUpdate == null) return null;

    // Update the fields of the existing project
    projectToUpdate.Title = project.Title;
    projectToUpdate.Description = project.Description;
    projectToUpdate.CsvColumnToPredict = project.CsvColumnToPredict;
    projectToUpdate.RowDepedencies = project.RowDepedencies;
    projectToUpdate.RowDepedenciesLinked = project.RowDepedenciesLinked;
    projectToUpdate.DatasetHasIdColumn = project.DatasetHasIdColumn;
    projectToUpdate.DatasetHasIdColumnList = project.DatasetHasIdColumnList;
    projectToUpdate.SurvivalModel = project.SurvivalModel;
    projectToUpdate.SurvivalModelDatasetTargetTime = project.SurvivalModelDatasetTargetTime;
    projectToUpdate.MetricsBaselineJson = project.MetricsBaselineJson;

    // Save the updated project to the database
    _dbContext.Projects.Update(projectToUpdate);
    await _dbContext.SaveChangesAsync();

    return projectToUpdate;
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

  public async Task<Project?> GetProjectByProcessId(long processId)
  {
    return await _dbContext.Projects.Where(p => p.Processes.Any(p => p.Id == processId)).FirstOrDefaultAsync();
  }

  public void DeleteProjectbyId(Project project)
  {
    _dbContext.Projects.Remove(project);
    _dbContext.SaveChanges();
  }


}