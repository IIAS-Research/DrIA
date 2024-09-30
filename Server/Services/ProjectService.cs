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

    async public Task<FileStorageService.FileStorageResponseWrite> WriteProjectFile(IFormFile file)
    {
        return await _fileStorageService.WriteProjectFileToStorageAsync(file);
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
        var responseWriteFile = await WriteProjectFile(projectData.File);
        if (!response.EnsureOtherServiceIsSuccess(responseWriteFile)) return response;
        await _projectRepository.CreateProjectFile(project, projectData.File.FileName, responseWriteFile.FilePath, responseWriteFile.MimeType);
        response.SetSuccessful(project);
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

    // Service responses
    public class ProjectResponseGetOne : ResponseService
    {
        public Project? Project { get; set; }
    }

    public class ProjectResponseGetAll : ResponseService
    {
        public List<Project> Projects { get; set; } = [];
    }

}
