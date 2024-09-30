using Server.DAL.Db;
using Server.DTOs;
using Server.Models;

namespace Server.Repositories;
public class ProcessRepository(MainDbContext dbContext)
{
    private readonly MainDbContext _dbContext = dbContext;

    public async Task<Process> CreateProcess(RunPipelineOutDTO pipelineResponse, Process.ProcessPipeline pipelineData, Project project)
    {
      Process process = new Process{
          Date = DateTime.Now,
          Duration = 0L,
          Status = pipelineResponse.status,
          Pipeline = pipelineData, // serais mieux de l'avoir du webservice
          ProjectId = project.Id
        };
        await _dbContext.Processes.AddRangeAsync(process);
        await _dbContext.SaveChangesAsync();
        return process;
    }
}