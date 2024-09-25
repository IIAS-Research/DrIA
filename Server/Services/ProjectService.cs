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

    async public Task CreateProject(StartProjectInDTO projectData)
    {
        return;
    }
}
