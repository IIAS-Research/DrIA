using Server.DTOs;
using Server.Models;
using Server.Repositories;
using static Server.Models.Process;

namespace Server.Services;

public class ProcessService : ApplicationService
{
    private readonly ProcessRepository _processRepository;
    private readonly IaPullingModelService _pullingService;

    public ProcessService(ProcessRepository processRepository, IaPullingModelService pullingService)
    {
        _processRepository = processRepository;
        _pullingService = pullingService;
    }

    async public Task<Process> CreateProcess(RunPipelineOutDTO pipelineResponse, string pipelineData, Project project)
    {
        var process = await _processRepository.CreateProcess(pipelineResponse, pipelineData, project);
        await _pullingService.CreateIaConnection(process.IaRemoteId).StartPulling();
        return process;
    }

    public async Task<ResponseService<ProcessPipeline>> GetProcessPipeline(long processId, long projectId)
    {
        var responseService = new ResponseService<ProcessPipeline>();

        var pipeline = await _processRepository.GetProcessPipeline(processId, projectId);
        if (pipeline == null)
        {
            responseService.AddError("No results found for the provided information");
        }
        else
        {
            responseService.SetSuccessful(pipeline);
        }
        return responseService;
    }

}
