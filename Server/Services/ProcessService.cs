using Server.DTOs;
using Server.Models;
using Server.Repositories;

namespace Server.Services;

public class ProcessService : ApplicationService
{
    private readonly ProcessRepository _processRepository;

    public ProcessService(ProcessRepository processRepository)
    {
        _processRepository = processRepository;
    }

   async public Task CreateProcess(StartProjectInDTO projectData, Project project)
    {
        //await _processRepository.CreateProcess(StartProjectInDTO projectData, Project project);
    }
}
