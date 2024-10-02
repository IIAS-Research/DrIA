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

    async public Task<Process> CreateProcess(RunPipelineOutDTO pipelineResponse, string pipelineData, Project project)
    {
        return await _processRepository.CreateProcess(pipelineResponse, pipelineData, project);
    }
}
