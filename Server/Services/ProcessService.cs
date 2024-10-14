using Server.DTOs;
using Server.Models;
using Server.Repositories;
using static Server.Models.Process;

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

    public async Task<ResponseService<ProcessPipeline>> GetProcessPipeline(long processId, long projectId)
    {
        var responseService = new ResponseService<ProcessPipeline>();

        var pipeline = await _processRepository.GetProcessPipeline(processId, projectId);
        if (pipeline == null)
        {
            responseService.AddError("No result found for the provided informations");
        }
        else
        {
            responseService.SetSuccessful(pipeline);
        }
        return responseService;
    }

}
